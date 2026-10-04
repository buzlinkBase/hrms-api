using Hrms.Core.Calculators.DtrPay;

namespace Hrms.Core.Policies.DTRPolicies;

internal class RestDayPolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
    {
        var dailyRecord = context.DailyRecord;
        var hours = DayTypeHours.For(context, dailyRecord.RestDayHours, dailyRecord.RestDayNDHours, dailyRecord.RestDayNDOTHours);
        // Plain hours keep this day type's own rules below; under Additive the night hours inside
        // RestDayHours are then added at the full day rate (DayTypeHours). Night is added after the
        // plain pay because the plain pay sets line.Worked rather than adding to it.
        return hours.AddNightPay(PayPlainHours(line, context, hours.Plain), context, PayCategories.RestDay);
    }

    // This day type's pay for its plain (daytime) hours -- unchanged from before the DTR stored
    // natural hours: holiday eligibility, Fixed-salary pre-funding, unworked = shift - plain.
    private static BasicPipelineData PayPlainHours(BasicPipelineData line, PayrollContext context, decimal plainHours)
    {
        var dailyRecord = context.DailyRecord;
        if (dailyRecord.ShiftWorkingHour <= 0 || plainHours <= 0)
        {
            return line;
        }

        var totalRateMultiplier = PremiumRateHelper.GetRate(context, RateType.RESTDAY_DUTY, RATE_DEFAULT.RESTDAY_DUTY);
        var hourlyRate = RateHelper.GetHourlyRate(context);
        var restDayHours = plainHours;

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