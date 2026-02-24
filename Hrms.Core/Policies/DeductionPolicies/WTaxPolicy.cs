namespace Hrms.Core.Policies.DeductionPolicies
{
    internal class WTaxPolicy : PayrollPolicyBase<DeductionPipeData, DeductionPayloadContext>
    {
        public WTaxPolicy() : base(new IsComputeWtax(), SpecFailBehaviour.ReturnInput)
        {
        }
        public override DeductionPipeData ApplyIfSatisfied(DeductionPipeData line, DeductionPayloadContext context)
        {
            if (line.IsLimit) return line;
            var calculator = WTaxCalculatorFactory.Create(context);
            return calculator.Calculate(context, line);
        }
    }
}