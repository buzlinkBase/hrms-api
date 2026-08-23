using Hrms.Domain.Entities;

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

        var hr = (decimal)context.DailyRecord.LateMinutes * 60;
        var hrRate = context.Employee.DailyRate / (decimal)context.DailyRecord.ShiftWorkingHour;
        line.LateInfo = new LateInfo
        {
            PayrollDate = context.PayrollDate,
            Hour = hr,
            Amount = hrRate * hr
        };
        line.Value += hrRate * hr;
        return line;
    }
}
