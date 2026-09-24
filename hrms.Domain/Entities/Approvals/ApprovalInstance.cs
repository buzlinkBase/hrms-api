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

    // Bumped every time StartAsync resets an already-resolved instance for a re-request (see
    // StartAsync's restart branch) -- stays 1 for an instance that's never been restarted. Exists
    // so RecordActionAsync's same-step idempotency guard can tell "already acted on THIS cycle's
    // current step" apart from a same-numbered step in a PRIOR, already-resolved cycle: old
    // ApprovalAction rows are deliberately kept as audit history across a restart (see StartAsync's
    // own comment), and StepNumber alone gets reused (resets to 1), so StepNumber+ActorEmployeeId
    // can collide with a stale action from before the restart without this.
    public int CycleNumber { get; set; } = 1;

    // Owner/Admin override for the CURRENT step only -- when set, this employee is the sole
    // eligible approver for CurrentStepNumber, replacing whatever the step's own ApproverType
    // would normally resolve to (see ApproverEligibilityResolver.IsEligible). Cleared
    // automatically whenever CurrentStepNumber advances or the instance resolves (see
    // ApprovalEngineService.RecordActionAsync), so it never leaks into a later step.
    public Guid? ReassignedApproverEmployeeId { get; set; }
    public virtual Employee? ReassignedApprover { get; set; }

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

    // Snapshot of ApprovalInstance.CycleNumber at the moment this action was recorded -- lets a
    // same-StepNumber lookup (e.g. RecordActionAsync's idempotency guard) distinguish this
    // cycle's action from a same-numbered step's action kept as history from a prior, already-
    // resolved cycle.
    public int CycleNumber { get; set; } = 1;

    public Guid ActorEmployeeId { get; set; }
    public virtual Employee? Actor { get; set; }

    public ApprovalActionType Action { get; set; }
    public string? Note { get; set; }
}
