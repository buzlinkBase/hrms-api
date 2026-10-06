namespace Hrms.Core.Policies.DTRPolicies;
internal class RegularWorkDayPolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    public RegularWorkDayPolicy() { }
    public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
    {
        var dailyRecord = context.DailyRecord;
        if (dailyRecord.ShiftWorkingHour <= 0)
        {
            return line;
        }
        var hours = DayTypeHours.For(context, dailyRecord.RegularNetHours, dailyRecord.RegularNDHours, dailyRecord.RegularNDOTHours);
        // A Fixed-salary employee's Basic Pay is the monthly salary, so their regular NIGHT hours'
        // base is paid through the ND bucket instead (AdditiveRegularDayPay.AddNightBaseToNd) --
        // the day pay must stay daytime-only (Plain) for them under every method, or the night
        // base would be counted in both places.
        var paidHours = context.Employee.SalaryType == SalaryType.FIXED
            ? hours.Plain
            : hours.PaidHours(context, dailyRecord.RegularNetHours);

        line.Value += RateHelper.GetHourlyRate(context) * paidHours;
        return line;
    }
}
