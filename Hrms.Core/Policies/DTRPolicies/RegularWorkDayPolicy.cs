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
        var multiplier = context.OtNdCalculationMethod == OtNdCalculationMethod.Additive
            ? (decimal)context.DailyRecord.RegularNetHours 
            : hours.Plain;

        line.Value += RateHelper.GetHourlyRate(context) * multiplier;
        return line;
    }
}
