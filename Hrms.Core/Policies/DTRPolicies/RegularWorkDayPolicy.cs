namespace Hrms.Core.Policies.DTRPolicies;

internal class RegularWorkDayPolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    public RegularWorkDayPolicy() { }
    public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
    {
        var dailyRecord = context.DailyRecord;
        var employee = context.Employee;
        if (dailyRecord.ShiftWorkingHour <= 0 || dailyRecord.RegularNetHours <= 0)
        {
            return line;
        }
        var hourlyRate = employee.DailyRate / (decimal)dailyRecord.ShiftWorkingHour;
        var regularHours = (decimal)dailyRecord.RegularNetHours;
        line.Value += hourlyRate * regularHours;
        return line;
    } 
}
