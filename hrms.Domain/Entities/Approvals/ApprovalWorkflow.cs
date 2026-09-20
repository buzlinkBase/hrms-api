using BuzlinkRepository;
using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Domain.Entities.Approvals;

// The saved policy for one application type (and optionally one department). See
// ApprovalWorkflowService for the one-active-per-(ApplicationType, ScopeDepartmentId) rule and
// the "immutable once it has instances" versioning rule.
[DisableSoftDelete]
public class ApprovalWorkflow : BaseEntity
{
    public ApprovalApplicationType ApplicationType { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    // Null = the tenant-wide default for this ApplicationType. Set = overrides the default for
    // applicants in that one department only — see ApprovalEngineService's scope resolution.
    public Guid? ScopeDepartmentId { get; set; }
    public virtual Department? ScopeDepartment { get; set; }

    public virtual ICollection<ApprovalWorkflowStep> Steps { get; set; } = new List<ApprovalWorkflowStep>();
}

// One ordered step within a workflow. See Enums.ApproverType for what each ApproverType
// resolves to and which of the fields below apply.
[DisableSoftDelete]
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

    public NoteRequirement NoteRequirement { get; set; } = NoteRequirement.Optional;

    // Optional narrowing of a Department/Position/ApplicantDepartment step's eligible pool down
    // to specific named employees within that group. Empty = "anyone currently in the group" is
    // eligible (the default, simplest case).
    public virtual ICollection<ApprovalWorkflowStepApprover> NamedApprovers { get; set; } = new List<ApprovalWorkflowStepApprover>();
}

[DisableSoftDelete]
public class ApprovalWorkflowStepApprover : BaseEntity
{
    public Guid ApprovalWorkflowStepId { get; set; }
    public virtual ApprovalWorkflowStep? Step { get; set; }

    public Guid EmployeeId { get; set; }
    public virtual Employee? Employee { get; set; }
}
