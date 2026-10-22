using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Domain.ValueObjects;

/// <summary>A new (or a not-yet-started) uniform allowance policy version for one client.</summary>
public class UpsertUniformAllowancePolicy
{
    public DateOnly EffectiveFrom { get; set; }
    public bool IsEnabled { get; set; } = true;
    public UniformCreditFrequency Frequency { get; set; } = UniformCreditFrequency.Monthly;
    public int CreditDay { get; set; } = 1;
    public int StartMonth { get; set; } = 1;
    public UniformAmountMode AmountMode { get; set; } = UniformAmountMode.FixedPerPeriod;
    public decimal Amount { get; set; }
    public int? ExpectedDaysPerPeriod { get; set; }
    public List<EmploymentStatus> EligibleStatuses { get; set; } = [];
    public int WaitingMonths { get; set; }
    public int? MinPresentDays { get; set; }
    public decimal? MaxBalance { get; set; }
    public UniformBalanceExpiry Expiry { get; set; } = UniformBalanceExpiry.CarryOver;
    public int? ExpiryMonth { get; set; }
    public int? ExpiryAfterMonths { get; set; }
    public UniformSeparationRule OnSeparation { get; set; } = UniformSeparationRule.KeepForRelease;
    public UniformRehireRule RehireRule { get; set; } = UniformRehireRule.Restart;
    public int? RehireBridgeDays { get; set; }
    public string ChangeReason { get; set; } = string.Empty;
}

/// <summary>A saved version, or (Id null) the client's legacy settings shown as one.</summary>
public class UniformAllowancePolicyModel : UpsertUniformAllowancePolicy
{
    public Guid? Id { get; set; }
    public Guid ClientId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    /// <summary>Started on or before the last credit run -- read-only from now on.</summary>
    public bool IsLocked { get; set; }
    public bool IsLegacy { get; set; }
}

/// <summary>Client Settings > Uniform Allowance: every version, which one is in force, and the edit cut-off.</summary>
public class UniformAllowancePolicyListModel
{
    public List<UniformAllowancePolicyModel> Versions { get; set; } = [];
    /// <summary>The legacy settings, when the client used them before (or still, with no version yet).</summary>
    public UniformAllowancePolicyModel? Legacy { get; set; }
    public Guid? CurrentPolicyId { get; set; }
    /// <summary>The last day a credit was decided for this client; new versions must start after it.</summary>
    public DateOnly? LastProcessedCreditDate { get; set; }
}

/// <summary>One upcoming credit date and what it depends on.</summary>
public class UniformCreditPreviewModel
{
    public DateOnly CreditDate { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public Guid? PolicyId { get; set; }
    /// <summary>The amount when it's known up front (a fixed amount, ignoring the cap); null when it depends on attendance.</summary>
    public decimal? Amount { get; set; }
    public string Condition { get; set; } = string.Empty;
}

public class UniformAllowanceCreditLogModel
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeNo { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public Guid ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public Guid? PolicyId { get; set; }
    public DateOnly CreditDate { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public UniformCreditOutcome Outcome { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string ReasonText { get; set; } = string.Empty;
    public int? PresentDays { get; set; }
    public int? RequiredDays { get; set; }
    public decimal Amount { get; set; }
}

public class UniformAllowanceCreditLogFilter
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? EmployeeId { get; set; }
    public UniformCreditOutcome? Outcome { get; set; }
}

/// <summary>An upcoming credit for one employee, with whether they'd qualify on what's known today.</summary>
public class UniformUpcomingCreditModel : UniformCreditPreviewModel
{
    /// <summary>Why it won't be credited as things stand (e.g. still in the waiting period); empty when it should be.</summary>
    public string Blocker { get; set; } = string.Empty;
}

/// <summary>One employee's uniform allowance: the rules, what was decided, and what's next.</summary>
public class UniformAllowanceScheduleModel
{
    public Guid EmployeeId { get; set; }
    public decimal Balance { get; set; }
    public DateOnly? ServiceStartDate { get; set; }
    public DateOnly? SeparatedOn { get; set; }
    public UniformAllowancePolicyModel? Policy { get; set; }
    public List<UniformAllowanceCreditLogModel> History { get; set; } = [];
    public List<UniformUpcomingCreditModel> Upcoming { get; set; } = [];
}

/// <summary>"My Uniform Allowance" in the employee portal.</summary>
public class MyUniformAllowanceModel : UniformAllowanceScheduleModel
{
    public List<LedgerEntryModel> Entries { get; set; } = [];
}
