using BuzlinkRepository;
using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Domain.Entities.Approvals;

/// <summary>
/// "While I'm away, let this person approve for me." The delegate may act on requests whose
/// current step would have been the delegator's, for the chosen application type (or all of
/// them), between the two dates (either may be open), and -- when OnlyWhileOnLeave -- only
/// while the delegator has an approved leave covering today. Delegation is direct only: it
/// never passes on again (A -> B does not let B's own delegate act for A).
/// </summary>
[DisableSoftDelete]
public class ApprovalDelegation : BaseEntity
{
    public Guid DelegatorEmployeeId { get; set; }
    public virtual Employee? Delegator { get; set; }

    public Guid DelegateEmployeeId { get; set; }
    public virtual Employee? Delegate { get; set; }

    /// <summary>First day it applies (PH date); null = from now.</summary>
    public DateOnly? FromDate { get; set; }

    /// <summary>Last day it applies, inclusive (PH date); null = until switched off.</summary>
    public DateOnly? ToDate { get; set; }

    /// <summary>Null = every application type.</summary>
    public ApprovalApplicationType? ApplicationType { get; set; }

    /// <summary>Applies only on days the delegator has an approved leave -- an automatic out-of-office.</summary>
    public bool OnlyWhileOnLeave { get; set; }

    public bool IsActive { get; set; } = true;
    public string? Reason { get; set; }
}
