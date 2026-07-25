namespace Hrms.Core.Policies.DTRPolicies;

public class LatePolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
    {
        var hr = (decimal)context.DailyRecord.LateMinutes;
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
