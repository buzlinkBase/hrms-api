namespace Hrms.Core.Policies.DeductionPolicies;

public class SSSPolicy : PayrollPolicyBase<DeductionPipeData, DeductionPayloadContext>
{
    public SSSPolicy() : base(new IsComputeSSS(), SpecFailBehaviour.ReturnInput)
    {
    }
    public override DeductionPipeData ApplyIfSatisfied(DeductionPipeData line, DeductionPayloadContext context)
    {
        if (line.IsLimit) return line;
        var calculator = SSSCalculatorFactory.Create(context);
        return calculator.Calculate(context, line);
    }
}
