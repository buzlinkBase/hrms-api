using System.Globalization;
using Hrms.Domain;

namespace Hrms.Core.Services.Compliance;

// An employee's pay setup as one save would leave it. Null parts weren't sent (a caller without
// pay clearance, or settings saved on their own) and are not evaluated. Acknowledged once per
// company per setting and value -- not per employee -- so the next employee with the same choice
// saves without asking again.
public sealed record EmployeePaySubject(
    ComputationBasis? Sss = null,
    ComputationBasis? PhilHealth = null,
    ComputationBasis? PagIbig = null,
    ComputationBasis? WithholdingTax = null,
    EmployeeEligibility? Eligibility = null,
    decimal DailyRate = 0,
    decimal? RegionalMinimumDailyRate = null);

public sealed record EmployeeEligibility(
    bool Overtime, bool RegularHolidayPay, bool NightDifferential, bool LeaveCredits, bool ThirteenthMonth);

public sealed class StatutoryContributionMethodRule : IStatutoryDeviationRule<EmployeePaySubject>
{
    public IEnumerable<StatutoryDeviation> Evaluate(EmployeePaySubject subject)
    {
        var methods = new (string Key, string Label, ComputationBasis? Method, string Law)[]
        {
            ("employee.sss.method", "SSS contribution method", subject.Sss, "RA 11199"),
            ("employee.philhealth.method", "PhilHealth contribution method", subject.PhilHealth, "RA 11223"),
            ("employee.pagibig.method", "Pag-IBIG contribution method", subject.PagIbig, "RA 9679"),
            ("employee.wtax.method", "Withholding tax method", subject.WithholdingTax, "the Tax Code"),
        };
        foreach (var (key, label, method, law) in methods)
        {
            if (method is null or ComputationBasis.Table) continue;
            yield return new StatutoryDeviation(
                key, label, nameof(ComputationBasis.Table), method.Value.ToString(),
                method == ComputationBasis.None
                    ? $"Nothing is deducted or remitted for this employee, although {law} requires it unless the employee is exempt."
                    : $"A fixed amount instead of the table may deduct or remit less than {law} requires.");
        }
    }
}

public sealed class BenefitEligibilityRule : IStatutoryDeviationRule<EmployeePaySubject>
{
    public IEnumerable<StatutoryDeviation> Evaluate(EmployeePaySubject subject)
    {
        if (subject.Eligibility is not { } e) yield break;
        var benefits = new (string Key, string Label, bool Eligible, string Law)[]
        {
            ("employee.eligibility.overtime", "Overtime pay", e.Overtime, "Labor Code Art. 87"),
            ("employee.eligibility.regular-holiday", "Regular holiday pay", e.RegularHolidayPay, "Labor Code Art. 94"),
            ("employee.eligibility.night-differential", "Night differential", e.NightDifferential, "Labor Code Art. 86"),
            ("employee.eligibility.leave-credits", "Leave credits (SIL)", e.LeaveCredits, "Labor Code Art. 95"),
            ("employee.eligibility.13th-month", "13th month pay", e.ThirteenthMonth, "PD 851"),
        };
        foreach (var (key, label, eligible, law) in benefits)
        {
            if (eligible) continue;
            yield return new StatutoryDeviation(
                key, label, "Eligible", "Not eligible",
                $"Only employees {law} exempts (e.g. managerial staff, field personnel) may be excluded.");
        }
    }
}

public sealed class BelowMinimumWageRule : IStatutoryDeviationRule<EmployeePaySubject>
{
    public IEnumerable<StatutoryDeviation> Evaluate(EmployeePaySubject subject)
    {
        if (subject.DailyRate <= 0 || subject.RegionalMinimumDailyRate is not { } minimum || subject.DailyRate >= minimum) yield break;
        yield return new StatutoryDeviation(
            "employee.below-minimum-wage",
            "Daily rate below the regional minimum wage",
            minimum.ToString("0.00", CultureInfo.InvariantCulture),
            subject.DailyRate.ToString("0.00", CultureInfo.InvariantCulture),
            "Below the wage order for the employee's branch region, unless the establishment is exempted.");
    }
}

// A time shift as saved.
public sealed record TimeShiftSubject(
    double OverTimeThreshold, double? MaxOvertimeHours, double? NightDiffThreshold,
    BreakMode AmBreak, BreakMode PmBreak);

public sealed class TimeShiftRule : IStatutoryDeviationRule<TimeShiftSubject>
{
    public IEnumerable<StatutoryDeviation> Evaluate(TimeShiftSubject subject)
    {
        if (subject.OverTimeThreshold > 0)
            yield return new StatutoryDeviation(
                "shift.ot-threshold", "Minimum OT before it counts", "0 minutes",
                $"{Number(subject.OverTimeThreshold)} minutes",
                "Authorized overtime shorter than the threshold is not paid (Labor Code Art. 87).");
        if (subject.MaxOvertimeHours is > 0)
            yield return new StatutoryDeviation(
                "shift.ot-cap", "Overtime cap", "No cap",
                $"{Number(subject.MaxOvertimeHours.Value)} hours",
                "Authorized overtime beyond the cap is not paid.");
        if (subject.NightDiffThreshold is > 0)
            yield return new StatutoryDeviation(
                "shift.nd-threshold", "Night differential threshold", "0 minutes",
                $"{Number(subject.NightDiffThreshold.Value)} minutes",
                "Every hour worked from 10 PM to 6 AM earns the night differential (Labor Code Art. 86).");
        if (subject.AmBreak == BreakMode.UNPAID_BREAK || subject.PmBreak == BreakMode.UNPAID_BREAK)
            yield return new StatutoryDeviation(
                "shift.unpaid-short-break", "Short (AM/PM) break", nameof(BreakMode.PAID_BREAK), nameof(BreakMode.UNPAID_BREAK),
                "Short rest periods are counted as hours worked (IRR Book III, Rule I, Sec. 7).");
    }

    private static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}

// A leave type as saved. Only Service Incentive Leave has a statutory minimum the type can fall below.
public sealed record LeaveSubject(
    string Code, AccrualBasis AccrualBasis, double Credits, double AccrualRate, int MinServiceMonths,
    bool ConvertToCash, decimal CashConversionRate);

public sealed class ServiceIncentiveLeaveRule : IStatutoryDeviationRule<LeaveSubject>
{
    public const string SilCode = "SIL";
    public const double StatutoryDaysPerYear = 5;
    public const int StatutoryServiceMonths = 12;

    public IEnumerable<StatutoryDeviation> Evaluate(LeaveSubject subject)
    {
        if (!string.Equals(subject.Code, SilCode, StringComparison.OrdinalIgnoreCase)) yield break;

        var daysPerYear = DaysPerYear(subject);
        // A small tolerance: a monthly rate of 5/12 adds back up to 4.999... days.
        if (daysPerYear < StatutoryDaysPerYear - 0.01)
            yield return new StatutoryDeviation(
                "leave.sil.days", "Service Incentive Leave days per year",
                StatutoryDaysPerYear.ToString("0.##", CultureInfo.InvariantCulture),
                daysPerYear.ToString("0.##", CultureInfo.InvariantCulture),
                "Labor Code Art. 95 grants 5 days of SIL a year.");
        if (subject.MinServiceMonths > StatutoryServiceMonths)
            yield return new StatutoryDeviation(
                "leave.sil.service", "Service Incentive Leave required service",
                $"{StatutoryServiceMonths} months", $"{subject.MinServiceMonths} months",
                "SIL is earned after one year of service.");
        if (!subject.ConvertToCash || subject.CashConversionRate < 1)
            yield return new StatutoryDeviation(
                "leave.sil.conversion", "Unused Service Incentive Leave",
                "Converted to cash at 100%",
                subject.ConvertToCash ? $"Converted at {(subject.CashConversionRate * 100).ToString("0.##", CultureInfo.InvariantCulture)}%" : "Not converted",
                "Unused SIL is commutable to its money equivalent.");
    }

    private static double DaysPerYear(LeaveSubject s) => s.AccrualBasis switch
    {
        AccrualBasis.Monthly => s.AccrualRate * 12,
        AccrualBasis.Annually => s.AccrualRate,
        AccrualBasis.PerPayPeriod => s.AccrualRate * 24,
        _ => s.Credits,
    };
}
