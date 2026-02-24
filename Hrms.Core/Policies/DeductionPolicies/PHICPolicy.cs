namespace Hrms.Core.Policies.DeductionPolicies;

internal class PHICPolicy : PayrollPolicyBase<DeductionPipeData, DeductionPayloadContext>
{
    public PHICPolicy() : base(new IsComputePHIC(), SpecFailBehaviour.ReturnInput)
    {
    }
    public override DeductionPipeData ApplyIfSatisfied(DeductionPipeData line, DeductionPayloadContext context)
    {
        if (line.IsLimit) return line;
        var calculator = PHICCalculatorFactory.Create(context);
        return calculator.Calculate(context, line);
    }
}