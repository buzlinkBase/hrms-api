namespace Hrms.Domain.Entities;

// Regional Daily Minimum Wage per a DOLE Wage Order — used to auto-derive an employee's
// Minimum-Wage-Earner status for BIR Form 1601-C (see
// PayrollReportService.GetMonthlyRemittanceReturnAsync), by comparing a payroll row's own
// DailyRate against the rate effective for the employee's Branch.RegionCode as of that period.
public class MinimumWageRate : BaseEntity
{
    public string RegionCode { get; set; } = string.Empty;
    public string RegionName { get; set; } = string.Empty;
    public decimal DailyRate { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public string? WageOrderNo { get; set; }
    // Sector/class this rate applies to within the region (e.g. "Non-Agriculture",
    // "Retail/Service establishments employing 10 workers or less") — a single wage order
    // often sets different rates per class within the same region. Null = the general rate
    // applying to any class in this region (legacy/default — see
    // PayrollReportService.ResolveRegionRate's fallback).
    public string? WageOrderClass { get; set; }
}
