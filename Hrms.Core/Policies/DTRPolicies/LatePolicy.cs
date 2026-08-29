namespace Hrms.Core.Policies.DTRPolicies;

public class LatePolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
    {
        var dailyRecord = context.DailyRecord;

        if (dailyRecord.ShiftWorkingHour <= 0 || dailyRecord.LateMinutes <= 0)
        {
            return line;
        }

        var hoursLate = (decimal)dailyRecord.LateMinutes / 60m;
        var hourlyRate = RateHelper.GetHourlyRate(context);

        // Add late monetary value to the pipeline output accumulator
        line.Value += hourlyRate * hoursLate;

        return line;
    }
}