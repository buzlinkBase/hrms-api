namespace Hrms.Domain.ValueObjects;

// "While I'm away" delegation (see ApprovalDelegation) and the My Approvals inbox payloads.

public class ApprovalDelegationRequest
{
    /// <summary>Admin screen only: who is delegating. Ignored on the self-service endpoint (always the caller).</summary>
    public Guid? DelegatorEmployeeId { get; set; }
    public Guid DelegateEmployeeId { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    /// <summary>Null = every application type.</summary>
    public ApprovalApplicationType? ApplicationType { get; set; }
    public bool OnlyWhileOnLeave { get; set; }
    public string? Reason { get; set; }
}

public class ApprovalDelegationResponse
{
    public Guid Id { get; set; }
    public Guid DelegatorEmployeeId { get; set; }
    public string? DelegatorName { get; set; }
    public Guid DelegateEmployeeId { get; set; }
    public string? DelegateName { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public ApprovalApplicationType? ApplicationType { get; set; }
    public bool OnlyWhileOnLeave { get; set; }
    public bool IsActive { get; set; }
    public string? Reason { get; set; }
    /// <summary>Active and within its dates (a "while on leave" one also needs the leave itself to be today).</summary>
    public bool IsInWindow { get; set; }
}

public class MyApprovalResponse
{
    public ApprovalApplicationType ApplicationType { get; set; }
    public Guid ApplicationId { get; set; }
    public Guid ApplicantEmployeeId { get; set; }
    public string ApplicantName { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public DateTime FiledAt { get; set; }
    public int StepNumber { get; set; }
    public int TotalSteps { get; set; }
    public string? FlowName { get; set; }
    public NoteRequirement NoteRequirement { get; set; }
    /// <summary>Set when the caller is acting for someone who delegated to them.</summary>
    public Guid? OnBehalfOfEmployeeId { get; set; }
    public string? OnBehalfOfName { get; set; }
    /// <summary>Who the current step is waiting on ("Ben Cruz", "HR Department", "HR + Finance").</summary>
    public string? CurrentApproverLabel { get; set; }
}
