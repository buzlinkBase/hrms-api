
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
            ComputationBasis.FixedMonthly => PHICFixSalaryFrequencyFactory.Create(context),
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

        if (config.ComputationType == ComputationBasis.FixedPerPayroll)
        {
            return new FixedPHICPerPayroll();
        }

        if (config.ComputationType == ComputationBasis.FixedMonthly)
        {
            return context.Employee.PayrollFrequency switch
            {
                PayrollFrequency.DAILY => new FixedPHICDailyCalculator(),
                PayrollFrequency.WEEKLY => new FixedPHICWeeklyCalculator(),
                PayrollFrequency.SEMI_MONTHLY => new FixedPHICSemiMonthlyCalculator(),
                PayrollFrequency.MONTHLY => new FixedPHICMonthlyCalculator(),
                _ => throw new NotImplementedException()
            };
        }
        else
        {
            throw new NotImplementedException();
        }
    }
}

public class NoPHICDeductionCalculator : NoDeductionCalculator { }
