namespace Hrms.Core.Policies.DTRPolicies;

internal class RestDayPolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
    {
        if (context.DailyRecord.RestDayHours == 0 || context.DailyRecord.ShiftWorkingHour <= 0)
            return line;


        var dailyRecord = context.DailyRecord;
        var employee = context.Employee;

        // Guard against non-working or zero-hour entries
        if (dailyRecord.ShiftWorkingHour <= 0 || dailyRecord.RestDayHours <= 0)
        {
            return line;
        }

        var totalRateMultiplier = PremiumRateHelper.GetRate(context, RateType.RESTDAY_DUTY, RATE_DEFAULT.RESTDAY_DUTY);
        var hourlyRate = RateHelper.GetHourlyRate(context);
        var restDayHours = (decimal)dailyRecord.RestDayHours;

        // Check if base rest day rate is pre-funded (Fixed with IsRestDayPaid = true)
        bool isBasePayPreFunded = employee.SalaryType == SalaryType.FIXED 
            && employee.IsRestDayPaid;

        // If pre-funded, pay only the premium delta above 1.0 (e.g., 1.30 - 1.00 = 0.30)
        // If not pre-funded (Daily/Variable or IsRestDayPaid = false), pay the full rate multiplier
        var effectiveMultiplier = isBasePayPreFunded
            ? Math.Max(0m, totalRateMultiplier - 1.0m)
            : totalRateMultiplier;

        line.Value += hourlyRate * restDayHours * effectiveMultiplier;
        return line;

    }
}