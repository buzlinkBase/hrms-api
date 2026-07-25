namespace Hrms.Core.Policies.DTRPolicies;

public class UndertimePolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
    {
        var workHour = 8.0m;
        var hr = (decimal)context.DailyRecord.UTMinutes / workHour;
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
