
namespace Hrms.Core.Policies.DeductionPolicies;

public class HDMFCalculatorFactory
{
    public static IDeductionCalculator Create(DeductionPayloadContext context)
    {
        var config = context.Employee.HDMFRate;
        if (config == null) return new NoHDMFDeductionCalculator();
        return config.ComputationType switch
        {
            ComputationBasis.None => new NoHDMFDeductionCalculator(),
            ComputationBasis.FixedPerPayroll => HDMFFixSalaryFrequencyFactory.Create(context),
            ComputationBasis.Table => HDMFTableSalaryFrequencyFactory.Create(context),
            _ => throw new NotImplementedException()
        };
    }
}

public class HDMFTableSalaryFrequencyFactory
{
    public static IDeductionCalculator Create(DeductionPayloadContext context)
    {
        return context.Employee.PayrollFrequency switch
        {
            PayrollFrequency.DAILY => new TableHDMFDailyCalculator(),
            PayrollFrequency.WEEKLY => new TableHDMFWeeklyCalculator(),
            PayrollFrequency.SEMI_MONTHLY => new TableHDMFSemiMonthlyCalculator(),
            PayrollFrequency.MONTHLY => new TableHDMFMonthlyCalculator(),
            _ => throw new NotImplementedException()
        };
    }
}

public class HDMFFixSalaryFrequencyFactory
{
    public static IDeductionCalculator Create(DeductionPayloadContext context)
    {
        var config = context.Employee.HDMFRate;
        if (config == null) return new NoHDMFDeductionCalculator();
        return new FixedHDMFPerPayroll();
    }
}

public class NoHDMFDeductionCalculator : NoDeductionCalculator { }
