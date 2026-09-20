using BuzlinkRepository;
using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Domain.Entities.Approvals;

// Tracks one submitted application record's live progress through its workflow. Created by
// ApprovalEngineService.StartAsync right after the application itself is saved — one row per
// application, regardless of whether a workflow was actually configured (see ApprovalWorkflowId
// below).
[DisableSoftDelete]
public class ApprovalInstance : BaseEntity
{
    public ApprovalApplicationType ApplicationType { get; set; }

    // Polymorphic reference to the application record (LeaveApplication.Id, etc.) — no DB-level
    // FK constraint possible across tables, same as every other polymorphic reference in this
    // codebase.
    public Guid ApplicationId { get; set; }

    // Denormalized copy of the application's own EmployeeId, captured once at StartAsync. The
    // engine is shared across 5 different application tables and has no generic way to look this
    // up from ApplicationId alone, and it's needed on every eligibility check to resolve the
    // ApplicantManager/ApplicantDepartment approver types.
    public Guid ApplicantEmployeeId { get; set; }
    public virtual Employee? Applicant { get; set; }

    // Null = no workflow was configured for this application type/department at submission time
    // — this is the implicit single-step legacy fallback (anyone holding the coarse
    // {Row}:Approve permission can act, no note). Set = the frozen workflow governing this
    // instance; that workflow can no longer be edited once any instance references it.
    public Guid? ApprovalWorkflowId { get; set; }
    public virtual ApprovalWorkflow? Workflow { get; set; }

    public int CurrentStepNumber { get; set; } = 1;
    public ApprovalInstanceStatus Status { get; set; } = ApprovalInstanceStatus.InProgress;

    public virtual ICollection<ApprovalAction> Actions { get; set; } = new List<ApprovalAction>();
}

// Audit log of every approve/decline click — also how quorum is counted for a Department/
// Position/ApplicantDepartment step (distinct ActorEmployeeId rows with Action == Approved for
// the current StepNumber).
[DisableSoftDelete]
public class ApprovalAction : BaseEntity
{
    public Guid ApprovalInstanceId { get; set; }
    public virtual ApprovalInstance? Instance { get; set; }

    public int StepNumber { get; set; }

    public Guid ActorEmployeeId { get; set; }
    public virtual Employee? Actor { get; set; }

    public ApprovalActionType Action { get; set; }
    public string? Note { get; set; }
}
