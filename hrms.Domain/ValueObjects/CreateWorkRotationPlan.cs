namespace Hrms.Domain.ValueObjects;

public class CreateWorkRotationPlan
{
    public DateOnly PayrollDate { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid TimeShiftId { get; set; }
    public Guid? OperationAreaId { get; set; }
    // Client picked directly on the day; wins over the site's client. Null = none.
    public Guid? ClientId { get; set; }
}

public class CreateWorkRotationPlanBatch
{
    public List<Guid> EmployeeIds { get; set; }
    public List<DateOnly> PayrollDates { get; set; }
    public Guid TimeShiftId { get; set; }
    public Guid? OperationAreaId { get; set; }
    public Guid? ClientId { get; set; }
}

public class UpdateWorkSchedulePlan : CreateWorkRotationPlan
{
    public Guid Id { get; set; }
}

public class WorkSchedulePlanModel
{
    public Guid Id { get; set; }
    public Guid TimeShiftId { get; set; }
    public Guid EmployeeId { get; set; }
    public DateOnly PayrollDate { get; set; }
    public string? BatchCode { get; set; }
    public string? FullName { get; set; }
    public string? ShiftName { get; set; }
    public Guid? OperationAreaId { get; set; }
    public string? AreaName { get; set; }
    public Guid? ClientId { get; set; }
    public string? ClientName { get; set; }
}

public record WorkRotationPlanFilter(DateOnly FromDate, DateOnly ToDate, Guid? EmployeeId);