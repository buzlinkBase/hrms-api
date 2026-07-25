namespace Hrms.Core.Policies.DTRPolicies;

internal class RegularWorkDayPolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    public RegularWorkDayPolicy() { }
    public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
    {
        var dailyRate = context.Employee.DailyRate;
        var hourlyRate = dailyRate / (decimal)context.DailyRecord.ShiftWorkingHour;
        var regularHours = (decimal)context.DailyRecord.RegularNetHours;
        line.Value += hourlyRate * regularHours;
        return line;
    }
}
