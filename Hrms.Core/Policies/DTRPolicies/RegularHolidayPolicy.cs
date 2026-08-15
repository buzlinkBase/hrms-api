using Hrms.Core;

public class RegularHolidayPolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    public RegularHolidayPolicy()
    {
    }

    public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
    {
        var premiumRate = RegularHolRateSolver.ResolvePremiumRate(context);
        var dailyRate = RateHelper.GetDailyRate(context);
        var hourlyRate = RateHelper.GetHourlyRate(context);
        var workedHours = (decimal)context.DailyRecord.LegalHolHours;
        var shiftHours = (decimal)context.DailyRecord.ShiftWorkingHour;
        var holidayCredit = GetHolidayCreditDays(context);

        bool IsElible = new IsEligibleForHolidayPay().IsSatisfiedBy(context);

        if (IsElible)
        {
            //TODO if fixed get only the premium
            workedHours += holidayCredit * shiftHours;
        }
        if (premiumRate > 1)
        {
            premiumRate -= 1;
        }

        var holDay = (workedHours / shiftHours) >= 1 ? 1 : 0;
        var remainder = Math.Max((workedHours / shiftHours) - 1, 0);
        line.Value += holDay * dailyRate;
        line.Value += remainder * dailyRate * premiumRate;
        return line;

    }

    private decimal GetHolidayCreditDays(PayrollContext context)
    {
        //if (context.Employee.SalaryType != SalaryType.MONTHLY_FIXED)
        //{
        //    return (decimal)context.DailyRecord.HolCount;
        //}
        ///Fixed Employee is already paid LH premium
        //if (context.Employee.SalaryType == SalaryType.MONTHLY_FIXED) return 0;
        //if eligible HolCount>0
        bool IsElible = new IsEligibleForHolidayPay().IsSatisfiedBy(context);
        if (!IsElible) return 0;
        //var workedHours = 0;
        //var holidayCredit = context.DailyRecord.HolCount;
        //if (context.Payload.CompanyPolicy.HolidayCreditPolicy == HolidayCreditMode.NoCredit && IsElible)
        //{
        //    //TODO if fixed get only the premium
        //    workedHours += holidayCredit * shiftHours;
        //}
        return (decimal)context.DailyRecord.HolCount;
    }
}

public static class RegularHolRateSolver
{
    private static readonly List<RateRule> _rules = new()
    {
        // Pure Legal Holiday Duty
        new RateRule
        {
            Condition = ctx => new IsRegularHolidayDuty().IsSatisfiedBy(ctx),
            GetRate = ctx => PremiumRateHelper.GetRate(ctx, RateType.LEGAL_HOLIDAY_DUTY, RATE_DEFAULT.LEGAL_HOLIDAY_DUTY),
        },

        // Rest Day + Legal Holiday Duty
        new RateRule
        {
            Condition = ctx => new IsRestDayLegalHolidayDuty().IsSatisfiedBy(ctx),
            GetRate = ctx =>
            {
                return
                    PremiumRateHelper.GetRate(ctx, RateType.RESTDAY_DUTY, RATE_DEFAULT.RESTDAY_DUTY) *
                    PremiumRateHelper.GetRate(ctx, RateType.LEGAL_HOLIDAY_DUTY, RATE_DEFAULT.LEGAL_HOLIDAY_DUTY);
            },
        },
        new RateRule
        {
            Condition = ctx => new IsRegularHoliday().IsSatisfiedBy(ctx),
            GetRate = ctx =>
            {
                return
                    PremiumRateHelper.GetRate(ctx, RateType.RESTDAY_DUTY, RATE_DEFAULT.RESTDAY_DUTY) *
                    PremiumRateHelper.GetRate(ctx, RateType.LEGAL_HOLIDAY, RATE_DEFAULT.LEGAL_HOLIDAY);
            },
        },
    };

    public static decimal ResolvePremiumRate(PayrollContext context)
    {
        foreach (var rule in _rules)
        {
            if (rule.Condition(context))
                return rule.GetRate(context);
        }
        return 1.00m; // default multiplier (no premium)
    }
}