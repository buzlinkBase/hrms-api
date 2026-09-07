namespace Hrms.Core.Policies.DeductionPolicies;

// Strategy (GoF) for how much of a statutory component's outstanding monthly balance a
// non-final cutoff/week/period should withhold. The last cutoff of the month never uses
// this — it always takes the exact remaining balance (see the Table*Calculator classes),
// which is already correct-by-construction regardless of how earlier periods split things.
// Selected per employee via CutoffAllocationStrategyFactory. Generic over SSS/HDMF/PHIC —
// each component (EE/ER/EC) is allocated with one call — and over Semi-Monthly/Weekly/
// Monthly, since all three feed it the same (rate, balance, divisor) shape.
//
// Fixed and Variable salary types resolve to the same strategy (see the factory below) —
// a PayrollGroup's cutoff-allocation policy (the default even 50/50 split, or
// FirstHalfMonth/SecondHalfMonth's "release everything on one cutoff") is an HR scheduling
// decision that applies uniformly regardless of salary type. Only the BRACKET LOOKUP basis
// still differs by salary type (StatutoryHelper.GetSemiMonthlyBracketBaseRate/
// GetWeeklyBracketBaseRate: Fixed projects the full month from MonthlyRate, Variable brackets
// off actual gross earned so far) — that's a separate, still-correct distinction this
// strategy doesn't touch.
public interface ICutoffAllocationStrategy
{
    decimal AllocateFirstCutoffShare(decimal rate, decimal balance, DeductionPayloadContext context, int divisor, int? daysWorked = null, int? totalDaysInMonth = null);
}

// Splits the outstanding balance evenly across the divisor (the PayrollGroup's configured
// cutoff count, or 1 when the schedule releases everything on this cutoff), capped at
// whatever's actually still owed. daysWorked/totalDaysInMonth (when the caller is prorating
// a mid-month hire's first period) pass straight through to CalcRemainingBalance.
public class FixedDivisorAllocationStrategy : ICutoffAllocationStrategy
{
    public decimal AllocateFirstCutoffShare(decimal rate, decimal balance, DeductionPayloadContext context, int divisor, int? daysWorked = null, int? totalDaysInMonth = null)
        => StatutoryHelper.CalcRemainingBalance(rate, balance, divisor, daysWorked, totalDaysInMonth);
}

public static class CutoffAllocationStrategyFactory
{
    // salaryType is intentionally unused today — kept as the resolution key so a future
    // policy that genuinely needs to split by salary type doesn't require another factory
    // signature change. See the interface doc comment above for why Fixed and Variable
    // aren't differentiated here.
    public static ICutoffAllocationStrategy Resolve(SalaryType salaryType) => new FixedDivisorAllocationStrategy();
}
