namespace Hrms.Core.Policies.DTRPolicies;

public class UndertimePolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
    {
        var dailyRecord = context.DailyRecord;
        if (dailyRecord.ShiftWorkingHour <= 0 || dailyRecord.LateMinutes <= 0)
        {
            return line;
        }
        var hr = (decimal)context.DailyRecord.UTMinutes / 60m;
        var hrRate = context.Employee.DailyRate / (decimal)context.DailyRecord.ShiftWorkingHour;
        line.UTInfo = new UnderTimeInfo
        {
            PayrollDate = context.PayrollDate,
            Hour = hr,
            Amount = hrRate * hr
        };
        line.Value += hrRate * hr;
        return line;
    }
}
