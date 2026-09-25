namespace Hrms.Domain.ValueObjects;

// Setup > Approval Workflows config screen payload/response shapes. IDs only (ApproverEmployeeId/
// ApproverDepartmentId/ApproverPositionId/NamedApproverEmployeeIds) -- the frontend resolves
// display names against the Employee/Department/Position lists it already fetches elsewhere in
// Setup, same pattern as every other id-referencing dropdown in this app.

public class ApprovalWorkflowStepRequest
{
    public int StepNumber { get; set; }
    public ApproverType ApproverType { get; set; }
    public Guid? ApproverEmployeeId { get; set; }
    public Guid? ApproverDepartmentId { get; set; }
    public Guid? ApproverPositionId { get; set; }
    public int MinApprovals { get; set; } = 1;
    public NoteRequirement NoteRequirement { get; set; } = NoteRequirement.Optional;
    public List<Guid> NamedApproverEmployeeIds { get; set; } = [];
}

public class ApprovalWorkflowRequest
{
    public ApprovalApplicationType ApplicationType { get; set; }
    public string Name { get; set; } = string.Empty;
    // Null = tenant-wide default for this ApplicationType.
    public Guid? ScopeDepartmentId { get; set; }
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
    public NoteRequirement NoteRequirement { get; set; }
    public List<Guid> NamedApproverEmployeeIds { get; set; } = [];
}

public class ApprovalWorkflowResponse
{
    public Guid Id { get; set; }
    public ApprovalApplicationType ApplicationType { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public Guid? ScopeDepartmentId { get; set; }
    public string? ScopeDepartmentName { get; set; }
    public bool IsEditable { get; set; }
    public List<ApprovalWorkflowStepResponse> Steps { get; set; } = [];
}

public class ApprovalActionResponse
{
    public int StepNumber { get; set; }
    public Guid ActorEmployeeId { get; set; }
    // Resolved server-side -- the Employee Portal can't load the employee list to look names up,
    // so without this the applicant's timeline showed "Approved by 08df1607…".
    public string? ActorName { get; set; }
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

public class ApprovalInstanceResponse
{
    public ApprovalApplicationType ApplicationType { get; set; }
    public Guid ApplicationId { get; set; }
    public Guid? ApprovalWorkflowId { get; set; }
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
