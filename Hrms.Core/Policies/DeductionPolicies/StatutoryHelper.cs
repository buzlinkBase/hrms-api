using Hrms.Domain.Entities;

namespace Hrms.Core.Policies.DeductionPolicies;

public class StatutoryHelper
{
    public static decimal GetMonthlyGrossBaseRate(DeductionPayloadContext context)
    {
        // Bracket lookup uses the gross already computed upstream by
        // PayrollProcessorService (PayrollLine.GrossIncome) — the same authoritative
        // figure shown on payslips, payroll summary, and reports — rather than an
        // independently-approximated MonthlyRate formula for Fixed employees, which
        // silently omitted rest-day/holiday/ND pay and other components already
        // included in GrossIncome. Monthly has a single cutoff, so no prior-this-month
        // accumulation is needed.
        return context.PayrollLine.GrossIncome;
    }
    public static decimal GetSemiMonthlyGrossBaseRate(DeductionPayloadContext context)
    {
        // Bracket lookup uses actual resolved gross (this cutoff's + prior cutoffs'
        // this month) directly from the payroll line already computed upstream —
        // same basis for Fixed and Variable alike, since PayrollLine.GrossIncome is
        // already correctly computed per salary type by the time deductions run.
        // Fixed employees previously used an independently-approximated MonthlyRate
        // formula here that silently omitted rest-day/holiday/ND pay and other
        // components already included in GrossIncome. Interim cutoffs under-bracket
        // until the month's full gross has accumulated; the balance-netting ledger
        // corrects the running total by the final cutoff.
        if (!context.Payload.PostedPriorPayrolls.TryGetValue(new EmployeeKey(context.Employee.Id), out var payrol)) payrol = new List<Payroll>();
        var prioGross = payrol.Sum(x => x.GrossIncome);
        return context.PayrollLine.GrossIncome + prioGross;
    }
    public static decimal GetWeeklyGrossBaseRate(DeductionPayloadContext context)
    {
        // Same actual-gross-to-date basis as the Semi-Monthly case above, every week
        // rather than only the final one of the month.
        if (!context.Payload.PostedPriorPayrolls.TryGetValue(new EmployeeKey(context.Employee.Id), out var payrol)) payrol = new List<Payroll>();
        var prioGross = payrol.Sum(x => x.GrossIncome);
        return context.PayrollLine.GrossIncome + prioGross;
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

        var actualOtherIncome = payrolls.Sum(x =>
            x.TotalRegularAllowances
          + x.TotalBonuses
          + x.TotalCommissions
          + x.TotalDeminimises
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
            var totalDaysInaMonthAve = context.Payload.CompanyPolicy.TotalDaysInaYear / 12;

            baseRate = context.Employee.DailyRate * Math.Min(workedDays, totalDaysInaMonthAve);
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