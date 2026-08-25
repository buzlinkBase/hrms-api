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
        // Fixed salary employees already have their regular working hours included in their monthly base pay.
        // Daily-paid workers earn their hourly rate per worked regular hour.
        if (employee.SalaryType == SalaryType.FIXED)
        {
            return line;
        }
        var hourlyRate = RateHelper.GetHourlyRate(context);
        var regularHours = (decimal)dailyRecord.RegularNetHours;
        line.Value += hourlyRate * regularHours;
        return line;
    }
}