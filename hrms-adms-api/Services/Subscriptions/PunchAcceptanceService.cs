using System.Collections.Concurrent;
using Onepunch.Common.Lib.Subscriptions;

namespace Hrms.adms.Services.Subscriptions;

/// <summary>
/// Whether a device's attendance punches are still stored for its company. A company whose owner's
/// plan lapsed keeps storing them for a while (Subscriptions:LockedPunchRetentionDays after the
/// lock, never past the account's purge date or the company's deletion -- see
/// EntitlementSnapshot.PunchesAcceptedUntilUtc), so no attendance is lost to a short lapse. Past
/// that, uploads are refused and the device keeps the logs in its own memory until the company
/// is paid up again.
/// </summary>
public class PunchAcceptanceService
{
    // One warning per device per day, not one per upload (devices retry every few seconds).
    private static readonly ConcurrentDictionary<string, byte> Warned = new();

    private readonly IEntitlementProvider _entitlements;
    private readonly TimeProvider _clock;

    public PunchAcceptanceService(IEntitlementProvider entitlements, TimeProvider clock)
    {
        _entitlements = entitlements;
        _clock = clock;
    }

    /// <summary>
    /// Whether the company's plan includes the HRMS system. Without it nothing a device pushes is
    /// kept: the controller acknowledges the upload so the device clears its memory and stops
    /// retrying, but nothing is stored or processed.
    /// </summary>
    public async Task<bool> HasHrmsAsync(Guid tenantId, CancellationToken token) =>
        (await _entitlements.GetAsync(tenantId, token)).HasFeature(EntitlementKeys.HrmsCore);

    public async Task<bool> AcceptsPunchesAsync(Guid tenantId, string serialNumber, CancellationToken token)
    {
        var snapshot = await _entitlements.GetAsync(tenantId, token);
        var now = _clock.GetUtcNow().UtcDateTime;
        if (snapshot.AcceptsPunches(now)) return true;

        if (Warned.TryAdd($"{serialNumber}:{now:yyyyMMdd}", 0))
        {
            Log.Logger.Warning(
                "Device {SN} (company {TenantId}): punches refused -- subscription {Status}, stored only until {Until:u}",
                serialNumber, tenantId, snapshot.Status, snapshot.PunchesAcceptedUntilUtc);
        }
        return false;
    }
}
