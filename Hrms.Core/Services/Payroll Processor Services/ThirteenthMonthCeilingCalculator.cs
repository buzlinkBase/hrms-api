namespace Hrms.Core.Services;

// Pure math shared by ThirteenthMonthPayrollService and LastPayrollService for splitting a
// lump-sum 13th-month-style payout against the annual tax exemption ceiling (PD 851 / TRAIN
// law). No dependencies, never constructor-injected, so this is plain static — not subject to
// the DI auto-registration convention's "*Service" naming requirement (see
// LibServicesRegistrations.cs).
public static class ThirteenthMonthCeilingCalculator
{
    // Splits a 13th month gross into the non-taxable portion (up to the exemption ceiling)
    // and the taxable excess above it, per TRAIN law. `internal` so it's directly unit
    // testable without a database — see ThirteenthMonthPayrollService.GenerateAsync/
    // LastPayrollService.GenerateAsync.
    internal static (decimal NonTaxable, decimal Taxable) ComputeThirteenthMonthTaxSplit(decimal gross, decimal ceiling)
    {
        var nonTaxable = Math.Min(gross, ceiling);
        var taxable = Math.Max(0, gross - ceiling);
        return (nonTaxable, taxable);
    }

    // Per Payroll Settings: the exemption ceiling covers 13th month pay + Special Bonuses
    // combined, not 13th month pay alone — whatever ceiling room Special Bonuses already paid
    // this year consumed isn't available to the 13th month payout. `internal` so it's directly
    // unit testable without a database.
    internal static decimal ComputeRemainingThirteenthMonthCeiling(decimal ceiling, decimal priorSpecialBonuses) =>
        Math.Max(0, ceiling - priorSpecialBonuses);
}
