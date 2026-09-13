namespace Hrms.Domain.ValueObjects;

public record struct EmployeeKey(Guid employeeId);
public record struct EmployeeKeyYearMonth(Guid employeeId, int Month, int year);
public record struct EmployeePayDateKey(Guid employeeId, DateOnly payrollDate);
public record struct EmployeeLeaveCreditsKey(Guid EmployeeId, Guid LeaveId);
public record struct ClientRateKey(Guid ClientId, RateType Type);
public record struct ClientStatutoryCapKey(Guid ClientId, StatutoryCapType Type);
public record DateRequestPayload(
    DateOnly FromDate,
    DateOnly ToDate);


