using Hrms.Core.Services.Attachments;
using Hrms.Core.Services.Storage;
using Hrms.Domain.Entities.Attachments;

namespace Hrms.Core.Services.StorageManager;

/// <summary>The record a stored file belongs to, as the Storage Manager shows it.</summary>
/// <param name="RecordLabel">E.g. "Ana Reyes (E-001)"; null when the record names nobody.</param>
/// <param name="Locked">True when the record is already decided -- deleting its file needs an explicit confirmation.</param>
/// <param name="AttachmentId">The attachment's id, so the screen can open the file; null for images.</param>
public sealed record StoredFileOwner(string? RecordLabel, bool Locked, Guid? AttachmentId = null);

/// <summary>
/// One kind of stored file (a feature's attachments, employee photos, logos, ...). The Storage
/// Manager knows nothing about leave, employees or clients: it asks the categories in order, and the
/// first one whose <see cref="Handles"/> accepts a file's area owns it (chain of responsibility; the
/// <see cref="UnlinkedFileCategory"/> at the end accepts everything). A new kind of file is one new
/// category -- the service, controller and screens stay as they are.
///
/// Reads go past the per-company query filters on purpose and filter by company themselves: the
/// screen must see every record that points at a file (a confidential employee's photo included),
/// or a file still in use would look unlinked.
/// </summary>
public interface IStoredFileCategory
{
    /// <summary>Stable id the screens send back, e.g. "employee-photos".</summary>
    string Key { get; }

    string Label { get; }

    /// <summary>Whether files stored under <paramref name="area"/> are this category's.</summary>
    bool Handles(string area);

    /// <summary>Which of <paramref name="keys"/> a record still points at.</summary>
    Task<HashSet<string>> FindReferencedAsync(Guid tenantId, IReadOnlyCollection<string> keys, CancellationToken token);

    /// <summary>The record behind each referenced key (keys no record points at are left out).</summary>
    Task<Dictionary<string, StoredFileOwner>> DescribeAsync(Guid tenantId, IReadOnlyCollection<string> keys, CancellationToken token);

    /// <summary>Stops the records pointing at <paramref name="keys"/>; the caller commits, then deletes the files.</summary>
    Task ReleaseAsync(Guid tenantId, IReadOnlyCollection<string> keys, CancellationToken token);
}

/// <summary>The files of one feature's attachments, e.g. leave application attachments.</summary>
public sealed class AttachmentFileCategory(IUnitOfWorkService uow, IAttachmentOwnerHandler handler) : IStoredFileCategory
{
    public string Key => handler.Area;

    public string Label => LabelOf(handler.OwnerType);

    public bool Handles(string area) => area == handler.Area;

    private IQueryable<Attachment> Rows(Guid tenantId, IReadOnlyCollection<string> keys) =>
        uow.Context.Attachments.IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId && a.OwnerType == handler.OwnerType && keys.Contains(a.StorageKey));

    public async Task<HashSet<string>> FindReferencedAsync(Guid tenantId, IReadOnlyCollection<string> keys, CancellationToken token) =>
        (await Rows(tenantId, keys).AsNoTracking().Select(a => a.StorageKey).ToListAsync(token)).ToHashSet();

    public async Task<Dictionary<string, StoredFileOwner>> DescribeAsync(Guid tenantId, IReadOnlyCollection<string> keys, CancellationToken token)
    {
        var rows = await Rows(tenantId, keys).AsNoTracking()
            .Select(a => new { a.Id, a.StorageKey, a.OwnerId })
            .ToListAsync(token);

        var owners = new Dictionary<Guid, AttachmentOwnerInfo?>();
        foreach (var ownerId in rows.Where(r => r.OwnerId != null).Select(r => r.OwnerId!.Value).Distinct())
            owners[ownerId] = await handler.FindAsync(ownerId, token);

        var employeeIds = owners.Values.Where(o => o?.EmployeeId != null).Select(o => o!.EmployeeId!.Value).Distinct().ToList();
        var names = await EmployeeNames.LoadAsync(uow, tenantId, employeeIds, token);

        var result = new Dictionary<string, StoredFileOwner>();
        foreach (var row in rows)
        {
            if (row.OwnerId is not { } ownerId)
            {
                result[row.StorageKey] = new StoredFileOwner("Not attached to a saved record yet", false, row.Id);
                continue;
            }
            // A record the handler can't see (e.g. hidden by the confidential filter) counts as
            // locked, so its file is never removed without the explicit confirmation.
            var owner = owners.GetValueOrDefault(ownerId);
            var label = owner?.EmployeeId is { } employeeId ? names.GetValueOrDefault(employeeId) : null;
            result[row.StorageKey] = new StoredFileOwner(label, owner?.Locked ?? true, row.Id);
        }
        return result;
    }

    public async Task ReleaseAsync(Guid tenantId, IReadOnlyCollection<string> keys, CancellationToken token) =>
        uow.Context.Attachments.RemoveRange(await Rows(tenantId, keys).ToListAsync(token));

    public static string LabelOf(AttachmentOwnerType type) => type switch
    {
        AttachmentOwnerType.EmployeeRecord => "Employee documents",
        AttachmentOwnerType.AssignAsset => "Employee asset photos",
        AttachmentOwnerType.LeaveApplication => "Leave attachments",
        AttachmentOwnerType.ProfileUpdateRequest => "Profile update attachments",
        AttachmentOwnerType.TravelOrder => "Official business attachments",
        AttachmentOwnerType.PassSlip => "Pass slip attachments",
        AttachmentOwnerType.UnderTime => "Undertime attachments",
        AttachmentOwnerType.OverTime => "Overtime attachments",
        AttachmentOwnerType.Loan => "Loan attachments",
        AttachmentOwnerType.CashBond => "Cash bond attachments",
        AttachmentOwnerType.OtherIncome => "Other income attachments",
        _ => $"{type} attachments",
    };
}

/// <summary>Employee profile photos (<c>Employee.ProfileImg</c>). Removing one leaves the employee without a photo.</summary>
public sealed class EmployeePhotoCategory(IUnitOfWorkService uow) : IStoredFileCategory
{
    public string Key => OrganizationImageService.PhotoArea;

    public string Label => "Employee photos";

    public bool Handles(string area) => area == OrganizationImageService.PhotoArea;

    private IQueryable<Hrms.Domain.Entities.EmployeeEntities.Employee> Rows(Guid tenantId, IReadOnlyCollection<string> keys) =>
        uow.Context.Employees.IgnoreQueryFilters().Where(e => e.TenantId == tenantId && keys.Contains(e.ProfileImg));

    public async Task<HashSet<string>> FindReferencedAsync(Guid tenantId, IReadOnlyCollection<string> keys, CancellationToken token) =>
        (await Rows(tenantId, keys).AsNoTracking().Select(e => e.ProfileImg).ToListAsync(token)).ToHashSet();

    public async Task<Dictionary<string, StoredFileOwner>> DescribeAsync(Guid tenantId, IReadOnlyCollection<string> keys, CancellationToken token)
    {
        var rows = await Rows(tenantId, keys).AsNoTracking()
            .Select(e => new { e.ProfileImg, e.FirstName, e.LastName, e.EmployeeNo, e.EmploymentStatus, e.DeletedAt })
            .ToListAsync(token);
        var result = new Dictionary<string, StoredFileOwner>();
        foreach (var e in rows)
        {
            var status = e.DeletedAt != null ? "deleted" : e.EmploymentStatus.ToString();
            result[e.ProfileImg] = new StoredFileOwner($"{EmployeeNames.Format(e.FirstName, e.LastName, e.EmployeeNo)} · {status}", false);
        }
        return result;
    }

    public async Task ReleaseAsync(Guid tenantId, IReadOnlyCollection<string> keys, CancellationToken token)
    {
        foreach (var employee in await Rows(tenantId, keys).ToListAsync(token))
            employee.ProfileImg = string.Empty;
    }
}

/// <summary>The company's own logo (<c>Company.LogoKey</c>).</summary>
public sealed class CompanyLogoCategory(IUnitOfWorkService uow) : IStoredFileCategory
{
    public string Key => OrganizationImageService.LogoArea;

    public string Label => "Company logo";

    public bool Handles(string area) => area == OrganizationImageService.LogoArea;

    private IQueryable<Hrms.Domain.Entities.Company> Rows(Guid tenantId, IReadOnlyCollection<string> keys) =>
        uow.Context.Companies.IgnoreQueryFilters().Where(c => c.TenantId == tenantId && c.LogoKey != null && keys.Contains(c.LogoKey));

    public async Task<HashSet<string>> FindReferencedAsync(Guid tenantId, IReadOnlyCollection<string> keys, CancellationToken token) =>
        (await Rows(tenantId, keys).AsNoTracking().Select(c => c.LogoKey!).ToListAsync(token)).ToHashSet();

    public async Task<Dictionary<string, StoredFileOwner>> DescribeAsync(Guid tenantId, IReadOnlyCollection<string> keys, CancellationToken token) =>
        (await FindReferencedAsync(tenantId, keys, token)).ToDictionary(k => k, _ => new StoredFileOwner("Current company logo", false));

    public async Task ReleaseAsync(Guid tenantId, IReadOnlyCollection<string> keys, CancellationToken token)
    {
        foreach (var company in await Rows(tenantId, keys).ToListAsync(token))
            company.LogoKey = null;
    }
}

/// <summary>Client logos (<c>Client.LogoKey</c>).</summary>
public sealed class ClientLogoCategory(IUnitOfWorkService uow) : IStoredFileCategory
{
    public string Key => OrganizationImageService.ClientLogoArea;

    public string Label => "Client logos";

    public bool Handles(string area) => area == OrganizationImageService.ClientLogoArea;

    private IQueryable<Hrms.Domain.Entities.Client> Rows(Guid tenantId, IReadOnlyCollection<string> keys) =>
        uow.Context.Clients.IgnoreQueryFilters().Where(c => c.TenantId == tenantId && c.LogoKey != null && keys.Contains(c.LogoKey));

    public async Task<HashSet<string>> FindReferencedAsync(Guid tenantId, IReadOnlyCollection<string> keys, CancellationToken token) =>
        (await Rows(tenantId, keys).AsNoTracking().Select(c => c.LogoKey!).ToListAsync(token)).ToHashSet();

    public async Task<Dictionary<string, StoredFileOwner>> DescribeAsync(Guid tenantId, IReadOnlyCollection<string> keys, CancellationToken token)
    {
        var rows = await Rows(tenantId, keys).AsNoTracking().Select(c => new { c.LogoKey, c.Name, c.DeletedAt }).ToListAsync(token);
        return rows.ToDictionary(c => c.LogoKey!, c => new StoredFileOwner(c.DeletedAt != null ? $"{c.Name} · deleted" : c.Name, false));
    }

    public async Task ReleaseAsync(Guid tenantId, IReadOnlyCollection<string> keys, CancellationToken token)
    {
        foreach (var client in await Rows(tenantId, keys).ToListAsync(token))
            client.LogoKey = null;
    }
}

/// <summary>
/// The end of the chain: files no record points at any more -- an upload whose form was never
/// saved, an image that was replaced but whose old file couldn't be deleted, a folder no feature
/// uses. Deleting these touches no record. The service only puts a file here once it is older than
/// <see cref="StorageManagerService.UnlinkedGracePeriod"/>, so an upload still in progress is never offered.
/// </summary>
public sealed class UnlinkedFileCategory : IStoredFileCategory
{
    public const string CategoryKey = "unlinked";

    public string Key => CategoryKey;

    public string Label => "Unused files";

    public bool Handles(string area) => true;

    public Task<HashSet<string>> FindReferencedAsync(Guid tenantId, IReadOnlyCollection<string> keys, CancellationToken token) =>
        Task.FromResult(new HashSet<string>());

    public Task<Dictionary<string, StoredFileOwner>> DescribeAsync(Guid tenantId, IReadOnlyCollection<string> keys, CancellationToken token) =>
        Task.FromResult(new Dictionary<string, StoredFileOwner>());

    public Task ReleaseAsync(Guid tenantId, IReadOnlyCollection<string> keys, CancellationToken token) => Task.CompletedTask;
}

/// <summary>Employee names for record labels, read past the company filters like the categories are.</summary>
internal static class EmployeeNames
{
    public static string Format(string firstName, string lastName, string employeeNo)
    {
        var name = $"{firstName} {lastName}".Trim();
        return string.IsNullOrWhiteSpace(employeeNo) ? name : $"{name} ({employeeNo})";
    }

    public static async Task<Dictionary<Guid, string>> LoadAsync(
        IUnitOfWorkService uow, Guid tenantId, IReadOnlyCollection<Guid> employeeIds, CancellationToken token)
    {
        if (employeeIds.Count == 0) return [];
        var rows = await uow.Context.Employees.IgnoreQueryFilters().AsNoTracking()
            .Where(e => e.TenantId == tenantId && employeeIds.Contains(e.Id))
            .Select(e => new { e.Id, e.FirstName, e.LastName, e.EmployeeNo })
            .ToListAsync(token);
        return rows.ToDictionary(e => e.Id, e => Format(e.FirstName, e.LastName, e.EmployeeNo));
    }
}
