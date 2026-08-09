namespace Hrms.Domain.ValueObjects;

public class CreateLeaveApplication
{
    public Guid LeaveId { get; set; }
    public Guid EmployeeId { get; set; }
    public DateOnly LeaveDateFrom { get; set; }
    public DateOnly LeaveDateTo { get; set; }
    public PayType PayType { get; set; } = PayType.WithPay;//TODO payment should be based on actual credits 
    public LeaveDayType DayType { get; set; } = LeaveDayType.WholeDay;
    public string? ApplicationRemarks { get; set; }
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

public class LeaveApplicationPyRun
{
    public Guid LeaveId { get; set; }
    public Guid EmployeeId { get; set; }
    public DateOnly LeaveDate { get; set; }
    public LeaveDayType DayType { get; set; }
    public PayType PayType { get; set; }
}
