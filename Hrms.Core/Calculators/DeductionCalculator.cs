namespace Hrms.Core.Calculators;

public class DeductionCalculator : ICalculator<DeductionPipeData, DeductionPayloadContext>
{
    public DeductionPipeData Calculate(DeductionPayloadContext context)
    {
        return new DeductionPipeline().Run(context);
    }
}
