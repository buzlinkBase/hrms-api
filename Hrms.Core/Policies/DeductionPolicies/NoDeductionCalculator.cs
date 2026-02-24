namespace Hrms.Core.Policies.DeductionPolicies;

public class NoDeductionCalculator : IDeductionCalculator
{
    public DeductionPipeData Calculate(DeductionPayloadContext context, DeductionPipeData line) => line;
}
