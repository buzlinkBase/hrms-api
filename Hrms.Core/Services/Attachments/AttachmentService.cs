using Hrms.Core.Services.Storage;
using Hrms.Core.Services.Subscriptions;
using Hrms.Domain.Entities.Attachments;
using Onepunch.Common.Lib.Exceptions;

namespace Hrms.Core.Services.Attachments;

/// <summary>An attachment as the screens see it. The file is fetched through a separate link request.</summary>
public sealed record AttachmentDto(
    Guid Id, AttachmentOwnerType OwnerType, Guid? OwnerId, string FileName, string ContentType, long SizeBytes, DateTime CreatedAt);

/// <summary>One feature's switch, as shown on the Company Policy > Attachments tab.</summary>
public sealed record AttachmentSettingDto(AttachmentOwnerType OwnerType, bool Enabled);

/// <summary>A file to hand to the browser: a short-lived link, or the content itself when storage can't make links.</summary>
public sealed record AttachmentDownload(Uri? Url, Stream? Content, string FileName, string ContentType);

/// <summary>
/// Files attached to records (leave applications, employee documents, assets, ...), kept in the
/// company's file storage under <c>tenants/{companyId}/attachments/…</c>; the database keeps only
/// the key, name and size. Every feature plugs in through an <see cref="IAttachmentOwnerHandler"/>.
///
/// Every feature accepts new files until the company switches it off (a feature with no saved
/// setting counts as on). Switching it off never deletes what is already there. Every upload is checked against the plan's storage limit first.
///
/// A form whose record isn't saved yet uploads "staged" files (no owner); the record then claims
/// them by id once it exists (<see cref="LinkAsync"/>). Staged files are private to the user who
/// uploaded them.
/// </summary>
public class AttachmentService
{
    private readonly IUnitOfWorkService _uow;
    private readonly IFileStorage _storage;
    private readonly ITenantProvider _tenantProvider;
    private readonly IStorageQuotaGuard _quota;
    private readonly Dictionary<AttachmentOwnerType, IAttachmentOwnerHandler> _handlers;

    public AttachmentService(
        IUnitOfWorkService uow, IFileStorage storage, ITenantProvider tenantProvider,
        IStorageQuotaGuard quota, IEnumerable<IAttachmentOwnerHandler> handlers)
    {
        _uow = uow;
        _storage = storage;
        _tenantProvider = tenantProvider;
        _quota = quota;
        _handlers = handlers.ToDictionary(h => h.OwnerType);
    }

    // ── Settings ─────────────────────────────────────────────────────────────

    /// <summary>One entry per feature that can carry attachments, on or off.</summary>
    public async Task<List<AttachmentSettingDto>> GetSettingsAsync(CancellationToken token)
    {
        var saved = await _uow.Context.AttachmentSettings.AsNoTracking().ToListAsync(token);
        return _handlers.Keys
            .OrderBy(t => t)
            .Select(t => new AttachmentSettingDto(t, saved.All(s => s.OwnerType != t || s.Enabled)))
            .ToList();
    }

    public async Task SaveSettingsAsync(IEnumerable<AttachmentSettingDto> settings, CancellationToken token)
    {
        var saved = await _uow.Context.AttachmentSettings.ToListAsync(token);
        foreach (var setting in settings.Where(s => _handlers.ContainsKey(s.OwnerType)).DistinctBy(s => s.OwnerType))
        {
            var row = saved.FirstOrDefault(s => s.OwnerType == setting.OwnerType);
            if (row == null)
                await _uow.Context.AttachmentSettings.AddAsync(
                    new AttachmentSetting { OwnerType = setting.OwnerType, Enabled = setting.Enabled }, token);
            else
                row.Enabled = setting.Enabled;
        }
        await _uow.CommitChangesAsync("", token);
    }

    public async Task<bool> IsEnabledAsync(AttachmentOwnerType type, CancellationToken token) =>
        await _uow.Context.AttachmentSettings.AsNoTracking().AllAsync(s => s.OwnerType != type || s.Enabled, token);

    // ── Upload / list / open / delete ────────────────────────────────────────

    /// <summary>
    /// Stores a file. With <paramref name="ownerId"/> it is attached to that record at once;
    /// without, it is staged until <see cref="LinkAsync"/>.
    /// </summary>
    public async Task<AttachmentDto> UploadAsync(
        AttachmentOwnerType type, Guid? ownerId, Stream upload, long? length, string? fileName,
        AttachmentCaller caller, CancellationToken token)
    {
        var handler = HandlerFor(type);
        var tenantId = RequireTenant();
        if (!await IsEnabledAsync(type, token))
            throw new GuardException("Attachments are turned off for this feature. Ask an admin to turn them on in Company Policy > Attachments.");

        if (ownerId is { } id)
        {
            await AuthorizeOwnerAsync(handler, id, caller, manage: true, token);
            if (await CountAsync(type, id, token) >= AttachmentFiles.MaxPerRecord)
                throw new InvalidAttachmentException($"A record can have at most {AttachmentFiles.MaxPerRecord} files.");
        }
        else
        {
            if (!CanStage(handler, caller)) throw new NotFoundException("Record not found.");
            var staged = await Staged(type, caller).CountAsync(token);
            if (staged >= AttachmentFiles.MaxPerRecord)
                throw new InvalidAttachmentException($"You can add at most {AttachmentFiles.MaxPerRecord} files.");
        }

        var (content, contentType) = await AttachmentFiles.ReadAsync(upload, length, token);
        await using (content)
        {
            var name = Path.GetFileNameWithoutExtension(fileName ?? "file") + AttachmentFiles.ExtensionFor(contentType);
            var key = StorageKeys.ForNewFile(tenantId, handler.Area, name);
            var size = content.Length;

            // The plan's storage limit: refuse before anything is stored, with a message that says to buy storage.
            await _quota.EnsureCanStoreAsync(tenantId, size, token);
            await _storage.UploadAsync(new FileUpload(key, content, contentType), token);
            _quota.Stored(tenantId, size);

            var row = new Attachment
            {
                OwnerType = type,
                OwnerId = ownerId,
                StorageKey = key,
                FileName = StorageKeys.SafeFileName(name),
                ContentType = contentType,
                SizeBytes = size,
                UploadedBy = caller.UserId,
            };
            try
            {
                await _uow.Context.Attachments.AddAsync(row, token);
                await _uow.CommitChangesAsync("", token);
            }
            catch
            {
                await DeleteFileQuietlyAsync(key, CancellationToken.None);
                throw;
            }
            return ToDto(row);
        }
    }

    public async Task<List<AttachmentDto>> ListAsync(AttachmentOwnerType type, Guid ownerId, AttachmentCaller caller, CancellationToken token)
    {
        await AuthorizeOwnerAsync(HandlerFor(type), ownerId, caller, manage: false, token);
        var rows = await _uow.Context.Attachments.AsNoTracking()
            .Where(a => a.OwnerType == type && a.OwnerId == ownerId)
            .OrderBy(a => a.CreatedAt).ToListAsync(token);
        return rows.Select(ToDto).ToList();
    }

    /// <summary>The caller's own staged files of this type, for a form that was reopened before saving.</summary>
    public async Task<List<AttachmentDto>> ListStagedAsync(AttachmentOwnerType type, AttachmentCaller caller, CancellationToken token)
    {
        if (!CanStage(HandlerFor(type), caller)) return [];
        var rows = await Staged(type, caller).AsNoTracking().OrderBy(a => a.CreatedAt).ToListAsync(token);
        return rows.Select(ToDto).ToList();
    }

    /// <summary>A way to open the file: a short-lived link when storage has them, otherwise the content.</summary>
    public async Task<AttachmentDownload> OpenAsync(Guid id, bool wantContent, AttachmentCaller caller, CancellationToken token)
    {
        var row = await FindReadableAsync(id, caller, token);
        if (!StorageKeys.BelongsTo(row.StorageKey, _tenantProvider.TenantId))
            throw new NotFoundException("File not found.");

        if (!wantContent)
        {
            var url = await _storage.GetDownloadUrlAsync(row.StorageKey, row.FileName, token);
            if (url != null) return new AttachmentDownload(url, null, row.FileName, row.ContentType);
        }

        var content = await _storage.OpenReadAsync(row.StorageKey, token)
            ?? throw new NotFoundException("File not found.");
        return new AttachmentDownload(null, content, row.FileName, row.ContentType);
    }

    public async Task DeleteAsync(Guid id, AttachmentCaller caller, CancellationToken token)
    {
        var row = await _uow.Context.Attachments.FirstOrDefaultAsync(a => a.Id == id, token)
            ?? throw new NotFoundException("File not found.");
        var handler = HandlerFor(row.OwnerType);
        if (row.OwnerId is { } ownerId)
            await AuthorizeOwnerAsync(handler, ownerId, caller, manage: true, token);
        else if (row.UploadedBy != caller.UserId)
            throw new NotFoundException("File not found.");

        _uow.Context.Attachments.Remove(row);
        await _uow.CommitChangesAsync("", token);
        await DeleteFileQuietlyAsync(row.StorageKey, token);
    }

    // ── Staged files claimed by a saved record ───────────────────────────────

    /// <summary>
    /// Attaches the caller's staged files to a record that was just saved. Ids that aren't the
    /// caller's staged files of this type are refused, so nobody can claim someone else's upload.
    /// The caller commits (the record's own save usually does).
    /// </summary>
    public async Task LinkAsync(
        AttachmentOwnerType type, Guid ownerId, IReadOnlyCollection<Guid>? attachmentIds, AttachmentCaller caller, CancellationToken token)
    {
        if (attachmentIds is not { Count: > 0 }) return;
        var ids = attachmentIds.Distinct().ToList();
        var rows = await Staged(type, caller).Where(a => ids.Contains(a.Id)).ToListAsync(token);
        if (rows.Count != ids.Count) throw new GuardException("One of the uploaded files is no longer available. Upload it again.");

        var existing = await CountAsync(type, ownerId, token);
        if (existing + rows.Count > AttachmentFiles.MaxPerRecord)
            throw new InvalidAttachmentException($"A record can have at most {AttachmentFiles.MaxPerRecord} files.");

        foreach (var row in rows) row.OwnerId = ownerId;
    }

    /// <summary>
    /// Claims staged files for a record that already exists and saves. For forms whose record is
    /// created by its own endpoint: they create it, then call this with the new id.
    /// </summary>
    public async Task LinkStagedAsync(
        AttachmentOwnerType type, Guid ownerId, IReadOnlyCollection<Guid> attachmentIds, AttachmentCaller caller, CancellationToken token)
    {
        await AuthorizeOwnerAsync(HandlerFor(type), ownerId, caller, manage: true, token);
        await LinkAsync(type, ownerId, attachmentIds, caller, token);
        await _uow.CommitChangesAsync("", token);
    }

    /// <summary>How many of <paramref name="attachmentIds"/> are the caller's staged files of this type (for "at least one file is required").</summary>
    public async Task<int> CountStagedAsync(
        AttachmentOwnerType type, IReadOnlyCollection<Guid>? attachmentIds, AttachmentCaller caller, CancellationToken token)
    {
        if (attachmentIds is not { Count: > 0 }) return 0;
        var ids = attachmentIds.Distinct().ToList();
        return await Staged(type, caller).CountAsync(a => ids.Contains(a.Id), token);
    }

    /// <summary>Removes every file of a record that is being deleted.</summary>
    public async Task DeleteForOwnerAsync(AttachmentOwnerType type, Guid ownerId, CancellationToken token)
    {
        var rows = await _uow.Context.Attachments.Where(a => a.OwnerType == type && a.OwnerId == ownerId).ToListAsync(token);
        if (rows.Count == 0) return;
        _uow.Context.Attachments.RemoveRange(rows);
        await _uow.CommitChangesAsync("", token);
        foreach (var row in rows) await DeleteFileQuietlyAsync(row.StorageKey, token);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private IAttachmentOwnerHandler HandlerFor(AttachmentOwnerType type) =>
        _handlers.TryGetValue(type, out var handler) ? handler : throw new GuardException("This record type does not support attachments.");

    private Guid RequireTenant()
    {
        var tenantId = _tenantProvider.TenantId;
        return tenantId == Guid.Empty ? throw new GuardException("No company is selected.") : tenantId;
    }

    private static bool CanStage(IAttachmentOwnerHandler handler, AttachmentCaller caller) =>
        caller.UserId != null
        && (caller.HasAnyPermission(handler.ManagePermissions) || (handler.AllowsSelfService && caller.EmployeeId != null));

    private IQueryable<Attachment> Staged(AttachmentOwnerType type, AttachmentCaller caller) =>
        _uow.Context.Attachments.Where(a => a.OwnerType == type && a.OwnerId == null && a.UploadedBy == caller.UserId);

    private Task<int> CountAsync(AttachmentOwnerType type, Guid ownerId, CancellationToken token) =>
        _uow.Context.Attachments.CountAsync(a => a.OwnerType == type && a.OwnerId == ownerId, token);

    // The record must exist and be visible to the caller (a hidden confidential employee's record is
    // "not found" here too), the caller must be allowed to see or change it, and a locked record
    // takes no new files. "Not found" for a forbidden record too, so ids can't be probed.
    private static async Task AuthorizeOwnerAsync(
        IAttachmentOwnerHandler handler, Guid ownerId, AttachmentCaller caller, bool manage, CancellationToken token)
    {
        var info = await handler.FindAsync(ownerId, token) ?? throw new NotFoundException("Record not found.");
        var allowed = caller.HasAnyPermission(manage ? handler.ManagePermissions : handler.ViewPermissions)
            || (handler.AllowsSelfService && caller.EmployeeId != null && info.EmployeeId == caller.EmployeeId);
        if (!allowed) throw new NotFoundException("Record not found.");
        if (manage && info.Locked)
            throw new GuardException("This record can no longer be changed, so its files can't be added or removed.");
    }

    private async Task<Attachment> FindReadableAsync(Guid id, AttachmentCaller caller, CancellationToken token)
    {
        var row = await _uow.Context.Attachments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, token)
            ?? throw new NotFoundException("File not found.");
        if (row.OwnerId is { } ownerId)
            await AuthorizeOwnerAsync(HandlerFor(row.OwnerType), ownerId, caller, manage: false, token);
        else if (row.UploadedBy != caller.UserId)
            throw new NotFoundException("File not found.");
        return row;
    }

    private async Task DeleteFileQuietlyAsync(string key, CancellationToken token)
    {
        if (!StorageKeys.BelongsTo(key, _tenantProvider.TenantId)) return;
        try
        {
            await _storage.DeleteAsync(key, token);
            _quota.Removed(_tenantProvider.TenantId);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Couldn't delete attachment file {Key}", key);
        }
    }

    private static AttachmentDto ToDto(Attachment a) =>
        new(a.Id, a.OwnerType, a.OwnerId, a.FileName, a.ContentType, a.SizeBytes, a.CreatedAt);
}
