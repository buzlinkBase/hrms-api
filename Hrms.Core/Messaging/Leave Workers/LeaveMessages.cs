namespace Hrms.Core.Messaging.LeaveWorkers;

// Published when a leave application is created (filed) — used to grant PerEvent credits upfront
public record LeaveApplicationCreated(Guid ApplicationId, Guid EmployeeId, Guid LeaveId);

// Trigger: grant initial credits for all eligible employees at period start (Jan 1)
public record RunLeavePeriodGrant(int Year);

// Trigger: accrue monthly/annual credits (published on 1st of each month)
public record RunLeaveAccrual(DateOnly ProcessDate);

// Trigger: process year-end carry-over / expiry for all employees
public record RunLeaveCarryOver(int FromYear);
