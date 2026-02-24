namespace Hrms.Domain.ValueObjects;

public class CreateOverTimeApplication
{
    public Guid EmployeeId { get; set; }
    public DateOnly OTDate { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool FlexiEndTime { get; set; }
    public bool PaidByNetDutyTime { get; set; }
    public double OverTimeThreshold { get; set; }
    public string Remarks { get; set; } = string.Empty;
    public ApprovalStatus OTStatus { get; set; }
    public double OTMinutes { get; set; }
    public double OTBeforeOverride { get; set; }

}

public class UpdateOvertimeApplication : CreateOverTimeApplication
{
    public Guid Id { get; set; }
    public string Status { get; set; }
}
public class OvertimeApplicationModel : UpdateOvertimeApplication;