using Onepunch.AuditEngine;
using BuzlinkRepository;
using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Domain.Entities.Approvals;

// The saved policy for one application type, optionally limited to some scope (branches, sites,
// departments... -- see Scopes). Several can be active for a type at once; ApprovalWorkflowService
// keeps one active per (ApplicationType, scope signature) and applies the "immutable once it has
// instances" versioning rule; ApprovalScopeMatching picks which active one an application uses.
[DisableSoftDelete]
[Audited]
public class ApprovalWorkflow : BaseEntity
{
    public ApprovalApplicationType ApplicationType { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    // Legacy single-department scope, kept so workflows saved before Scopes existed keep working:
    // it counts as a Department condition. Saving through Setup converts it into a scope row.
    public Guid? ScopeDepartmentId { get; set; }
    public virtual Department? ScopeDepartment { get; set; }

    // Who this workflow applies to. No rows (and no legacy department) = the company-wide default.
    // AND across dimensions, OR within one dimension's values.
    public virtual ICollection<ApprovalWorkflowScope> Scopes { get; set; } = new List<ApprovalWorkflowScope>();

    // When an applicant matches several active workflows of the type, the lowest number wins
    // (then the most specific, then the oldest).
    public int Priority { get; set; } = 100;

    public virtual ICollection<ApprovalWorkflowStep> Steps { get; set; } = new List<ApprovalWorkflowStep>();

    // A scheduled switch: this (inactive) version takes over from that active workflow as soon
    // as the active one has no requests in progress -- see ApprovalWorkflowSwitch. Null = not
    // scheduled.
    public Guid? ReplacesWorkflowId { get; set; }
}

// One scope condition of a workflow: a dimension and one allowed value. A dimension with several
// rows means "any of these". TargetId holds the id for entity dimensions (branch, department...),
// TargetValue the name for EmploymentStatus.
[DisableSoftDelete]
public class ApprovalWorkflowScope : BaseEntity
{
    public Guid ApprovalWorkflowId { get; set; }
    public virtual ApprovalWorkflow? Workflow { get; set; }

    public ApprovalScopeDimension Dimension { get; set; }
    public Guid? TargetId { get; set; }
    public string? TargetValue { get; set; }
}

// One ordered step within a workflow. See Enums.ApproverType for what each ApproverType
// resolves to and which of the fields below apply.
[DisableSoftDelete]
[Audited]
public class ApprovalWorkflowStep : BaseEntity
{
    public Guid ApprovalWorkflowId { get; set; }
    public virtual ApprovalWorkflow? Workflow { get; set; }

    public int StepNumber { get; set; }
    public ApproverType ApproverType { get; set; }

    public Guid? ApproverEmployeeId { get; set; }          // ApproverType.Person
    public virtual Employee? ApproverEmployee { get; set; }

    public Guid? ApproverDepartmentId { get; set; }         // ApproverType.Department
    public virtual Department? ApproverDepartment { get; set; }

    public Guid? ApproverPositionId { get; set; }           // ApproverType.Position
    public virtual Position? ApproverPosition { get; set; }

    // Quorum — how many distinct approvers from the resolved pool are required before this step
    // clears. Meaningful for Department/Position/ApplicantDepartment; always 1 for Person and
    // ApplicantManager since those resolve to exactly one person.
    public int MinApprovals { get; set; } = 1;

    // ApplicantManagerLevel: how many managers up (1 = the applicant's manager). ApplicantDepartmentHead:
    // 1 = their department's head, 2 = the parent department's head, and so on.
    public int? ApproverLevel { get; set; }

    // What happens when the approver of a dynamic type (manager level, department/section/branch/
    // site/client head) can't be worked out for an applicant.
    public UnresolvedApproverPolicy OnUnresolved { get; set; } = UnresolvedApproverPolicy.Block;
    public Guid? FallbackEmployeeId { get; set; }

    // The step applies only when the request's measure passes the threshold ("Days >= 5"); otherwise
    // it is skipped. None = always. Never on step 1 (a skipped first step could finish the request
    // without the module's own status update). See ApprovalStepCondition.
    public ApprovalStepConditionMetric ConditionMetric { get; set; } = ApprovalStepConditionMetric.None;
    public ApprovalConditionOperator ConditionOperator { get; set; } = ApprovalConditionOperator.GreaterOrEqual;
    public decimal? ConditionValue { get; set; }

    // Reminders and escalation while the step waits. Hours count from when the request reached the
    // step. RemindAfterHours: first reminder to the step's approvers; RemindRepeatHours: and again
    // this often. EscalateAfterHours + EscalateToEmployeeId: also open the step to that person (an
    // addition to the approvers, not a replacement). Null = off. See ApprovalSlaPolicy.
    public int? RemindAfterHours { get; set; }
    public int? RemindRepeatHours { get; set; }
    public int? EscalateAfterHours { get; set; }
    public Guid? EscalateToEmployeeId { get; set; }

    public NoteRequirement NoteRequirement { get; set; } = NoteRequirement.Optional;

    // Optional narrowing of a Department/Position/ApplicantDepartment step's eligible pool down
    // to specific named employees within that group. Empty = "anyone currently in the group" is
    // eligible (the default, simplest case).
    // Extra pools of approvers for this step. The step's own approver above is the first pool; when there
    // are groups here the step clears only once EVERY pool has its quorum (for example one approval
    // from HR and one from Finance). Empty = the step behaves as it always has.
    public virtual ICollection<ApprovalWorkflowStepGroup> Groups { get; set; } = new List<ApprovalWorkflowStepGroup>();

    public virtual ICollection<ApprovalWorkflowStepApprover> NamedApprovers { get; set; } = new List<ApprovalWorkflowStepApprover>();
}

// One extra pool of approvers on a step -- same shape as the step's own approver (a person, a
// department, a position, or a per-applicant type), with its own quorum. See ApprovalStepPools.
[DisableSoftDelete]
public class ApprovalWorkflowStepGroup : BaseEntity
{
    public Guid ApprovalWorkflowStepId { get; set; }
    public virtual ApprovalWorkflowStep? Step { get; set; }

    /// <summary>1, 2, ... within the step; gives the pools a stable order.</summary>
    public int GroupNumber { get; set; }

    public ApproverType ApproverType { get; set; }
    public Guid? ApproverEmployeeId { get; set; }
    public Guid? ApproverDepartmentId { get; set; }
    public virtual Department? ApproverDepartment { get; set; }
    public Guid? ApproverPositionId { get; set; }
    public virtual Position? ApproverPosition { get; set; }
    public int? ApproverLevel { get; set; }
    public UnresolvedApproverPolicy OnUnresolved { get; set; } = UnresolvedApproverPolicy.Block;
    public Guid? FallbackEmployeeId { get; set; }

    /// <summary>How many distinct approvers from this pool must approve.</summary>
    public int MinApprovals { get; set; } = 1;
}

[DisableSoftDelete]
public class ApprovalWorkflowStepApprover : BaseEntity
{
    public Guid ApprovalWorkflowStepId { get; set; }
    public virtual ApprovalWorkflowStep? Step { get; set; }

    public Guid EmployeeId { get; set; }
    public virtual Employee? Employee { get; set; }
}
