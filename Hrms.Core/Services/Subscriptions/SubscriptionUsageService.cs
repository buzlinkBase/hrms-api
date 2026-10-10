using Hrms.Core.Services.Storage;
using Onepunch.Common.Lib.Subscriptions;

namespace Hrms.Core.Services.Subscriptions;

/// <summary>
/// What Tenant Service asks hrms-api when it checks a downgrade or recommends a plan: per company,
/// active employees (the employees.max limit) and how much each paid feature has actually been
/// used. Spans companies, so it reads past the per-company query filters and applies the
/// soft-delete rule itself.
/// </summary>
public class SubscriptionUsageService
{
    private readonly IUnitOfWorkService _uow;
    private readonly IStorageUsageMeter? _storage;

    public SubscriptionUsageService(IUnitOfWorkService uow, IStorageUsageMeter? storage = null)
    {
        _uow = uow;
        _storage = storage;
    }

    public async Task<List<CompanyUsage>> GetUsageAsync(IReadOnlyCollection<Guid> tenantIds, CancellationToken token)
    {
        if (tenantIds.Count == 0) return [];
        var db = _uow.Context;
        var separated = EmployeeService.SeparatedStatuses;

        var counts = new Dictionary<string, Dictionary<Guid, int>>
        {
            [EntitlementKeys.EmployeesMax] = await CountAsync(db.Employees.IgnoreQueryFilters()
                .Where(e => tenantIds.Contains(e.TenantId) && e.DeletedAt == null && !separated.Contains(e.EmploymentStatus))
                .Select(e => e.TenantId), token),
            [EntitlementKeys.PayrollCore] = await CountAsync(db.PayrollBatches.IgnoreQueryFilters()
                .Where(b => tenantIds.Contains(b.TenantId) && b.DeletedAt == null)
                .Select(b => b.TenantId), token),
            [EntitlementKeys.PayrollLoansIncome] = await CountAsync(db.DeductionApplications.IgnoreQueryFilters()
                .Where(a => tenantIds.Contains(a.TenantId) && a.DeletedAt == null)
                .Select(a => a.TenantId), token),
            [EntitlementKeys.ApprovalsMultilevel] = await CountAsync(db.ApprovalWorkflows.IgnoreQueryFilters()
                .Where(w => tenantIds.Contains(w.TenantId) && w.DeletedAt == null && w.IsActive && w.Steps.Count > 1)
                .Select(w => w.TenantId), token),
            [EntitlementKeys.Announcements] = await CountAsync(db.Announcements.IgnoreQueryFilters()
                .Where(a => tenantIds.Contains(a.TenantId) && a.DeletedAt == null)
                .Select(a => a.TenantId), token),
            [EntitlementKeys.ClientsAgency] = await CountAsync(db.Clients.IgnoreQueryFilters()
                .Where(c => tenantIds.Contains(c.TenantId) && c.DeletedAt == null)
                .Select(c => c.TenantId), token),
        };

        // Storage is in whole megabytes (storage.max_mb), reported per company; Tenant Service adds a
        // whole account's companies together against its one limit. If it can't be measured the key is
        // left out, so it shows as unknown rather than as zero.
        var storageMb = await TryMeasureStorageAsync(tenantIds, token);

        return tenantIds
            .Select(tenantId =>
            {
                var used = counts.ToDictionary(kv => kv.Key, kv => kv.Value.GetValueOrDefault(tenantId));
                if (storageMb != null) used[EntitlementKeys.StorageMaxMb] = storageMb.GetValueOrDefault(tenantId);
                return new CompanyUsage { TenantId = tenantId, Used = used };
            })
            .ToList();
    }

    private async Task<Dictionary<Guid, int>?> TryMeasureStorageAsync(IReadOnlyCollection<Guid> tenantIds, CancellationToken token)
    {
        if (_storage == null) return null;
        try
        {
            return await _storage.GetMegabytesAsync(tenantIds, token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Warning(ex, "Couldn't measure file storage for {Count} company(ies)", tenantIds.Count);
            return null;
        }
    }

    private static Task<Dictionary<Guid, int>> CountAsync(IQueryable<Guid> tenantIdsPerRow, CancellationToken token) =>
        tenantIdsPerRow
            .GroupBy(tenantId => tenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, token);
}
