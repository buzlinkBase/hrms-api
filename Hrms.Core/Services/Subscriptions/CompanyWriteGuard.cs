using Onepunch.Common.Lib.Subscriptions;

namespace Hrms.Core.Services.Subscriptions;

/// <summary>
/// Refuses a change to a company whose subscription is read-only (403 <c>subscription-read-only</c>)
/// or stopped by an operator (403 <c>subscription-locked</c>). SubscriptionAccessMiddleware already
/// refuses write requests at the HTTP edge; this is the same rule one layer down, asked by the
/// services that write attendance, DTR and payroll runs, so queued work (SaveDtrDraftWorker,
/// CreateAttendanceWorker) and any endpoint wrongly marked [AllowWhenReadOnly] are refused too.
/// </summary>
public interface ICompanyWriteGuard
{
    Task EnsureWritableAsync(CancellationToken token);

    /// <summary>
    /// Device punches (synced / biometric) follow the punch-retention rule instead -- they keep being
    /// stored for a while after a lapse (EntitlementSnapshot.AcceptsPunches), the same rule
    /// hrms-adms-api's PunchAcceptanceService applies, so no attendance is lost to a short lapse.
    /// Any other source (manual, upload, mobile, portal) is a change like any other.
    /// </summary>
    Task EnsureCanStoreAttendanceAsync(IEnumerable<LOGSOURCE> sources, CancellationToken token);
}

public sealed class CompanyWriteGuard(
    IEntitlementProvider entitlements,
    ITenantProvider tenantProvider,
    TimeProvider? clock = null) : ICompanyWriteGuard
{
    private static readonly HashSet<LOGSOURCE> DeviceSources = [LOGSOURCE.SYNC, LOGSOURCE.BIOMETRIC];

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task EnsureWritableAsync(CancellationToken token)
    {
        if (await SnapshotAsync(token) is not { } snapshot) return;

        // Decided as for the owner: who's asking doesn't matter here, only the company's state.
        var decision = SubscriptionAccessPolicy.Decide(snapshot, "POST", isBillingUser: true);
        if (SubscriptionAccessPolicy.Refusal(decision) is { } refusal) throw refusal;
    }

    public async Task EnsureCanStoreAttendanceAsync(IEnumerable<LOGSOURCE> sources, CancellationToken token)
    {
        var distinct = sources.Distinct().ToList();
        if (distinct.Count == 0) return;
        if (!distinct.All(DeviceSources.Contains))
        {
            await EnsureWritableAsync(token);
            return;
        }

        if (await SnapshotAsync(token) is not { } snapshot) return;
        if (!snapshot.AcceptsPunches(_clock.GetUtcNow().UtcDateTime))
            throw PlanRestrictionException.ReadOnly(
                "Your subscription is read-only and the period for storing device punches has passed. Renew in Billing to continue.");
    }

    // No company (a system-level call): not a company's change, same as the middleware.
    private async Task<EntitlementSnapshot?> SnapshotAsync(CancellationToken token)
    {
        var tenantId = tenantProvider.TenantId;
        return tenantId == Guid.Empty ? null : await entitlements.GetAsync(tenantId, token);
    }
}
