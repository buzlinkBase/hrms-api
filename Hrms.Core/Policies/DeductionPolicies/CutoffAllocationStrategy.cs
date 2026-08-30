namespace Hrms.Core.Policies.DeductionPolicies;

// Strategy (GoF) for how much of a statutory component's outstanding monthly balance a
// non-final cutoff/week/period should withhold. The last cutoff of the month never uses
// this — it always takes the exact remaining balance (see the Table*Calculator classes),
// which is already correct-by-construction regardless of how earlier periods split things.
// Selected per employee via CutoffAllocationStrategyFactory. Generic over SSS/HDMF/PHIC —
// each component (EE/ER/EC) is allocated with one call — and over Semi-Monthly/Weekly/
// Monthly, since all three feed it the same (rate, balance, divisor) shape.
public interface ICutoffAllocationStrategy
{
    decimal AllocateFirstCutoffShare(decimal rate, decimal balance, DeductionPayloadContext context, int divisor, int? daysWorked = null, int? totalDaysInMonth = null);
}

// Fixed-salary employees earn the same amount every cutoff, so an even divide-by-N split is
// exact — no need to look at actual gross. This is the original, still-default behavior.
// daysWorked/totalDaysInMonth (when the caller is prorating a mid-month hire's first period)
// pass straight through to CalcRemainingBalance, unchanged from before this strategy existed.
public class FixedDivisorAllocationStrategy : ICutoffAllocationStrategy
{
    public decimal AllocateFirstCutoffShare(decimal rate, decimal balance, DeductionPayloadContext context, int divisor, int? daysWorked = null, int? totalDaysInMonth = null)
        => StatutoryHelper.CalcRemainingBalance(rate, balance, divisor, daysWorked, totalDaysInMonth);
}

// Variable-salary employees' pay fluctuates per period, so an even divisor split can
// over-withhold on a low-earning period (absences, mid-period hire) or under-withhold on a
// high one. StatutoryHelper.Get*GrossBaseRate already brackets Variable employees off actual
// gross earned so far this month — at a non-final period that's just this period's own gross
// (plus whatever prior periods this month already posted), so `balance` is already the
// correct amount owed on what was actually earned; a mid-month hire's shorter first period
// is already reflected in that smaller actual gross, so day/divisor proration would
// double-apply the same adjustment — daysWorked/totalDaysInMonth are accepted for interface
// parity but deliberately ignored. Take the balance in full rather than splitting it further;
// the next period re-brackets against the combined month-to-date gross and nets out whatever
// was already withheld (SSSHelper.GetBalance/HDMFHelper.GetBalance/etc.), so a bracket change
// from combining periods' gross is corrected there automatically.
public class VariableActualGrossAllocationStrategy : ICutoffAllocationStrategy
{
    public decimal AllocateFirstCutoffShare(decimal rate, decimal balance, DeductionPayloadContext context, int divisor, int? daysWorked = null, int? totalDaysInMonth = null)
        => balance;
}

public static class CutoffAllocationStrategyFactory
{
    public static ICutoffAllocationStrategy Resolve(SalaryType salaryType) =>
        salaryType == SalaryType.VARIABLE
            ? new VariableActualGrossAllocationStrategy()
            : new FixedDivisorAllocationStrategy();
}
