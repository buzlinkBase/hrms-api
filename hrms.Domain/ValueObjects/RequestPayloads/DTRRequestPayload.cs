namespace Hrms.Domain.ValueObjects;

public record DTRRequestPayload
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public Guid? EmployeeId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? PayrollGroupId { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? OperationAreaId { get; set; }
}
public record struct EmployeeRequestPayload(
    Guid? DepartmentId,
    Guid? EmployeeId,
    Guid? ClientId,
    Guid? PayrollGroupId);

public record DateEmployeeRequestPayload(DateOnly FromDate, DateOnly ToDate, List<Guid> EmployeeIds);

