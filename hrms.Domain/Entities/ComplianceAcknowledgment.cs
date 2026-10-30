namespace Hrms.Domain.Entities;

// A company admin confirming, when saving a setting, that they chose a value that differs from the
// Philippine statutory (DOLE) default -- e.g. the Additive OT/ND method, or an overtime rate below
// 1.25. Evidence for the Terms of Service (customer configuration and labor-law compliance): who,
// when, from where, which setting, the statutory value and the value they chose. Append-only; a
// setting already acknowledged at the same value isn't asked about again
// (ComplianceAcknowledgmentService). Erased with the company like every TenantId table.
public class ComplianceAcknowledgment : BaseEntity
{
    // Where the setting lives: "company", or "client:{clientId}" for a client's own override.
    public string Scope { get; set; } = string.Empty;
    // Stable key of the setting (StatutoryDeviation.Key), e.g. "rate.OVERTIME".
    public string SettingKey { get; set; } = string.Empty;
    public string SettingLabel { get; set; } = string.Empty;
    public string StatutoryValue { get; set; } = string.Empty;
    public string ConfiguredValue { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string? IpAddress { get; set; }
    public DateTime AcknowledgedAtUtc { get; set; }
}
