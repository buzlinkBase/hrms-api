namespace Hrms.Core.Policies.DTRPolicies;

internal class RestDayPolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
    {
        var dailyRecord = context.DailyRecord;
        if (dailyRecord.ShiftWorkingHour <= 0 || dailyRecord.RestDayHours <= 0)
        {
            return line;
        }

        var totalRateMultiplier = PremiumRateHelper.GetRate(context, RateType.RESTDAY_DUTY, RATE_DEFAULT.RESTDAY_DUTY);
        var hourlyRate = RateHelper.GetHourlyRate(context);
        var restDayHours = (decimal)dailyRecord.RestDayHours;

        // Fixed with IsRestDayPaid = true: base rest day rate is pre-funded, so only the
        // premium delta above 1.0x is paid. There's no unworked-rest-day benefit, so
        // CalculateUnworkedPay is never invoked — rest day pay only applies to hours worked.
        bool isBasePayPreFunded = context.Employee.SalaryType == SalaryType.FIXED
            && context.Employee.IsRestDayPaid;

        line.Value += DayTypePayCalculator
            .For(isEligible: true, isBasePayPreFunded, totalRateMultiplier, unworkedMultiplier: 0m)
            .CalculateWorkedPay(hourlyRate, restDayHours)
            .Total;

        return line;
    }
}