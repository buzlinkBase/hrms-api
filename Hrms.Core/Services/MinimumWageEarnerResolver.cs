using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

// Shared Minimum-Wage-Earner (MWE) detection — extracted from PayrollReportService so both
// the monthly BIR 1601-C remittance report and Year-End Tax Annualization (TaxAnnualizationService)
// resolve MWE status the exact same way, rather than maintaining two copies of this logic.
// Pure, static, no DB access — testable without a database (see
// hrms.test\PayrollRunTests\MonthlyRemittanceReturnSummaryTests.cs).
public static class MinimumWageEarnerResolver
{
    // MWE status is assessed from the employee's most recent Regular-type payroll row in the
    // rows given, against the region rate effective as of that same row's PostingPeriod — not
    // stored on Employee, so a later wage-rate change never retroactively reclassifies an
    // already-filed/already-annualized period. Callers must pass ONLY PayrollType.Regular rows
    // (ThirteenthMonth/LastPay/YearEndAdjustment rows have DailyRate == 0 and would corrupt the
    // "latest row" pick).
    //
    // IsUnclassified: true when the employee has payroll data to check but no Branch/Region/
    // MinimumWageRate to check it against — defaulted to non-MWE, but flagged so it's never
    // silent (mirrors MonthlyRemittanceReturnEmployeeModel.IsUnclassified /
    // TaxAnnualizationInputModel.IsUnclassified).
    public static (bool IsMinimumWageEarner, bool IsUnclassified) IsMinimumWageEarner(
        List<Payroll> regularRowsInScope, string? regionCode, string? wageOrderClass,
        List<MinimumWageRate> minimumWageRates)
    {
        var latestRow = regularRowsInScope.OrderByDescending(x => x.PostingPeriod).FirstOrDefault();
        var regionRate = latestRow != null
            ? ResolveRegionRate(minimumWageRates, regionCode, wageOrderClass, latestRow.PostingPeriod)
            : null;
        var isUnclassified = latestRow != null && regionRate == null;
        var isMWE = latestRow != null && regionRate.HasValue && latestRow.DailyRate <= regionRate.Value;
        return (isMWE, isUnclassified);
    }

    // Pure lookup over an already-fetched rate list. Wage orders often set different rates
    // within the same region depending on the establishment's registered sector/class
    // (Branch.WageOrderClass) — tries an exact class match first (including both-null, the
    // legacy/general-rate case), then falls back to the region's class-less rate if the
    // branch's specific class has no rate of its own configured, so setting a class on a
    // branch can never make a previously working region-only setup regress to Unclassified.
    public static decimal? ResolveRegionRate(
        List<MinimumWageRate> minimumWageRates, string? regionCode, string? wageOrderClass, DateOnly asOf)
    {
        if (string.IsNullOrEmpty(regionCode)) return null;
        var normalizedClass = string.IsNullOrEmpty(wageOrderClass) ? null : wageOrderClass;
        var candidates = minimumWageRates.Where(r => r.RegionCode == regionCode && r.EffectiveDate <= asOf);

        var exact = candidates
            .Where(r => (string.IsNullOrEmpty(r.WageOrderClass) ? null : r.WageOrderClass) == normalizedClass)
            .OrderByDescending(r => r.EffectiveDate)
            .Select(r => (decimal?)r.DailyRate)
            .FirstOrDefault();
        if (exact != null) return exact;
        if (normalizedClass == null) return null;

        return candidates
            .Where(r => string.IsNullOrEmpty(r.WageOrderClass))
            .OrderByDescending(r => r.EffectiveDate)
            .Select(r => (decimal?)r.DailyRate)
            .FirstOrDefault();
    }
}
