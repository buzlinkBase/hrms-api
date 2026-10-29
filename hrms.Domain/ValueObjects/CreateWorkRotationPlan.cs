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

// One cell of the Quick Schedule grid. A null TimeShiftId clears that employee's day.
public class WorkRotationGridEntry
{
    public Guid EmployeeId { get; set; }
    public DateOnly PayrollDate { get; set; }
    public Guid? TimeShiftId { get; set; }
    public Guid? OperationAreaId { get; set; }
    public Guid? ClientId { get; set; }
}

public class ApplyWorkRotationGrid
{
    public List<WorkRotationGridEntry> Entries { get; set; } = [];
}

public class ApplyWorkRotationGridResult
{
    public string? BatchCode { get; set; }
    public int Saved { get; set; }
    public int Cleared { get; set; }
}

public record WorkRotationGridFilter(DateOnly FromDate, DateOnly ToDate, List<Guid>? EmployeeIds);

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

// What a team-scoped caller can schedule. AllowedAreaIds empty = any project site.
public record ScheduleScopeModel(List<EmployeeFilterResponseModel> Employees, List<Guid> AllowedAreaIds);

public class SetSupervisorSiteGrants
{
    public List<Guid>? AreaIds { get; set; }
}
