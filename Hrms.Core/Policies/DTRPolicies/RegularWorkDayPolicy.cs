namespace Hrms.Core.Policies.DTRPolicies;

internal class RegularWorkDayPolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    public RegularWorkDayPolicy() { }

    public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
    {
        //include hourly computation for FIXED employee here for ND Premium extraction later
        var dailyRecord = context.DailyRecord;
        var employee = context.Employee;
        if (dailyRecord.ShiftWorkingHour <= 0 || dailyRecord.RegularNetHours <= 0)
        {
            return line;
        }
        var regularHours = (decimal)dailyRecord.RegularNetHours;
        line.Value += RateHelper.GetHourlyRate(context) * regularHours;
        return line;
    }
}