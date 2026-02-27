namespace Hrms.Domain.ValueObjects;

public record struct DTRRequestPayload(
    DateOnly FromDate,
    DateOnly ToDate,
    Guid? DepartmentId,
    Guid? EmployeeId,
    Guid? ClientId,
    Guid? PayrollGroupId);

public record struct EmployeeRequestPayload(
    Guid? DepartmentId,
    Guid? EmployeeId,
    Guid? ClientId,
    Guid? PayrollGroupId);

public record DateEmployeeRequestPayload(DateOnly FromDate, DateOnly ToDate, List<Guid> EmployeeIds);

