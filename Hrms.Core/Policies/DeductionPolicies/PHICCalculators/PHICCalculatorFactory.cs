
namespace Hrms.Core.Policies.DeductionPolicies;

public class PHICCalculatorFactory
{
    public static IDeductionCalculator Create(DeductionPayloadContext context)
    {
        var config = context.Employee.PHICRate;
        if (config == null) return new NoPHICDeductionCalculator();
        return config.ComputationType switch
        {
            ComputationBasis.None => new NoPHICDeductionCalculator(),
            ComputationBasis.FixedPerPayroll => PHICFixSalaryFrequencyFactory.Create(context),
            ComputationBasis.Table => PHICTableSalaryFrequencyFactory.Create(context),
            _ => throw new NotImplementedException()
        };
    }
}

public class PHICTableSalaryFrequencyFactory
{
    public static IDeductionCalculator Create(DeductionPayloadContext context)
    {
        return context.Employee.PayrollFrequency switch
        {
            PayrollFrequency.DAILY => new TablePHICDailyCalculator(),
            PayrollFrequency.WEEKLY => new TablePHICWeeklyCalculator(),
            PayrollFrequency.SEMI_MONTHLY => new TablePHICSemiMonthlyCalculator(),
            PayrollFrequency.MONTHLY => new TablePHICMonthlyCalculator(),
            _ => throw new NotImplementedException()
        };
    }
}

public class PHICFixSalaryFrequencyFactory
{
    public static IDeductionCalculator Create(DeductionPayloadContext context)
    {
        var config = context.Employee.PHICRate;
        if (config == null) return new NoPHICDeductionCalculator();
        return new FixedPHICPerPayroll();
    }
}

public class NoPHICDeductionCalculator : NoDeductionCalculator { }
