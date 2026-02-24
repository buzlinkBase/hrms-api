namespace Hrms.Core.Policies.DTRPolicies;

internal class RestDayPolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
    {
        if (!context.Payload.PremiumRates.TryGetValue(RateType.RESTDAY_DUTY, out var premiumRate))
        {
            premiumRate = RATE_DEFAULT.RESTDAY_DUTY;
        }
        var dailyRate = context.Employee.DailyRate;
        var hourlyRate = dailyRate / context.DailyRecord.ShiftWorkingHour;
        var restDayHours = (decimal)context.DailyRecord.RestDayHours;

        //for fixed emp just get the premium pay
        var result = context.Employee.SalaryType == SalaryType.MONTHLY_FIXED
                   ? hourlyRate * restDayHours * (premiumRate > 1 ? premiumRate - 1 : 0.30m)
                   : hourlyRate * restDayHours * premiumRate
                   ;
        line.Value += result;
        return line;
    }
}