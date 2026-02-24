namespace Hrms.Core.Policies.DeductionPolicies;

internal class HDMFPolicy : PayrollPolicyBase<DeductionPipeData, DeductionPayloadContext>
{
    public HDMFPolicy() : base(new IsComputeHDMF(), SpecFailBehaviour.ReturnInput)
    {
    }
    public override DeductionPipeData ApplyIfSatisfied(DeductionPipeData line, DeductionPayloadContext context)
    {
        if (line.IsLimit) return line;
        var calculator = HDMFCalculatorFactory.Create(context);
        return calculator.Calculate(context, line);
    }
}