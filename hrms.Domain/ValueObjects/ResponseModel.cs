namespace Hrms.Domain.ValueObjects;

public record struct EmployeeKey(Guid employeeId);
public record struct HolidayKey(Guid EmployeeId, DateOnly Date);
public record struct EmployeeKeyYearMonth(Guid employeeId, int Month, int year);
public record struct EmployeePayDateKey(Guid employeeId, DateOnly payrollDate);
public record struct EmployeeLeaveCreditsKey(Guid EmployeeId, Guid LeaveId);
public record DateRequestPayload(
    DateOnly FromDate,
    DateOnly ToDate);


