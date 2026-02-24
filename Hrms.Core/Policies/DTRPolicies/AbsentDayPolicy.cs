namespace Hrms.Core.Policies.DTRPolicies;

internal class AbsentDayPolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    public AbsentDayPolicy() : base(
         new IsAbsent()
        .AndNot(new IsLeave()))
    {
    }

    public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
    {
        //TODO check if non working holiday
        line.AbsentInfo = new AbsentInfo
        {
            PayrollDate = context.PayrollDate,
            Count = 1,
            Amount = context.Employee.DailyRate,
        };
        line.Value += 1;
        return line;

    }
}
