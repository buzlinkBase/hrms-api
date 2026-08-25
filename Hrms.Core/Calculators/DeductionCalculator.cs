namespace Hrms.Core.Calculators;

public class DeductionCalculator : ICalculator<DeductionPipeData, DeductionPayloadContext>
{
    private readonly IPipeLine<DeductionPayloadContext, DeductionPipeData> _pipeline;

    public DeductionCalculator(IPipeLine<DeductionPayloadContext, DeductionPipeData> pipeline)
    {
        _pipeline = pipeline;
    }
    public DeductionPipeData Calculate(DeductionPayloadContext context)
    {
        return _pipeline.Run(context);
    }
}
