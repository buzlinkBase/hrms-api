using Hrms.Core.Calculators.Payloads;
using Hrms.Domain.Entities;

namespace Hrms.Core.Policies.DeductionPolicies;

public class StatutoryHelper
{
    /// <summary>
    /// The income classes that never count toward the SSS / PhilHealth / Pag-IBIG bracket base, for
    /// every payroll frequency: De Minimis (tax- and contribution-exempt benefits), Reimbursement (a
    /// refund of the employee's own expense, not pay) and Special Bonus (13th-month-type bonuses).
    /// Regular, Commission and Others classes do count. The Other Income setup screen shows this
    /// same list -- keep them in step.
    /// </summary>
    public static readonly IReadOnlyList<IncomeClassType> NonStatutoryIncomeClasses =
        [IncomeClassType.Deminimis, IncomeClassType.Reimbursement, IncomeClassType.SpecialBonus];

    /// <summary>A cutoff's gross without the <see cref="NonStatutoryIncomeClasses"/>.</summary>
    public static decimal StatutoryGross(PayrollSummaryLine line) =>
        Math.Max(0, line.GrossIncome - line.TotalDeminimises - line.Reimbursement - line.TotalBonuses);

    /// <summary>A posted payroll's gross without the <see cref="NonStatutoryIncomeClasses"/>.</summary>
    public static decimal StatutoryGross(Payroll payroll) =>
        Math.Max(0, payroll.GrossIncome - payroll.TotalDeminimises - payroll.Reimbursement - payroll.TotalBonuses);

    public static decimal GetMonthlyGrossBaseRate(DeductionPayloadContext context)
    {
        // Bracket lookup uses the gross already computed upstream by
        // PayrollProcessorService (PayrollLine.GrossIncome) — the same authoritative
        // figure shown on payslips, payroll summary, and reports — rather than an
        // independently-approximated MonthlyRate formula for Fixed employees, which
        // silently omitted rest-day/holiday/ND pay and other components already
        // included in GrossIncome. Monthly has a single cutoff, so no prior-this-month
        // accumulation is needed -- except the earnings-only runs of a reliever paid by several
        // per-client runs that month (DeductionCarrierMode): this run carries their statutory
        // deductions, so the month's gross from those runs belongs in the bracket lookup too.
        return StatutoryGross(context.PayrollLine) + GetEarningsOnlyPriorStatutoryGross(context);
    }

    // Gross already paid by this employee's earnings-only (non-carrier) regular runs inside the
    // deduction period this run belongs to (a cutoff of their own payroll group), which exist only
    // under a deduction-carrier company. Zero for everyone else, so ordinary payrolls are
    // unaffected. The carrier run brackets its contributions and tax off this plus its own gross,
    // because those earnings were paid in the same period but in another client's run.
    public static decimal GetEarningsOnlyPriorGross(DeductionPayloadContext context) =>
        EarningsOnlyPriorPayrolls(context).Sum(x => x.GrossIncome);

    // The same earnings-only runs, counted toward the SSS / PhilHealth / Pag-IBIG base (the
    // NonStatutoryIncomeClasses left out). Withholding tax keeps GetEarningsOnlyPriorGross.
    public static decimal GetEarningsOnlyPriorStatutoryGross(DeductionPayloadContext context) =>
        EarningsOnlyPriorPayrolls(context).Sum(StatutoryGross);

    private static IEnumerable<Payroll> EarningsOnlyPriorPayrolls(DeductionPayloadContext context)
    {
        if (!context.Payload.PostedPriorPayrolls.TryGetValue(new EmployeeKey(context.Employee.Id), out var prior))
            return [];
        var period = DeductionPeriodResolver.Resolve(context.Employee.PayrollGroup, context.Payload.ToDate);
        return prior.Where(x => !x.IsDeductionCarrier && x.PayrollType == PayrollType.Regular
            && x.PayPeriodEnd >= period.Start && x.PayPeriodEnd <= period.End);
    }
    public static decimal GetSemiMonthlyGrossBaseRate(DeductionPayloadContext context)
    {
        if (!context.Payload.PostedPriorPayrolls.TryGetValue(new EmployeeKey(context.Employee.Id), out var payrol)) payrol = new List<Payroll>();
        return StatutoryGross(context.PayrollLine) + payrol.Sum(StatutoryGross);
    }
    // Projected whole-month gross for a FIXED employee releasing a full-month statutory
    // contribution before the month is actually finished — MonthlyRate adjusted for
    // attendance-driven deductions already known this period and non-statutory-base income
    // exclusions, matching GetSemiMonthlyGrossBaseRate/GetWeeklyGrossBaseRate's own
    // exclusions. Shared by GetSemiMonthlyBracketBaseRate and GetWeeklyBracketBaseRate — the
    // projection itself doesn't depend on payroll frequency, only which cutoff triggers it.
    private static decimal GetFixedProjectedMonthlyBaseRate(DeductionPayloadContext context) =>
        Math.Max(0, context.Employee.MonthlyRate
            - context.PayrollLine.AbsencesAmount
            - context.PayrollLine.LateAmount
            - context.PayrollLine.UnderTimeAmount
            - context.PayrollLine.UnpaidLeaves
            + context.PayrollLine.TotalAllIncome
            - context.PayrollLine.TotalDeminimises
            - context.PayrollLine.Reimbursement
            - context.PayrollLine.TotalBonuses);

    // FIXED employees know their monthly rate in advance, so on every cutoff except the
    // last one, project the full-month gross from it instead of GetSemiMonthlyGrossBaseRate's
    // "actual gross posted so far" — which on an early/middle cutoff only reflects a fraction
    // of the month's pay and under-brackets a deduction meant to cover the whole month (e.g. a
    // 30,000/month employee's first cutoff only has ~15,000 posted, landing in a lower
    // bracket than the month actually calls for). The LAST cutoff deliberately keeps using the
    // actual accumulated gross instead of the projection — by then every prior cutoff's real
    // attendance is already posted, so the true sum is more accurate than a projection
    // extrapolated from a single period's attendance, and the calculator's own unconditional
    // last-cutoff true-up (see Table*SemiMonthlyCalculator's IsSecondCutoff branch) absorbs
    // whatever drift the earlier cutoffs' projections introduced — so every cutoff, not just
    // the schedule's designated "release everything now" one, brackets correctly against the
    // whole month. VARIABLE employees are left on the actual-to-date basis unchanged — their
    // future cutoffs genuinely aren't known yet, so any shortfall relies on the same
    // last-cutoff true-up instead. A CutoffMismatchException here just means "can't tell yet"
    // — fall back to the normal actual-to-date base rate.
    public static decimal GetSemiMonthlyBracketBaseRate(DeductionPayloadContext context, ICutoffPolicyResolver resolver)
    {
        try
        {
            var oneTimeFund = IsOneTimePayLeave(context);
            if (oneTimeFund)
            {
                return StatutoryGross(context.PayrollLine);
            }

            if (context.Employee.SalaryType == SalaryType.FIXED && !resolver.IsLastCutoff(context))
            {
                return GetFixedProjectedMonthlyBaseRate(context);
            }
        }
        catch (CutoffMismatchException) { }
        return GetSemiMonthlyGrossBaseRate(context);
    }

    // Weekly analog of GetSemiMonthlyBracketBaseRate — same FIXED-projection rationale,
    // applied to every week except the last one (see Table*WeeklyCalculator's unconditional
    // last-week true-up for the other half of this: this only handles the bracket lookup, not
    // the release timing/divisor).
    public static decimal GetWeeklyBracketBaseRate(DeductionPayloadContext context, ICutoffPolicyResolver resolver)
    {
        try
        {
            var oneTimeFund = IsOneTimePayLeave(context);
            if (oneTimeFund)
            {
                return StatutoryGross(context.PayrollLine);
            }
            if (context.Employee.SalaryType == SalaryType.FIXED && !resolver.IsLastCutoff(context))
            {
                return GetFixedProjectedMonthlyBaseRate(context);
            }
        }
        catch (CutoffMismatchException) { }
        return GetWeeklyGrossBaseRate(context);
    }

    public static bool IsOneTimePayLeave(DeductionPayloadContext context)
    {
        if (context.Payload.OneTimeLeavePayouts.TryGetValue(new EmployeeKey(context.Employee.Id), out var apps))
        {
            var currentPayOut = apps.FirstOrDefault(x => x.PayoutMode == PayoutMode.OneTime && (x.ReleasePayrollDate >= context.Payload.FromDate && x.ReleasePayrollDate <= context.Payload.ToDate));
            return currentPayOut != null;
        }
        return false;
    }
    public static decimal GetWeeklyGrossBaseRate(DeductionPayloadContext context)
    {
        // Same actual-gross-to-date basis as the Semi-Monthly case above, every week
        // rather than only the final one of the month.
        var oneTimeFund = IsOneTimePayLeave(context);
        if (oneTimeFund)
        {
            return StatutoryGross(context.PayrollLine);
        }
        if (!context.Payload.PostedPriorPayrolls.TryGetValue(new EmployeeKey(context.Employee.Id), out var payrol)) payrol = new List<Payroll>();
        return StatutoryGross(context.PayrollLine) + payrol.Sum(StatutoryGross);
    }
    public static bool IsHiredThisMonth(DeductionPayloadContext context)
    {
        return context.Employee.HireDate.Year == context.Payload.FromDate.Year &&
               context.Employee.HireDate.Month == context.Payload.FromDate.Month;
    }

    public static bool IsCrossMonh(DeductionPayloadContext context)
    {
        return new IsCrossMonth().IsSatisfiedBy(context.Payload);
    }

    public static bool IsPartialDeduction(DeductionPayloadContext context)
    {
        var hireDate = GetHiredDate(context);
        if (!IsHiredThisMonth(context)) return false;
        //hired this month
        var cutoffResolver = new CutoffPolicyResolver();
        switch (context.Employee.PayrollFrequency)
        {
            case PayrollFrequency.DAILY:
                return true;
            case PayrollFrequency.WEEKLY:
                return !(new IsCrossMonth().IsSatisfiedBy(context.Payload)
                   || context.Payload.FromDate.IsLastWeekOfMonth()
                   || context.Payload.ToDate.IsLastWeekOfMonth());
            case PayrollFrequency.SEMI_MONTHLY:
                return !cutoffResolver.IsFirstCutoff(context);
            case PayrollFrequency.MONTHLY:
                var cutoff = cutoffResolver.GetCurrentCutoff(context);
                var toDate = context.Payload.ToDate;
                var date = new DateOnly(toDate.Year, toDate.Month, cutoff.Day);
                return hireDate.AddDays(15) < date;
            default:
                return false;
        }
    }

    public static DateOnly GetHiredDate(DeductionPayloadContext context) => context.Employee.HireDate;

    public static PayrollProjection GetDailyProjectedGrossRate(DeductionPayloadContext context)
    {
        var oneTimeFund = IsOneTimePayLeave(context);
        if (oneTimeFund)
        {
            var gross = StatutoryGross(context.PayrollLine);
            return new PayrollProjection(gross, 1, gross, gross, gross);
        }

        decimal baseRate = 0m;
        int curDay = context.Payload.FromDate.Day;
        int daysInMonth = context.Payload.FromDate.GetDaysInMonth();
        int divisor = daysInMonth;
        int remainingDays = daysInMonth - curDay;

        // Payroll history
        if (!context.Payload.PostedPriorPayrolls.TryGetValue(new EmployeeKey(context.Employee.Id), out var payrolls))
        {
            payrolls = new List<Payroll>();
        }

        // The statutory income classes only (see NonStatutoryIncomeClasses): no De Minimis,
        // Reimbursement or Special Bonus.
        var actualOtherIncome = payrolls.Sum(x =>
            x.TotalRegularAllowances
          + x.TotalCommissions
          + x.TotalOtherIncome
          + x.OvertimePay
          + x.Cola);

        decimal remainingIncome = 0m;
        if (context.Employee.SalaryType == SalaryType.FIXED)
        {
            baseRate = context.Employee.MonthlyRate;
            // Handle mid-month hires
            if (context.Employee.HireDate.Year == context.Payload.FromDate.Year &&
                context.Employee.HireDate.Month == context.Payload.FromDate.Month &&
                context.Employee.HireDate.Day > 1)
            {
                int workedDays = daysInMonth - context.Employee.HireDate.Day + 1;
                decimal dailyRate = context.Employee.MonthlyRate / daysInMonth;
                baseRate = dailyRate * workedDays;
                divisor = workedDays;
            }
            decimal dailyRateFixed = context.Employee.MonthlyRate / daysInMonth;
            remainingIncome = (dailyRateFixed * remainingDays) - RateDeductions(context);
        }
        else
        {
            // Daily‑rated employees
            int startDay = 1;
            if (context.Employee.HireDate.Year == context.Payload.FromDate.Year &&
                context.Employee.HireDate.Month == context.Payload.FromDate.Month)
            {
                startDay = context.Employee.HireDate.Day;
            }
            var totalAbsent = 0;// (int)payrolls.Sum(x => x.AbsentCount);
            int workedDays = daysInMonth - totalAbsent - startDay + 1;
            //var totalDaysInaMonthAve =  context.Payload.CompanyPolicy.TotalDaysInaYear / 12;
            baseRate = context.Employee.DailyRate * Math.Min(workedDays, 26);
            divisor = workedDays;
            remainingIncome = (context.Employee.DailyRate * remainingDays);
        }

        // Projected Gross = Actual + Remaining
        decimal projectedGross = actualOtherIncome + remainingIncome;

        return new PayrollProjection(baseRate, divisor, actualOtherIncome, remainingIncome, projectedGross);
    }

    public static decimal CalcRemainingBalance(
       decimal rate,
       decimal balance,
       int divisor,
       int? daysWorked = null,
       int? totalDaysInMonth = null)
    {
        if (balance <= 0) return 0m;

        decimal perInstance;

        if (daysWorked.HasValue && totalDaysInMonth.HasValue && totalDaysInMonth.Value > 0)
        {
            // Clamp daysWorked to avoid overshoot
            int effectiveDays = Math.Min(daysWorked.Value, totalDaysInMonth.Value);

            // Prorated contribution for new hires
            perInstance = rate * effectiveDays / totalDaysInMonth.Value;
        }
        else
        {
            // Regular divisor-based split (weekly/semi-monthly/monthly)
            if (divisor <= 0) divisor = 1; // safety fallback
            perInstance = rate / divisor;
        }

        // Deduction should not exceed remaining balance
        return Math.Min(perInstance, balance);
    }
    public static decimal GetProratedContribution(decimal monthlyRate, DateTime hireDate, DateTime periodEnd)
    {
        int totalDaysInMonth = DateTime.DaysInMonth(hireDate.Year, hireDate.Month);

        // Days worked = from hire date until end of period (inclusive)
        int daysWorked = (periodEnd - hireDate).Days + 1;

        // Prorated contribution
        return monthlyRate * daysWorked / totalDaysInMonth;
    }
    //UTILS
    private static decimal RateDeductions(DeductionPayloadContext context)
    {
        //deduct absenses for prior months
        //only deduct from fix_monthly since variable already deducted with absenses
        if (context.Employee.SalaryType != SalaryType.FIXED) return 0;
        var absences = GetAbsencesTotal(context);
        var lateUt = GetLateUTAmount(context);
        var lwop = GetLWOP(context);//TODO if leave is embeeded in salary
        return absences + lateUt + lwop;
    }
    private static decimal GetAbsencesTotal(DeductionPayloadContext context)
    {
        if (!context.Payload.PostedPriorPayrolls.TryGetValue(new EmployeeKey(context.Employee.Id), out var payrol)) payrol = new List<Payroll>();
        var PostedAbsent = payrol.Sum(x => x.AbsencesAmount);
        //current
        if (!context.Payload.CompanyPolicy.ApplyStatutoryOnActualMonth)
        {
            return context.PayrollLine.TimeHourPayResults.Sum(x => x.AbsentAmount) + PostedAbsent;
        }

        //TODO add up here the data already in the db outside the range with same month as the from date
        //return context.PayrollLine.TimeHourPayResults
        //     .Where(x => x.AbsentAmount.PayrollDate.Month == context.Payload.FromDate.Month && x.AbsentInfo.PayrollDate.Year <= context.Payload.FromDate.Year)
        //     .Sum(x => x.AbsentInfo.Amount)
        //     + PostedAbsent;

        return context.PayrollLine.TimeHourPayResults.Sum(x => x.AbsentAmount) + PostedAbsent;

    }
    private static decimal GetLWOP(DeductionPayloadContext context)
    {
        if (!context.Payload.PostedPriorPayrolls.TryGetValue(new EmployeeKey(context.Employee.Id), out var payrol))
        {
            payrol = new List<Payroll>();
        }

        var setting = context.Employee.Settings ?? new EmployeeSettingModel();
        if (context.Employee.SalaryType != SalaryType.FIXED) return 0;

        var prioLwop = payrol.Sum(x => x.UnpaidLeaves);
        return context.PayrollLine.TimeHourPayResults.Sum(x => x.UnpaidLeave) + prioLwop;

        //if (!context.Payload.CompanyPolicy.ApplyStatutoryOnActualMonth)
        //    return context.PayrollLine.BasicSalaryItems.Sum(x => x.LWOP) + prioLwop;
        //TODO add logic  for LWOP deduction here
        //return 0;

    }
    private static decimal GetLateUTAmount(DeductionPayloadContext context)
    {
        if (!context.Payload.PostedPriorPayrolls.TryGetValue(new EmployeeKey(context.Employee.Id), out var payrol)) payrol = new List<Payroll>();
        var priorLates = payrol.Sum(x => x.LateAmount + x.UnderTimeAmount);

        //if (!context.Payload.CompanyPolicy.ApplyStatutoryOnActualMonth)
        return context.PayrollLine.TimeHourPayResults.Sum(x => x.LateAmount + x.UTAmount) + priorLates;

        // //TODO add up here the data already in the db outside the range with same month as the from date
        // var late = context.PayrollLine.BasicSalaryItems
        //  .Where(x => x.LateHourInfo.PayrollDate.Month == context.Payload.FromDate.Month && x.LateHourInfo.PayrollDate.Year <= context.Payload.FromDate.Year)
        //  .Sum(x => x.AbsentInfo.Amount);

        // var ut = context.PayrollLine.BasicSalaryItems
        //.Where(x => x.UTHourInfo.PayrollDate.Month == context.Payload.FromDate.Month && x.UTHourInfo.PayrollDate.Year <= context.Payload.FromDate.Year)
        //.Sum(x => x.AbsentInfo.Amount);

        // return late + ut + priorLates;
    }
}
public record PayrollProjection(
    decimal BaseRate,
    int Divisor,
    decimal ActualPayroll,
    decimal RemainingIncome,
    decimal ProjectedGross
);