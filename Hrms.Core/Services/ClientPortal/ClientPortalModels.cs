namespace Hrms.Core.Services.ClientPortal;

// Wire shapes of the Client Portal's /ingest API (camelCase JSON). Every row is keyed by this system's own
// ids ("external ids"), so re-sending a row updates it instead of duplicating it.
public sealed record PortalClientRow(string ExternalId, string Code, string Name);

public sealed record PortalSiteRow(string ExternalId, string ClientExternalId, string Code, string Name);

public sealed record PortalPostRow(string ExternalId, string SiteExternalId, string Code, string Name);

public sealed record PortalGuardRow(string ExternalId, string ClientExternalId, string Code, string FullName);

public sealed record PortalScheduleRow(string PostExternalId, DateOnly WorkDate, string ShiftName, int RequiredHeadcount);

public sealed record PortalPunchRow(DateTime At, string Kind);

public sealed record PortalDtrRow(
    string ExternalId,
    string ClientExternalId,
    string SiteExternalId,
    string PostExternalId,
    string GuardExternalId,
    DateOnly WorkDate,
    string ShiftName,
    DateTime? TimeIn,
    DateTime? TimeOut,
    int BreakMinutes,
    int LateMinutes,
    int UndertimeMinutes,
    decimal RegularHours,
    decimal OvertimeHours,
    decimal NightDiffHours,
    decimal HolidayHours,
    bool OvertimeApproved,
    IReadOnlyList<PortalPunchRow> Punches);

/// <summary>Everything one approved DTR batch contributes to the portal, in the order the portal needs it (parents first).</summary>
public sealed record PortalPayload(
    IReadOnlyList<PortalClientRow> Clients,
    IReadOnlyList<PortalSiteRow> Sites,
    IReadOnlyList<PortalPostRow> Posts,
    IReadOnlyList<PortalGuardRow> Guards,
    IReadOnlyList<PortalScheduleRow> Schedules,
    IReadOnlyList<PortalDtrRow> Records);

/// <summary>The company's name and logo (base64 of the image file; null for none), as the portal's ingest endpoint takes them.</summary>
public sealed record PortalBrandingRow(string Name, string? LogoBase64);

public sealed record PortalRowError(string ExternalId, string Error);

public sealed record PortalIngestResult(int Accepted, int Rejected, IReadOnlyList<PortalRowError> Errors);

/// <summary>What the last sync did, kept for the settings screen.</summary>
public sealed record ClientPortalSyncSummary(
    string BatchCode, DateTime AtUtc, int Records, int Accepted, int Rejected, bool Succeeded, string? Message, IReadOnlyList<PortalRowError>? Errors);

public sealed record ClientPortalSettingsModel(bool Enabled, string PortalUrl, bool HasApiKey, ClientPortalSyncSummary? LastSync);

public sealed record ClientPortalSettingsRequest(bool Enabled, string PortalUrl, string? ApiKey);

public sealed record ClientPortalCredentials(string PortalUrl, string ApiKey);

/// <summary>Published once a DTR batch is posted; carries only what the sync needs to find the rows again.</summary>
public record ClientPortalSyncRequested
{
    public Guid DtrBatchId { get; init; }
    public string BatchCode { get; init; } = string.Empty;
}
