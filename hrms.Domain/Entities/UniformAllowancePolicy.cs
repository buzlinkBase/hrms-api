using BuzlinkRepository;
using Hrms.Domain.Entities.EmployeeEntities;
using Onepunch.AuditEngine;

namespace Hrms.Domain.Entities;

// A client's uniform allowance rules from EffectiveFrom until the next version starts. Never
// edited once a credit run has used it -- a change is a new version, so every past credit still
// points at the rules that produced it (UniformAllowanceCreditLog.PolicyId). A client with no
// version yet falls back to its legacy Client.UniformAllowance/UniformAllowanceBasis columns
// (see UniformAllowancePolicyResolver). Edited in Client Settings > Uniform Allowance. Soft delete
// is off: only a version that never took effect can be deleted, and its start date must be free
// for a replacement (unique per client); the audit trail keeps the record of it.
[DisableSoftDelete]
[Audited]
public class UniformAllowancePolicy : BaseEntity
{
    public Guid ClientId { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public bool IsEnabled { get; set; } = true;

    // ── When credits post ──
    public UniformCreditFrequency Frequency { get; set; } = UniformCreditFrequency.Monthly;
    // Day of the month credits post on (1-28, so every month has it). Ignored for HireAnniversary.
    public int CreditDay { get; set; } = 1;
    // The first month of the cycle for Quarterly/SemiAnnual/Annual (1-12), e.g. 1 = Jan/Apr/Jul/Oct.
    public int StartMonth { get; set; } = 1;

    // ── How much ──
    public UniformAmountMode AmountMode { get; set; } = UniformAmountMode.FixedPerPeriod;
    // Pesos per period (Fixed, Prorated) or per present day (PerPresentDay).
    public decimal Amount { get; set; }
    // ProratedByPresentDays: the present days that earn the full amount.
    public int? ExpectedDaysPerPeriod { get; set; }

    // ── Who qualifies ──
    public List<EmploymentStatus> EligibleStatuses { get; set; } =
        [EmploymentStatus.Regular, EmploymentStatus.Probationary, EmploymentStatus.Contract];
    // Whole months of service before the first credit.
    public int WaitingMonths { get; set; }
    // Present days needed within the period; null = no attendance requirement.
    public int? MinPresentDays { get; set; }

    // ── Cap and expiry ──
    public decimal? MaxBalance { get; set; }
    public UniformBalanceExpiry Expiry { get; set; } = UniformBalanceExpiry.CarryOver;
    // ResetAnnually: the month whose 1st clears the balance.
    public int? ExpiryMonth { get; set; }
    // ExpireAfterMonths: credits older than this many months lapse, oldest first.
    public int? ExpiryAfterMonths { get; set; }

    // ── Separation and rehire ──
    public UniformSeparationRule OnSeparation { get; set; } = UniformSeparationRule.KeepForRelease;
    public UniformRehireRule RehireRule { get; set; } = UniformRehireRule.Restart;
    // BridgeWithinDays: a rehire within this many days of separating keeps the earlier service.
    public int? RehireBridgeDays { get; set; }

    // ── Who changed it and why (the audit trail records the field-level diff) ──
    public string ChangeReason { get; set; } = string.Empty;
    public Guid? CreatedByUserId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
}

public enum UniformCreditFrequency
{
    Monthly,
    Quarterly,
    SemiAnnual,
    Annual,
    // Once a year on the day service started.
    HireAnniversary,
}

public enum UniformAmountMode
{
    // The full amount each period the employee qualifies.
    FixedPerPeriod,
    // Amount x present days / ExpectedDaysPerPeriod, never above the full amount.
    ProratedByPresentDays,
    // Amount for every present day in the period.
    PerPresentDay,
}

public enum UniformBalanceExpiry
{
    CarryOver,
    ResetAnnually,
    ExpireAfterMonths,
}

public enum UniformSeparationRule
{
    // The balance stays, flagged for release with last pay.
    KeepForRelease,
    // The balance is written off to zero.
    Forfeit,
}

public enum UniformRehireRule
{
    // The waiting period starts over from the rehire.
    Restart,
    // Rehired within RehireBridgeDays: service keeps counting from the original start.
    BridgeWithinDays,
}

// One credit decision for one employee on one credit date: credited, capped, or skipped with the
// reason -- what "why didn't I get my allowance this month?" is answered from. Written by
// UniformAllowanceAccrualWorker; one row per employee per credit date (unique), so a re-run of the
// same day never decides twice.
public class UniformAllowanceCreditLog : BaseEntity, IEmployeeOwned, IEntryRecord
{
    public Guid EmployeeId { get; set; }
    public virtual Employee Employee { get; set; } = null!;
    public Guid ClientId { get; set; }
    // The version that decided it; null when the client was still on its legacy settings.
    public Guid? PolicyId { get; set; }
    public DateOnly CreditDate { get; set; }
    // The attendance window judged.
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public UniformCreditOutcome Outcome { get; set; }
    // A UniformSkipReason name for Skipped/Capped, empty for a full credit.
    public string ReasonCode { get; set; } = string.Empty;
    public string ReasonText { get; set; } = string.Empty;
    public int? PresentDays { get; set; }
    public int? RequiredDays { get; set; }
    // What was credited (0 when skipped).
    public decimal Amount { get; set; }
    public Guid? LedgerId { get; set; }
}

public enum UniformCreditOutcome
{
    Credited,
    // Credited less than earned because the balance reached its cap.
    Capped,
    Skipped,
}
