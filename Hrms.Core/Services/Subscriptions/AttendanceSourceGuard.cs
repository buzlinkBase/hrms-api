using Onepunch.Common.Lib.Subscriptions;

namespace Hrms.Core.Services.Subscriptions;

/// <summary>
/// Refuses attendance from a source the company's plan doesn't include (throws
/// <see cref="PlanRestrictionException"/>, a 403 <c>plan-required</c>). Asked by
/// <see cref="AttendanceService.AddRangeAsync"/>, which every attendance log passes through,
/// so whatever endpoint accepts mobile punches is gated without having to remember to be.
/// </summary>
public interface IAttendanceSourceGuard
{
    Task EnsureAllowedAsync(IEnumerable<LOGSOURCE> sources, CancellationToken token);
}

/// <summary>
/// Strategy table: which plan feature a log source needs. A source with no entry (manual entry,
/// file upload, biometric devices) is never gated here and costs no entitlement lookup; gating
/// another source is one more row.
/// </summary>
public sealed class AttendanceSourceGuard(IEntitlementProvider entitlements, ITenantProvider tenantProvider) : IAttendanceSourceGuard
{
    private static readonly IReadOnlyDictionary<LOGSOURCE, string> RequiredFeature = new Dictionary<LOGSOURCE, string>
    {
        [LOGSOURCE.MOBILE] = EntitlementKeys.TimekeepingMobile,
        [LOGSOURCE.GEOFENCE] = EntitlementKeys.TimekeepingMobile,
    };

    private const string MobileMessage =
        "Your plan doesn't include Mobile Attendance (Geofencing). Upgrade your plan to accept attendance from the mobile app.";

    public async Task EnsureAllowedAsync(IEnumerable<LOGSOURCE> sources, CancellationToken token)
    {
        var needed = sources
            .Distinct()
            .Select(source => RequiredFeature.GetValueOrDefault(source))
            .OfType<string>()
            .Distinct()
            .ToList();
        if (needed.Count == 0) return;

        var snapshot = await entitlements.GetAsync(tenantProvider.TenantId, token);
        foreach (var feature in needed)
        {
            if (!snapshot.HasFeature(feature))
                throw PlanRestrictionException.FeatureRequired(feature, MobileMessage);
        }
    }
}
