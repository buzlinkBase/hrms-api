namespace Hrms.Domain.ValueObjects;

public class CreateUnderTimeApplication
{
    public Guid EmployeeId { get; set; }
    public DateOnly OTDate { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool FlexiEndTime { get; set; }
    public bool PaidByNetDutyTime { get; set; }
    public double UnderTimeThreshold { get; set; }
    public string Remarks { get; set; } = string.Empty;
    public ApprovalStatus OTStatus { get; set; }
    public double OTMinutes { get; set; }
    public double OTBeforeOverride { get; set; }

}

public class UpdateUnderTimeApplication : CreateUnderTimeApplication
{
    public Guid Id { get; set; }
    public string Status { get; set; }
}
public class UnderTimeApplicationModel : UpdateUnderTimeApplication;