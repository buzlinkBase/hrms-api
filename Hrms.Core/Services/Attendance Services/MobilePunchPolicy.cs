using DTR.Core;
using Hrms.Domain;
using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;
using NetTopologySuite.Geometries;
using Onepunch.Common.Lib.Exceptions;

namespace Hrms.Core.Services;

public enum PunchLocationKind { None, Branch, Site }

/// <summary>
/// The punch rule that applies to an employee: the mode, and where it came from (its boundary for
/// the geofence, and the location the QR / NFC tag must belong to).
/// </summary>
public sealed record ResolvedPunchMode(MobilePunchMode Mode, PunchLocationKind Kind, Guid LocationId, Polygon? Boundary)
{
    public static readonly ResolvedPunchMode Open = new(MobilePunchMode.OPEN, PunchLocationKind.None, Guid.Empty, null);

    /// <summary>What the location's QR code / NFC tag must contain.</summary>
    public string ExpectedTag => MobilePunchPolicy.TagFor(Kind, LocationId);
}

/// <summary>What the mobile app sends along with a punch. Each part is only read when the mode asks for it.</summary>
public sealed record MobilePunchEvidence(double? Latitude, double? Longitude, string? QrCode, string? NfcTag);

/// <summary>
/// Which of the ways to prove "I am at the site" a mode demands. A mode is the set of factors that
/// must ALL pass -- GEOFENCE_QR means inside the boundary AND the right QR; OPEN demands none.
/// </summary>
public static class MobilePunchPolicy
{
    public static bool RequiresGeofence(MobilePunchMode mode) =>
        mode is MobilePunchMode.GEOFENCE or MobilePunchMode.GEOFENCE_QR or MobilePunchMode.GEOFENCE_NFC;

    public static bool RequiresQr(MobilePunchMode mode) =>
        mode is MobilePunchMode.QR or MobilePunchMode.GEOFENCE_QR;

    public static bool RequiresNfc(MobilePunchMode mode) =>
        mode is MobilePunchMode.NFC or MobilePunchMode.GEOFENCE_NFC;

    /// <summary>The text printed on a location's QR code / written to its NFC tag.</summary>
    public static string TagFor(PunchLocationKind kind, Guid locationId) =>
        $"onepunch:{kind.ToString().ToLowerInvariant()}:{locationId:D}";

    /// <summary>A project site's own mode wins; a site set to "follow branch" (null) uses its branch's.</summary>
    public static ResolvedPunchMode Resolve(Branch? branch, CostCenters? site)
    {
        if (site?.PunchMode is { } siteMode)
            return new(siteMode, PunchLocationKind.Site, site.Id, site.Boundary);
        if (branch != null)
            return new(branch.PunchMode, PunchLocationKind.Branch, branch.Id, branch.Boundary);
        return ResolvedPunchMode.Open;
    }

    /// <summary>Null when the punch is allowed; otherwise the reason it is not, in words for the employee.</summary>
    public static string? Check(ResolvedPunchMode rule, MobilePunchEvidence evidence)
    {
        var mode = rule.Mode;
        if (RequiresGeofence(mode) && CheckGeofence(rule, evidence) is { } geofenceError) return geofenceError;
        if (RequiresQr(mode) && !TagMatches(rule, evidence.QrCode)) return "Scan this location's QR code to punch.";
        if (RequiresNfc(mode) && !TagMatches(rule, evidence.NfcTag)) return "Tap this location's NFC tag to punch.";
        return null;
    }

    private static string? CheckGeofence(ResolvedPunchMode rule, MobilePunchEvidence evidence)
    {
        // Fail closed: a company that asked for a geofence but never drew one can't be punched into.
        if (rule.Boundary == null) return "This location has no boundary set, so punching here is blocked. Please contact HR.";
        if (evidence.Latitude is not { } lat || evidence.Longitude is not { } lng
            || lat is < -90 or > 90 || lng is < -180 or > 180)
            return "Your location is needed to punch here. Turn on location services and try again.";

        // GeoJSON order: x = longitude, y = latitude.
        return rule.Boundary.Covers(new Point(lng, lat))
            ? null
            : "You are outside the allowed area for this location.";
    }

    private static bool TagMatches(ResolvedPunchMode rule, string? scanned) =>
        !string.IsNullOrWhiteSpace(scanned)
        && string.Equals(scanned.Trim(), rule.ExpectedTag, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Reads the employee's branch / project site and applies <see cref="MobilePunchPolicy"/>. The
/// mobile punch endpoint calls <see cref="EnsureAllowedAsync"/> before it records the log; the app
/// calls <see cref="ResolveAsync"/> to know which controls to show (map, scanner, NFC prompt, or just a button).
/// </summary>
public class MobilePunchPolicyService : BaseService<Branch>
{
    private readonly GeneralSettingService _settings;

    public MobilePunchPolicyService(IUnitOfWorkService uow, GeneralSettingService settings) : base(uow)
    {
        _settings = settings;
    }

    private async Task<bool> EnabledAsync()
    {
        var s = await _settings.GetSettingsAsync(PayrollSettingsIdentity.IdentityType);
        return s.TryGetValue(PayrollSettingsIdentity.KeyMobilePunchModesEnabled, out var setting)
            && GeneralSettingsUtil.ParseBool(setting.Value, false);
    }

    public async Task<ResolvedPunchMode> ResolveAsync(Guid? branchId, Guid? areaId, CancellationToken token)
    {
        // Company switch off: every location is Open, whatever mode was stored on it.
        if (!await EnabledAsync()) return ResolvedPunchMode.Open;

        CostCenters? site = null;
        if (areaId is { } siteId)
        {
            site = await Context.Areas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == siteId, token);
            // A site with no mode of its own follows the branch it sits in, even if the caller gave no branch.
            branchId ??= site?.BranchId;
        }

        Branch? branch = null;
        if (branchId is { } id)
            branch = await Context.Branches.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, token);

        return MobilePunchPolicy.Resolve(branch, site);
    }

    /// <summary>Throws a <see cref="GuardException"/> with the reason when this punch is not allowed.</summary>
    public async Task EnsureAllowedAsync(Guid? branchId, Guid? areaId, MobilePunchEvidence evidence, CancellationToken token)
    {
        var rule = await ResolveAsync(branchId, areaId, token);
        if (MobilePunchPolicy.Check(rule, evidence) is { } reason)
            throw new GuardException(reason);
    }
}
