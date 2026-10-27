namespace Hrms.Domain.ValueObjects;

// Setup > Approval Workflows config screen payload/response shapes. IDs only (ApproverEmployeeId/
// ApproverDepartmentId/ApproverPositionId/NamedApproverEmployeeIds) -- the frontend resolves
// display names against the Employee/Department/Position lists it already fetches elsewhere in
// Setup, same pattern as every other id-referencing dropdown in this app.

// One extra pool of approvers on a step -- same shape as a step's own approver, with its own quorum.
// The step clears only when every pool (the step's own approver and each group) has its quorum.
public class ApprovalWorkflowStepGroupDto
{
    public ApproverType ApproverType { get; set; }
    public Guid? ApproverEmployeeId { get; set; }
    public Guid? ApproverDepartmentId { get; set; }
    public Guid? ApproverPositionId { get; set; }
    public int? ApproverLevel { get; set; }
    public UnresolvedApproverPolicy OnUnresolved { get; set; } = UnresolvedApproverPolicy.Block;
    public Guid? FallbackEmployeeId { get; set; }
    public int MinApprovals { get; set; } = 1;
}

public class ApprovalWorkflowStepRequest
{
    public int StepNumber { get; set; }
    public ApproverType ApproverType { get; set; }
    public Guid? ApproverEmployeeId { get; set; }
    public Guid? ApproverDepartmentId { get; set; }
    public Guid? ApproverPositionId { get; set; }
    public int MinApprovals { get; set; } = 1;
    public int? ApproverLevel { get; set; }
    public UnresolvedApproverPolicy OnUnresolved { get; set; } = UnresolvedApproverPolicy.Block;
    public Guid? FallbackEmployeeId { get; set; }
    public ApprovalStepConditionMetric ConditionMetric { get; set; } = ApprovalStepConditionMetric.None;
    public ApprovalConditionOperator ConditionOperator { get; set; } = ApprovalConditionOperator.GreaterOrEqual;
    public decimal? ConditionValue { get; set; }
    public int? RemindAfterHours { get; set; }
    public int? RemindRepeatHours { get; set; }
    public int? EscalateAfterHours { get; set; }
    public Guid? EscalateToEmployeeId { get; set; }
    public NoteRequirement NoteRequirement { get; set; } = NoteRequirement.Optional;
    public List<Guid> NamedApproverEmployeeIds { get; set; } = [];
    /// <summary>Extra pools of approvers; empty = an ordinary single-pool step.</summary>
    public List<ApprovalWorkflowStepGroupDto> Groups { get; set; } = [];
}

// One scope condition: a dimension and the values it allows. Entity dimensions (branch,
// department, client, site, payroll group, position, section, employee) use TargetIds;
// EmploymentStatus uses Values (the status names).
public class ApprovalWorkflowScopeDto
{
    public ApprovalScopeDimension Dimension { get; set; }
    public List<Guid> TargetIds { get; set; } = [];
    public List<string> Values { get; set; } = [];
}

public class ApprovalWorkflowRequest
{
    public ApprovalApplicationType ApplicationType { get; set; }
    public string Name { get; set; } = string.Empty;
    // Older clients: one department. Folded into Scopes as a Department condition on save.
    public Guid? ScopeDepartmentId { get; set; }
    // Who the workflow applies to; none = the company-wide default.
    public List<ApprovalWorkflowScopeDto> Scopes { get; set; } = [];
    // Lower is checked first when an applicant matches several active workflows.
    public int Priority { get; set; } = 100;
    public List<ApprovalWorkflowStepRequest> Steps { get; set; } = [];
}

public class ApprovalWorkflowStepResponse
{
    public Guid Id { get; set; }
    public int StepNumber { get; set; }
    public ApproverType ApproverType { get; set; }
    public Guid? ApproverEmployeeId { get; set; }
    public Guid? ApproverDepartmentId { get; set; }
    public Guid? ApproverPositionId { get; set; }
    public int MinApprovals { get; set; }
    public int? ApproverLevel { get; set; }
    public UnresolvedApproverPolicy OnUnresolved { get; set; }
    public Guid? FallbackEmployeeId { get; set; }
    public ApprovalStepConditionMetric ConditionMetric { get; set; }
    public ApprovalConditionOperator ConditionOperator { get; set; }
    public decimal? ConditionValue { get; set; }
    public int? RemindAfterHours { get; set; }
    public int? RemindRepeatHours { get; set; }
    public int? EscalateAfterHours { get; set; }
    public Guid? EscalateToEmployeeId { get; set; }
    public NoteRequirement NoteRequirement { get; set; }
    public List<Guid> NamedApproverEmployeeIds { get; set; } = [];
    public List<ApprovalWorkflowStepGroupDto> Groups { get; set; } = [];
}

public class ApprovalWorkflowResponse
{
    public Guid Id { get; set; }
    public ApprovalApplicationType ApplicationType { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public Guid? ScopeDepartmentId { get; set; }
    public string? ScopeDepartmentName { get; set; }
    public int Priority { get; set; }
    /// <summary>Who it applies to, with the legacy single department folded in; empty = everyone.</summary>
    public List<ApprovalWorkflowScopeDto> Scopes { get; set; } = [];
    /// <summary>Workflows with the same slot (type + identical conditions) compete for activation; the UI groups versions by this.</summary>
    public string SlotKey { get; set; } = string.Empty;
    /// <summary>Other active workflows that can apply to the same people at the same priority.</summary>
    public List<string> OverlapsWith { get; set; } = [];
    /// <summary>Same as CanEdit; kept for older screens.</summary>
    public bool IsEditable { get; set; }
    /// <summary>Requests still going through this workflow -- it can't change until they finish.</summary>
    public int InProgressCount { get; set; }
    public bool HasBeenUsed { get; set; }
    public bool CanEdit { get; set; }
    /// <summary>Only a never-used workflow can be deleted (past requests reference it).</summary>
    public bool CanDelete { get; set; }
    /// <summary>Set when this version is scheduled to take over from that active workflow once it drains.</summary>
    public Guid? ReplacesWorkflowId { get; set; }
    public string? ReplacesWorkflowName { get; set; }
    /// <summary>Set on the active workflow when another version is waiting to take over from it.</summary>
    public string? ReplacedByWorkflowName { get; set; }
    public List<ApprovalWorkflowStepResponse> Steps { get; set; } = [];
    /// <summary>
    /// Names of the employees the steps reference (approver + named approvers), keyed by id --
    /// "Confidential" for one the caller may not see, who is missing from their employee list.
    /// </summary>
    public Dictionary<Guid, string> ApproverNames { get; set; } = [];
}

public class ApprovalWorkflowCandidateResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Priority { get; set; }
    /// <summary>Whether the employee falls under this workflow's conditions.</summary>
    public bool Matched { get; set; }
}

/// <summary>Which active workflow an employee's applications of a type run on (null = the single-step fallback).</summary>
public class ApprovalWorkflowResolutionResponse
{
    public Guid? WorkflowId { get; set; }
    public string? WorkflowName { get; set; }
    public bool IsFallback { get; set; }
    /// <summary>Every active workflow of the type in the order they are tried.</summary>
    public List<ApprovalWorkflowCandidateResponse> Candidates { get; set; } = [];
}

/// <summary>Whether a scheduled version took over right away (nothing was pending on the current one).</summary>
public sealed record ScheduleActivationResult(bool Activated);

public class ApprovalActionResponse
{
    public int StepNumber { get; set; }
    // Null for a system entry such as a skipped step.
    public Guid? ActorEmployeeId { get; set; }
    // Resolved server-side -- the Employee Portal can't load the employee list to look names up,
    // so without this the applicant's timeline showed "Approved by 08df1607…".
    public string? ActorName { get; set; }
    // Set when the actor decided for someone who had delegated to them ("while I'm away").
    public string? OnBehalfOfName { get; set; }
    public ApprovalActionType Action { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}

// One entry per step of the instance's workflow, resolved server-side the same way
// CurrentStepApproverLabel is -- lets the Approval Progress timeline show every upcoming step's
// approver/department, not just the current one, without the frontend doing its own
// Employee/Department/Position lookups.
public class ApprovalStepSummaryResponse
{
    public int StepNumber { get; set; }
    public ApproverType ApproverType { get; set; }
    public string? ApproverLabel { get; set; }
    public NoteRequirement NoteRequirement { get; set; }
}

/// <summary>Where one pool of a step with several stands: approvals so far against what it needs.</summary>
public class ApprovalPoolProgressResponse
{
    public string? Label { get; set; }
    public int Approved { get; set; }
    public int Required { get; set; }
    public bool Satisfied { get; set; }
}

public class ApprovalInstanceResponse
{
    public ApprovalApplicationType ApplicationType { get; set; }
    public Guid ApplicationId { get; set; }
    public Guid? ApprovalWorkflowId { get; set; }
    public string? ApprovalWorkflowName { get; set; }
    public int CurrentStepNumber { get; set; }
    public int TotalSteps { get; set; }
    public ApprovalInstanceStatus Status { get; set; }
    // Lets the frontend's approve/decline modal know whether to show/require a note field before
    // the caller acts -- None for the implicit fallback step (no workflow configured).
    public NoteRequirement CurrentStepNoteRequirement { get; set; } = NoteRequirement.None;
    // Resolved server-side so the Employee Portal's "pending with X" display needs no extra
    // Employee/Department/Position lookups of its own. Null once Status is no longer InProgress,
    // or for the implicit fallback step (no workflow configured).
    public ApproverType? CurrentStepApproverType { get; set; }
    /// <summary>The current step is a dynamic type that found nobody for this applicant and no fallback -- an admin needs to reassign it.</summary>
    public bool CurrentStepNoApprover { get; set; }
    /// <summary>Only for a current step with several pools: each pool's progress (who is still needed).</summary>
    public List<ApprovalPoolProgressResponse> CurrentStepPools { get; set; } = [];
    public string? CurrentStepApproverLabel { get; set; }
    public List<ApprovalActionResponse> Actions { get; set; } = [];
    public List<ApprovalStepSummaryResponse> Steps { get; set; } = [];
}

// ApprovalsController's Owner/Admin-only "Reassign" action payload — see
// ApprovalEngineService.ReassignApproverAsync.
public class ReassignApproverRequest
{
    public Guid NewApproverEmployeeId { get; set; }
    public string? Note { get; set; }
}
