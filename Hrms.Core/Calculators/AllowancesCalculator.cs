namespace Hrms.Core.Calculators;

public class AllowancesCalculator : ICalculator<AllowancePipeData, PayrollContext>
{
    private readonly IPipeLine<PayrollContext, AllowancePipeData> _pipeline;

    public AllowancesCalculator(IPipeLine<PayrollContext, AllowancePipeData> pipeline)
    {
        _pipeline = pipeline;
    }

    public AllowancePipeData Calculate(PayrollContext context)
    {
        return _pipeline.Run(context);
    }
}
