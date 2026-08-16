namespace Hrms.Domain.ValueObjects;

public class CreateLeaveApplication
{
    public Guid LeaveId { get; set; }
    public Guid EmployeeId { get; set; }
    public DurationType DurationType { get; set; } = DurationType.SingleDay;
    public DateOnly LeaveDateFrom { get; set; }
    public DateOnly LeaveDateTo { get; set; }
    public DayFraction DayFraction { get; set; } = DayFraction.FullDay;
    public PayType PayType { get; set; } = PayType.WithPay;
    public bool IsManualEntry { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int? TotalMinutes { get; set; }
    public string? ApplicationRemarks { get; set; }
    public string? SupportingDocumentUrl { get; set; }
}

public class UpdateLeaveApplication : CreateLeaveApplication
{
    public Guid Id { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; }
}

public class LeaveApplicationModel : UpdateLeaveApplication
{
    public int? ReviewedBy { get; set; }
    public DateTime? ReviewedOn { get; set; }
}
