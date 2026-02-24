namespace Hrms.Core.Calculators;

public class AllowancesCalculator : ICalculator<AllowancePipeData, PayrollContext>
{
    public AllowancePipeData Calculate(PayrollContext context)
    {
        return new AllowancePipeline().Run(context);
    }
}
