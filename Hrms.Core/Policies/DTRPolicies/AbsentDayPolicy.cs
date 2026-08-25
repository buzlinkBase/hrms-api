namespace Hrms.Core.Policies.DTRPolicies;

internal class AbsentDayPolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    //public AbsentDayPolicy() : base(
    //     new IsAbsent()
    //    .AndNot(new IsLeave()))
    //{
    //}

    public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
    {
        var dailyRecord = context.DailyRecord;

        if (dailyRecord.ShiftWorkingHour <= 0 || dailyRecord.AbsentCount <= 0)
        {
            return line;
        }

        line.Value += dailyRecord.AbsentCount * context.Employee.DailyRate;
        return line;

    }
}
