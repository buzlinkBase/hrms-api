namespace Hrms.Domain.ValueObjects;

public class CreateLeaveApplication
{
    public Guid LeaveId { get; set; }
    public Guid EmployeeId { get; set; }
    public DateOnly LeaveDateFrom { get; set; }
    public DateOnly LeaveDateTo { get; set; }
    public DayType LeaveType { get; set; }
    public PayType PayType { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.ForApproval;
}

public class UpdateLeaveApplication : CreateLeaveApplication
{
    public Guid Id { get; set; }
}

public class LeaveApplicationModel : UpdateLeaveApplication
{
}

public class LeaveApplicationPyRun
{
    public Guid LeaveId { get; set; }
    public Guid EmployeeId { get; set; }
    public DateOnly LeaveDate { get; set; }
    public LeaveDayType DayType { get; set; }
    public PayType PayType { get; set; }
}
