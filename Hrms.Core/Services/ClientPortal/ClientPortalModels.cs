namespace Hrms.Core.Services.ClientPortal;

// The row shapes (PortalClientRow ... PortalDtrRow, PortalRowError) are shared with the portal through the message contracts in
// Onepunch.Common.Lib.DTO.ClientPortalAttendance.

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

public sealed record PortalIngestResult(int Accepted, int Rejected, IReadOnlyList<PortalRowError> Errors);

/// <summary>
/// What the last sync did, kept for the settings screen. <paramref name="Operation"/> is Upsert or Remove (null on summaries saved before
/// removals were sent); <paramref name="Waiting"/> means the rows were handed to the message bus and the portal hasn't answered yet.
/// </summary>
public sealed record ClientPortalSyncSummary(
    string BatchCode, DateTime AtUtc, int Records, int Accepted, int Rejected, bool Succeeded, string? Message, IReadOnlyList<PortalRowError>? Errors,
    string? Operation = null, bool Waiting = false, int PartsReceived = 0, int Parts = 0);

public sealed record ClientPortalSettingsModel(bool Enabled, string PortalUrl, bool HasApiKey, ClientPortalSyncSummary? LastSync);

public sealed record ClientPortalSettingsRequest(bool Enabled, string PortalUrl, string? ApiKey);

public sealed record ClientPortalCredentials(string PortalUrl, string ApiKey);

/// <summary>
/// Internal to HRIS: asks the sync worker to send posted DTR rows to the portal. Carries only what the worker needs to find the rows
/// again: a whole batch (<see cref="DtrBatchId"/>), or just some rows (<see cref="DailyRecordIds"/>, after a punch they use changed).
/// </summary>
public record ClientPortalSyncRequested
{
    public const string Approved = "Approved";
    public const string PunchChanged = "PunchChanged";
    public const string Resend = "Resend";

    public Guid DtrBatchId { get; init; }
    public string BatchCode { get; init; } = string.Empty;
    public List<Guid>? DailyRecordIds { get; init; }
    public string Reason { get; init; } = Approved;
}
