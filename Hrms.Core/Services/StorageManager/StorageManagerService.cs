using Hrms.Core.Services.Storage;
using Onepunch.AuditEngine;
using Onepunch.Common.Lib.Exceptions;

namespace Hrms.Core.Services.StorageManager;

public sealed record StorageYearDto(int Year, int FileCount, long Bytes);

public sealed record StorageCategoryDto(string Category, string Label, int FileCount, long Bytes, List<StorageYearDto> Years);

/// <summary>A company's stored files grouped by category, then by the year they were stored.</summary>
public sealed record StorageBreakdownDto(int FileCount, long Bytes, List<StorageCategoryDto> Categories);

/// <param name="Deletable">False for a file uploaded moments ago that no record claims yet (its form may still be saving).</param>
public sealed record StoredFileDto(
    string Key, string FileName, long SizeBytes, DateTime StoredAtUtc,
    string? RecordLabel, bool Locked, bool Deletable, Guid? AttachmentId);

public sealed record StoredFilePage(List<StoredFileDto> Items, int Total, int Page, int PageSize);

/// <summary>
/// Which files to delete: a whole group (<see cref="Category"/> + <see cref="Year"/>), or the
/// <see cref="Keys"/> picked on the screen. <see cref="ConfirmLocked"/> must be true when any of
/// them belongs to a record that is already decided.
/// </summary>
public sealed record StorageDeleteRequest(string? Category, int? Year, List<string>? Keys, bool ConfirmLocked = false);

/// <summary>What a delete would remove, shown before the user confirms.</summary>
public sealed record StorageDeletePreview(int FileCount, long Bytes, int LockedCount);

public sealed record StorageDeleteResult(int DeletedCount, long FreedBytes, int FailedCount);

/// <summary>Who is deleting, for the audit trail. Null actor = the signed-in user of the request.</summary>
public sealed record StorageManagerCaller(AuditActor? Actor, string Channel, string? Reason)
{
    public static readonly StorageManagerCaller SignedInUser = new(null, "HRIS", null);

    public static StorageManagerCaller PlatformOperator(string operatorId, string? operatorName, string reason) =>
        new(new AuditActor(operatorId, $"Platform support · {operatorName ?? operatorId}", AuditActorTypes.Service), "Platform console", reason);
}

/// <summary>
/// The Storage Manager: lets an account use the storage it already has instead of buying more, by
/// showing what fills a company's folder -- grouped by category (see <see cref="IStoredFileCategory"/>)
/// and year -- and deleting what is no longer needed. Only files are deleted: a leave application
/// keeps its record, it just no longer has the attachment.
///
/// The folder listing is the source of truth (the same thing <see cref="StorageUsageMeter"/> charges
/// for), so the totals here always add up to the usage shown on Billing. A delete saves the record
/// changes and its audit entry in one commit, then removes the files; a file that fails to delete is
/// counted and logged, and simply shows up again as an unused file.
/// </summary>
public class StorageManagerService
{
    /// <summary>How old an unclaimed file must be before it is offered as unused.</summary>
    public static readonly TimeSpan UnlinkedGracePeriod = TimeSpan.FromHours(24);

    public const int MaxPageSize = 100;
    private const int QueryChunk = 500;

    private readonly IUnitOfWorkService _uow;
    private readonly IFileStorage _storage;
    private readonly IStorageUsageMeter _meter;
    private readonly IReadOnlyList<IStoredFileCategory> _chain;
    private readonly IAuditWriter? _audit;
    private readonly TimeProvider _clock;

    public StorageManagerService(
        IUnitOfWorkService uow, IFileStorage storage, IStorageUsageMeter meter, StoredFileCategoryChain chain,
        IAuditWriter? audit = null, TimeProvider? clock = null)
    {
        _uow = uow;
        _storage = storage;
        _meter = meter;
        _chain = chain.Categories;
        _audit = audit;
        _clock = clock ?? TimeProvider.System;
    }

    private sealed record ClassifiedFile(StoredObject File, StorageKeyParts Parts, IStoredFileCategory Category, int Year, bool Deletable);

    public async Task<StorageBreakdownDto> GetBreakdownAsync(Guid tenantId, CancellationToken token)
    {
        var files = await ClassifyAsync(tenantId, token);
        var categories = files
            .GroupBy(f => f.Category)
            .Select(c => new StorageCategoryDto(
                c.Key.Key, c.Key.Label, c.Count(), c.Sum(f => f.File.Size),
                c.GroupBy(f => f.Year)
                    .OrderByDescending(y => y.Key)
                    .Select(y => new StorageYearDto(y.Key, y.Count(), y.Sum(f => f.File.Size)))
                    .ToList()))
            .OrderByDescending(c => c.Bytes)
            .ToList();
        return new StorageBreakdownDto(files.Count, files.Sum(f => f.File.Size), categories);
    }

    /// <summary>One page of a group's files, oldest first, with the record each belongs to.</summary>
    public async Task<StoredFilePage> GetFilesAsync(
        Guid tenantId, string category, int year, int page, int pageSize, CancellationToken token)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var group = (await ClassifyAsync(tenantId, token))
            .Where(f => f.Category.Key == category && f.Year == year)
            .OrderBy(f => f.File.LastModifiedUtc)
            .ToList();

        var slice = group.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var owners = await DescribeAsync(tenantId, slice, token);
        var items = slice.Select(f =>
        {
            var owner = owners.GetValueOrDefault(f.File.Key);
            return new StoredFileDto(
                f.File.Key, f.Parts.FileName, f.File.Size, f.File.LastModifiedUtc,
                owner?.RecordLabel, owner?.Locked ?? false, f.Deletable, owner?.AttachmentId);
        }).ToList();
        return new StoredFilePage(items, group.Count, page, pageSize);
    }

    public async Task<StorageDeletePreview> PreviewDeleteAsync(Guid tenantId, StorageDeleteRequest request, CancellationToken token)
    {
        var targets = await SelectTargetsAsync(tenantId, request, token);
        var locked = await CountLockedAsync(tenantId, targets, token);
        return new StorageDeletePreview(targets.Count, targets.Sum(f => f.File.Size), locked);
    }

    public async Task<StorageDeleteResult> DeleteAsync(
        Guid tenantId, StorageDeleteRequest request, StorageManagerCaller caller, CancellationToken token)
    {
        var targets = await SelectTargetsAsync(tenantId, request, token);
        if (targets.Count == 0) throw new GuardException("There are no files to delete.");

        var locked = await CountLockedAsync(tenantId, targets, token);
        if (locked > 0 && !request.ConfirmLocked)
            throw new GuardException(
                $"{locked} of these files belong to records that are already decided. Confirm that you want to delete them too.");

        // Records first, in one commit with the audit entry; the files only once nothing points at them.
        foreach (var group in targets.GroupBy(f => f.Category))
            foreach (var chunk in group.Select(f => f.File.Key).Chunk(QueryChunk))
                await group.Key.ReleaseAsync(tenantId, chunk, token);

        var bytes = targets.Sum(f => f.File.Size);
        await WriteAuditAsync(tenantId, request, caller, targets, locked, bytes, token);
        await _uow.CommitChangesAsync("", token);

        var deleted = 0;
        long freed = 0;
        foreach (var chunk in targets.Chunk(QueryChunk))
        {
            try
            {
                await _storage.DeleteManyAsync(chunk.Select(f => f.File.Key).ToList(), token);
                deleted += chunk.Length;
                freed += chunk.Sum(f => f.File.Size);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.Warning(ex, "Storage Manager couldn't delete {Count} file(s) of company {TenantId}", chunk.Length, tenantId);
            }
        }
        _meter.Removed(tenantId);
        return new StorageDeleteResult(deleted, freed, targets.Count - deleted);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Every file of the company with its category: the first category in the chain that handles the
    /// file's area, as long as a record still points at the file. A file nobody points at is "unused"
    /// once it is older than the grace period; until then it stays in its category, not deletable.
    /// </summary>
    private async Task<List<ClassifiedFile>> ClassifyAsync(Guid tenantId, CancellationToken token)
    {
        if (tenantId == Guid.Empty) throw new GuardException("No company is selected.");

        var listing = await _storage.ListAsync(StorageKeys.TenantPrefix(tenantId), token);
        var unlinked = _chain[^1];
        var now = _clock.GetUtcNow().UtcDateTime;
        var result = new List<ClassifiedFile>(listing.Count);

        var parsed = listing
            .Select(file => (File: file, Parts: StorageKeyParts.Parse(file.Key, tenantId)))
            .Where(f => f.Parts != null)
            .Select(f => (f.File, Parts: f.Parts!))
            .ToList();

        foreach (var area in parsed.GroupBy(f => f.Parts.Area))
        {
            var category = _chain.First(c => c.Handles(area.Key));
            var referenced = new HashSet<string>();
            if (!ReferenceEquals(category, unlinked))
                foreach (var chunk in area.Select(f => f.File.Key).Chunk(QueryChunk))
                    referenced.UnionWith(await category.FindReferencedAsync(tenantId, chunk, token));

            foreach (var (file, parts) in area)
            {
                var year = parts.Year ?? file.LastModifiedUtc.Year;
                if (referenced.Contains(file.Key))
                    result.Add(new ClassifiedFile(file, parts, category, year, Deletable: true));
                else if (now - file.LastModifiedUtc >= UnlinkedGracePeriod)
                    result.Add(new ClassifiedFile(file, parts, unlinked, year, Deletable: true));
                else
                    result.Add(new ClassifiedFile(file, parts, category, year, Deletable: false));
            }
        }
        return result;
    }

    private async Task<List<ClassifiedFile>> SelectTargetsAsync(Guid tenantId, StorageDeleteRequest request, CancellationToken token)
    {
        var files = await ClassifyAsync(tenantId, token);
        IEnumerable<ClassifiedFile> chosen;
        if (request.Keys is { Count: > 0 } keys)
        {
            // Keys from the screen are only ever matched against the company's own listing.
            var wanted = keys.Where(k => StorageKeys.BelongsTo(k, tenantId)).ToHashSet(StringComparer.Ordinal);
            chosen = files.Where(f => wanted.Contains(f.File.Key));
        }
        else if (!string.IsNullOrWhiteSpace(request.Category) && request.Year is { } year)
        {
            chosen = files.Where(f => f.Category.Key == request.Category && f.Year == year);
        }
        else
        {
            throw new GuardException("Choose a category and year, or the files to delete.");
        }
        return chosen.Where(f => f.Deletable).ToList();
    }

    private async Task<Dictionary<string, StoredFileOwner>> DescribeAsync(
        Guid tenantId, IReadOnlyCollection<ClassifiedFile> files, CancellationToken token)
    {
        var result = new Dictionary<string, StoredFileOwner>();
        foreach (var group in files.GroupBy(f => f.Category))
            foreach (var chunk in group.Select(f => f.File.Key).Chunk(QueryChunk))
                foreach (var (key, owner) in await group.Key.DescribeAsync(tenantId, chunk, token))
                    result[key] = owner;
        return result;
    }

    private async Task<int> CountLockedAsync(Guid tenantId, IReadOnlyCollection<ClassifiedFile> files, CancellationToken token) =>
        (await DescribeAsync(tenantId, files, token)).Values.Count(o => o.Locked);

    private Task WriteAuditAsync(
        Guid tenantId, StorageDeleteRequest request, StorageManagerCaller caller,
        IReadOnlyCollection<ClassifiedFile> targets, int locked, long bytes, CancellationToken token)
    {
        if (_audit == null) return Task.CompletedTask;

        var categories = targets.Select(f => f.Category.Label).Distinct().ToList();
        var scope = request.Keys is { Count: > 0 }
            ? $"{targets.Count} selected file(s)"
            : $"all {targets.Count} file(s) of {request.Year}";
        var metadata = new Dictionary<string, string?>
        {
            ["Categories"] = string.Join(", ", categories),
            ["Files"] = targets.Count.ToString(),
            ["Bytes"] = bytes.ToString(),
            ["LockedRecordFiles"] = locked.ToString(),
            ["Channel"] = caller.Channel,
        };
        if (request.Year is { } year) metadata["Year"] = year.ToString();
        if (!string.IsNullOrWhiteSpace(caller.Reason)) metadata["Reason"] = caller.Reason;

        return _audit.WriteAsync(new AuditEvent
        {
            EventType = AuditEventTypes.Delete,
            EntityType = "StoredFiles",
            EntityId = request.Category ?? string.Join(",", targets.Select(f => f.Category.Key).Distinct()),
            OrganizationId = tenantId.ToString(),
            Actor = caller.Actor,
            Description = $"Deleted {scope} from {string.Join(", ", categories)} to free {StorageSize.Format(bytes)}",
            Metadata = metadata,
        }, token);
    }
}

/// <summary>The categories in the order they are asked; <see cref="UnlinkedFileCategory"/> is always last.</summary>
public sealed class StoredFileCategoryChain
{
    public StoredFileCategoryChain(
        IUnitOfWorkService uow, IEnumerable<Attachments.IAttachmentOwnerHandler> attachmentHandlers,
        IEnumerable<IStoredFileCategory> categories)
    {
        Categories =
        [
            .. attachmentHandlers.Select(h => (IStoredFileCategory)new AttachmentFileCategory(uow, h)),
            .. categories.Where(c => c is not UnlinkedFileCategory),
            new UnlinkedFileCategory(),
        ];
    }

    public IReadOnlyList<IStoredFileCategory> Categories { get; }
}

internal static class StorageSize
{
    public static string Format(long bytes) => bytes switch
    {
        >= 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024 * 1024):0.##} GB",
        >= 1024L * 1024 => $"{bytes / (1024.0 * 1024):0.##} MB",
        >= 1024 => $"{bytes / 1024.0:0.##} KB",
        _ => $"{bytes} bytes",
    };
}
