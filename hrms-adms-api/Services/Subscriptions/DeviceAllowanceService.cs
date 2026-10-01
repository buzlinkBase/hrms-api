using Onepunch.Common.Lib.Subscriptions;

namespace Hrms.adms.Services.Subscriptions;

/// <summary>
/// The plan's biometric entitlements for devices: the company's plan must include
/// timekeeping.biometric, and its active devices stay within biometric_devices.max. Only
/// registering or re-activating a device is checked -- a device already active keeps pushing
/// punches whatever the plan, so no attendance is ever lost to a downgrade.
/// </summary>
public class DeviceAllowanceService
{
    public const string ActiveStatus = "Active";

    private readonly IUnitOfWorkService _uow;
    private readonly IEntitlementProvider _entitlements;

    public DeviceAllowanceService(IUnitOfWorkService uow, IEntitlementProvider entitlements)
    {
        _uow = uow;
        _entitlements = entitlements;
    }

    /// <summary>Before a device becomes Active: a new one, or an existing one switched back on.</summary>
    public async Task EnsureCanActivateAsync(Guid tenantId, Guid? existingDeviceId, string? newStatus, CancellationToken token)
    {
        if (!IsActive(newStatus)) return;
        if (existingDeviceId is { } id)
        {
            var currentStatus = await _uow.Context.BiometricDevices
                .Where(d => d.Id == id && d.TenantId == tenantId)
                .Select(d => d.Status)
                .FirstOrDefaultAsync(token);
            if (IsActive(currentStatus)) return;
        }

        var snapshot = await _entitlements.GetAsync(tenantId, token);
        if (snapshot.IsReadOnly) throw PlanRestrictionException.ReadOnly();
        if (!snapshot.HasFeature(EntitlementKeys.TimekeepingBiometric))
            throw PlanRestrictionException.FeatureRequired(EntitlementKeys.TimekeepingBiometric,
                "Your plan doesn't include biometric devices.");
        if (snapshot.LimitOf(EntitlementKeys.BiometricDevicesMax) is not { } max) return;

        var used = (await CountActiveAsync([tenantId], token)).GetValueOrDefault(tenantId);
        if (used + 1 > max)
            throw PlanRestrictionException.LimitReached(EntitlementKeys.BiometricDevicesMax, max, used,
                $"Your plan allows {max} active biometric device{(max == 1 ? "" : "s")} per company. Upgrade, or set a device you no longer use to Inactive.");
    }

    /// <summary>Active, non-deleted devices per company -- across companies, so past the per-company filter.</summary>
    public Task<Dictionary<Guid, int>> CountActiveAsync(IReadOnlyCollection<Guid> tenantIds, CancellationToken token) =>
        _uow.Context.BiometricDevices
            .IgnoreQueryFilters()
            .Where(d => tenantIds.Contains(d.TenantId) && d.DeletedAt == null && d.Status == ActiveStatus)
            .GroupBy(d => d.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, token);

    private static bool IsActive(string? status) => string.Equals(status, ActiveStatus, StringComparison.OrdinalIgnoreCase);
}
