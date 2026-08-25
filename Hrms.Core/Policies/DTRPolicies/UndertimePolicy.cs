namespace Hrms.Core.Policies.DTRPolicies;

public class UndertimePolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
    {
        var dailyRecord = context.DailyRecord;
        // Fixed guard clause to check UTMinutes instead of LateMinutes
        if (dailyRecord.ShiftWorkingHour <= 0 || dailyRecord.UTMinutes <= 0)
        {
            return line;
        }
        var undertimeHours = (decimal)dailyRecord.UTMinutes / 60m;
        var hourlyRate = RateHelper.GetHourlyRate(context);
        line.Value += hourlyRate * undertimeHours;
        return line;
    }
}