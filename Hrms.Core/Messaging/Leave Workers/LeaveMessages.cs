namespace Hrms.Core.Messaging.LeaveWorkers;

// Published when a leave application is created (filed) — used to grant PerEvent credits upfront
public record LeaveApplicationCreated(Guid ApplicationId, Guid EmployeeId, Guid LeaveId);

// Trigger: grant initial credits for all eligible employees at fiscal period start
// FiscalYearStartMonth drives the period date range (1 = calendar year, 4 = Apr–Mar, etc.)
public record RunLeavePeriodGrant(int Year, int FiscalYearStartMonth = 1);

// Trigger: accrue monthly/annual credits (published on 1st of each month)
// FiscalYearStartMonth is needed so the annual-accrual check fires on the right month
public record RunLeaveAccrual(DateOnly ProcessDate, int FiscalYearStartMonth = 1);

// Trigger: process fiscal year-end carry-over / expiry
// FromYear is the fiscal year label (the year the fiscal year STARTED)
// FiscalYearStartMonth determines the actual close date and next-period date range
public record RunLeaveCarryOver(int FromYear, int FiscalYearStartMonth = 1);
