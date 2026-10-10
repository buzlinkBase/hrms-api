using Hrms.Infrastructure;
using Onepunch.Common.Lib.Subscriptions;

namespace Hrms.Core.Services.Subscriptions;

/// <summary>Counts how much of one account-wide limit the account's companies use together.</summary>
public interface ILimitUsageCounter
{
    string LimitKey { get; }
    Task<int> CountAsync(IReadOnlyCollection<Guid> tenantIds, CancellationToken token);
}

/// <summary>
/// employees.max: every employee of the account's companies not separated (terminated, resigned,
/// retired, deceased) -- confidential ones included, so restricted callers can't slip past the
/// limit unseen. Spans companies, so it reads past the per-company query filters and applies the
/// soft-delete rule itself.
/// </summary>
public sealed class ActiveEmployeeCounter(IUnitOfWorkService uow) : ILimitUsageCounter
{
    public string LimitKey => EntitlementKeys.EmployeesMax;

    public Task<int> CountAsync(IReadOnlyCollection<Guid> tenantIds, CancellationToken token) =>
        uow.Context.Employees.IgnoreQueryFilters()
            .CountAsync(e => tenantIds.Contains(e.TenantId)
                && e.DeletedAt == null
                && !EmployeeService.SeparatedStatuses.Contains(e.EmploymentStatus), token);
}

/// <summary>Refuses adding past a plan limit (throws <see cref="PlanRestrictionException"/>).</summary>
public interface IPlanLimitGuard
{
    Task EnsureCanAddAsync(string limitKey, int adding, CancellationToken token);
}

/// <summary>
/// Strategy: one <see cref="ILimitUsageCounter"/> per limit key; this looks up the account's
/// maximum and asks the matching counter how many all its companies use together. A key with no
/// counter isn't enforced here (it's enforced by the service that owns that data).
/// </summary>
public sealed class PlanLimitGuard(
    IEntitlementProvider entitlements,
    ITenantProvider tenantProvider,
    IEnumerable<ILimitUsageCounter> counters) : IPlanLimitGuard
{
    public async Task EnsureCanAddAsync(string limitKey, int adding, CancellationToken token)
    {
        if (adding <= 0) return;

        var snapshot = await entitlements.GetAsync(tenantProvider.TenantId, token);
        if (snapshot.IsReadOnly) throw PlanRestrictionException.ReadOnly();
        if (snapshot.LimitOf(limitKey) is not { } max) return;

        var counter = counters.FirstOrDefault(c => c.LimitKey == limitKey);
        if (counter == null) return;

        // Every company of the account shares the limit; this one is counted even if the snapshot predates it.
        var companyId = tenantProvider.TenantId;
        IReadOnlyCollection<Guid> pool = snapshot.AccountTenantIds.Contains(companyId)
            ? snapshot.AccountTenantIds
            : [.. snapshot.AccountTenantIds, companyId];

        var used = await counter.CountAsync(pool, token);
        if (used + adding <= max) return;

        throw PlanRestrictionException.LimitReached(limitKey, max, used, Message(limitKey, max, used, adding));
    }

    private static string Message(string limitKey, int max, int used, int adding) => limitKey switch
    {
        EntitlementKeys.EmployeesMax => adding == 1
            ? $"Your plan allows up to {max} active employees across all your companies, and you have {used}. Upgrade your plan, or separate employees who have left, to add more."
            : $"Adding {adding} employees would bring your companies to {used + adding}, over your plan's {max}. Upgrade your plan, or import fewer.",
        _ => $"Your plan's limit is reached ({used} of {max}).",
    };
}
