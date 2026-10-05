using Hrms.Core.Calculators.DtrPay;

namespace Hrms.Core.Policies.DTRPolicies;

internal class RestDayPolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
    {
        var dailyRecord = context.DailyRecord;
        var hours = DayTypeHours.For(context, dailyRecord.RestDayHours, dailyRecord.RestDayNDHours, dailyRecord.RestDayNDOTHours);
        // Additive pays the natural hours (night hours included) at this day type's rate, so night
        // differential only adds its premium; Compounded pays the daytime hours only (DayTypeHours).
        return PayHours(line, context, hours.PaidHours(context, dailyRecord.RestDayHours));
    }

    // This day type's pay for the hours it pays at its own rate: holiday eligibility,
    // Fixed-salary pre-funding, unworked = shift - paid.
    private static BasicPipelineData PayHours(BasicPipelineData line, PayrollContext context, decimal paidHours)
    {
        var dailyRecord = context.DailyRecord;
        if (dailyRecord.ShiftWorkingHour <= 0 || paidHours <= 0)
        {
            return line;
        }

        var totalRateMultiplier = PremiumRateHelper.GetRate(context, RateType.RESTDAY_DUTY, RATE_DEFAULT.RESTDAY_DUTY);
        var hourlyRate = RateHelper.GetHourlyRate(context);
        var restDayHours = paidHours;

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