namespace Hrms.Domain.ValueObjects;

public class CreateOverTimeApplication
{
    public Guid EmployeeId { get; set; }
    public DateOnly OTDate { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public double ManualOTMinutes { get; set; }
    public bool IsManualEntry { get; set; }
    public string Remarks { get; set; } = string.Empty;
}

public class UpdateOvertimeApplication : CreateOverTimeApplication
{
    public Guid Id { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; }
    // Approver's note for an approve/decline transition — see LeaveApplication's UpdateLeaveApplication.Note.
    public string? Note { get; set; }
}

public class OvertimeApplicationModel : UpdateOvertimeApplication
{ 
    public double OTBeforeOverride { get; set; }
    public bool FlexiEndTime { get; set; }
    public bool PaidByNetDutyTime { get; set; }
    public double OverTimeThreshold { get; set; }
    // "Filed On" on the portal's My Overtime Applications list.
    public DateTime CreatedAt { get; set; }
}
