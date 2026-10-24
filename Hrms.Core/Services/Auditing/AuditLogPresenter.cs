using System.Text.RegularExpressions;
using Onepunch.AuditEngine;
using Onepunch.AuditEngine.AspNetCore;

namespace Hrms.Core.Services.Auditing;

/// <summary>Filters sent by Security > Audit. All optional; dates are UTC instants.</summary>
public sealed record AuditLogQuery
{
    /// <summary>LOGIN, LOGOUT, CREATE, UPDATE, DELETE, APPROVE, REJECT, VIEW, EXPORT.</summary>
    public string? Action { get; init; }
    /// <summary>SUCCESS or FAILED.</summary>
    public string? Status { get; init; }
    /// <summary>An entity type from <see cref="AuditLogPresenter.Modules"/>.</summary>
    public string? Module { get; init; }
    public string? UserId { get; init; }
    public string? ResourceId { get; init; }
    public DateTimeOffset? From { get; init; }
    /// <summary>Exclusive.</summary>
    public DateTimeOffset? To { get; init; }
    public string? Search { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed record AuditLogChange(string Property, string? OldValue, string? NewValue);

/// <summary>The shape the frontend's AuditResponse expects, plus the recorded changes.</summary>
public sealed record AuditLogResponse(
    Guid Id,
    string Action,
    string Module,
    string? EntityType,
    string? ResourceId,
    string? UserId,
    string? UserName,
    string? Description,
    string? IpAddress,
    string? UserAgent,
    string Timestamp,
    string Status,
    string? Source,
    IReadOnlyList<AuditLogChange> Changes);

public sealed record AuditLogPage(IReadOnlyList<AuditLogResponse> Items, int Total, int Page, int PageSize);

public sealed record AuditModuleOption(string Value, string Label);

/// <summary>Maps between the audit engine's model and Security > Audit's API contract.</summary>
public static class AuditLogPresenter
{
    // Every entity type recorded today, across hrms-api, AuthApi, TenantApi and hrms-adms-api, with the label
    // the Audit screen shows. Types not listed still display (with a label derived from the name).
    private static readonly (string Value, string Label)[] KnownModules =
    [
        ("Employee", "Employee"),
        ("Leave", "Leave"),
        ("Overtime", "Overtime"),
        ("OfficialBusiness", "Official Business"),
        ("PassSlip", "Pass Slip"),
        ("Loan", "Loan/Deduction"),
        ("ProfileUpdate", "Profile Update"),
        ("PayrollBatch", "Payroll Batch"),
        ("PayrollPosting", "Payroll Posting"),
        ("PayrollPostingDeletion", "Payroll Posting Deletion"),
        ("DtrBatch", "DTR Batch"),
        ("Dtr", "DTR Posting"),
        ("DtrDeletion", "DTR Deletion"),
        ("ApprovalWorkflow", "Approval Workflow"),
        ("ApprovalWorkflowStep", "Approval Workflow Step"),
        ("User", "Account"),
        ("UserMembership", "Company Member"),
        ("MembershipRole", "Member Role"),
        ("Role", "Role"),
        ("RolePermission", "Role Permission"),
        ("Company", "Company"),
        ("CashBondAdjustment", "Cash Bond Adjustment"),
        ("CashBondRateChange", "Cash Bond Rate Change"),
        ("BiometricDevice", "Biometric Device"),
        ("DeviceCommand", "Device Command"),
        ("BiometricTemplate", "Biometric Template"),
    ];

    private static readonly Dictionary<string, string> ModuleLabels =
        KnownModules.ToDictionary(m => m.Value, m => m.Label, StringComparer.Ordinal);

    private static readonly string[] EventTypes =
    [
        AuditEventTypes.Login, AuditEventTypes.Logout, AuditEventTypes.Create, AuditEventTypes.Update,
        AuditEventTypes.Delete, AuditEventTypes.Approve, AuditEventTypes.Reject, AuditEventTypes.View,
        AuditEventTypes.Export,
    ];

    public static IReadOnlyList<AuditModuleOption> Modules { get; } =
        KnownModules.Select(m => new AuditModuleOption(m.Value, m.Label)).OrderBy(m => m.Label).ToList();

    /// <summary>Always scoped to <paramref name="companyId"/> -- a caller only ever sees their own company's trail.</summary>
    public static AuditQuery ToEngineQuery(AuditLogQuery query, string companyId) => new()
    {
        OrganizationId = companyId,
        EventType = EventTypes.FirstOrDefault(t => string.Equals(t, query.Action?.Trim(), StringComparison.OrdinalIgnoreCase)),
        Outcome = query.Status?.Trim().ToUpperInvariant() switch
        {
            "SUCCESS" => AuditOutcome.Success,
            "FAILED" or "FAILURE" => AuditOutcome.Failure,
            _ => null,
        },
        EntityType = Blank(query.Module),
        ActorId = Blank(query.UserId),
        EntityId = Blank(query.ResourceId),
        FromUtc = query.From?.UtcDateTime,
        ToUtc = query.To?.UtcDateTime,
        Search = Blank(query.Search),
        Page = query.Page,
        PageSize = query.PageSize,
    };

    public static AuditLogResponse ToResponse(AuditEvent e) => new(
        e.Id,
        e.EventType.ToUpperInvariant(),
        ModuleLabel(e.EntityType),
        e.EntityType,
        e.EntityId,
        e.Actor?.Id,
        e.Actor?.DisplayName,
        e.Description,
        e.Metadata.GetValueOrDefault(HttpContextAuditMetadataEnricher.IpAddress),
        e.Metadata.GetValueOrDefault(HttpContextAuditMetadataEnricher.UserAgent),
        DateTime.SpecifyKind(e.OccurredAtUtc, DateTimeKind.Utc).ToString("O"),
        e.Outcome == AuditOutcome.Failure ? "FAILED" : "SUCCESS",
        e.Source,
        e.Changes.Select(c => new AuditLogChange(c.Property, c.OldValue, c.NewValue)).ToList());

    public static AuditLogPage ToPage(AuditPage page) =>
        new(page.Items.Select(ToResponse).ToList(), page.Total, page.Page, page.PageSize);

    public static string ModuleLabel(string? entityType) =>
        entityType == null ? "System"
        : ModuleLabels.TryGetValue(entityType, out var label) ? label
        : Regex.Replace(entityType, "(?<=[a-z0-9])(?=[A-Z])", " ");

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
