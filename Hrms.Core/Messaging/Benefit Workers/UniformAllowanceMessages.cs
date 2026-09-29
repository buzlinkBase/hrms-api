namespace Hrms.Core.Messaging.BenefitWorkers;

// Trigger: accrue this month's Uniform Allowance for every eligible employee (run on the 1st of
// each month as a step of the daily benefits run -- see DailyBenefitsWorker).
// No FiscalYearStartMonth: unlike Leave's Annually basis, Uniform Allowance's accrual has no
// fiscal-year-anchored branch -- it's a flat monthly rate, every month, forever.
public record RunUniformAllowanceAccrual(DateOnly ProcessDate);
