
namespace Hrms.Core.Policies.DeductionPolicies;

public class SSSCalculatorFactory
{
    public static IDeductionCalculator Create(DeductionPayloadContext context)
    {
        var config = context.Employee.SSSRate;
        if (config == null) return new NoSSSDeductionCalculator();
        return config.ComputationType switch
        {
            ComputationBasis.None => new NoSSSDeductionCalculator(),
            ComputationBasis.FixedPerPayroll => SSSFixSalaryFrequencyFactory.Create(context),
            ComputationBasis.Table => SSSTableSalaryFrequencyFactory.Create(context),
            _ => throw new NotImplementedException()
        };
    }
}

public class SSSTableSalaryFrequencyFactory
{
    public static IDeductionCalculator Create(DeductionPayloadContext context)
    {
        return context.Employee.PayrollFrequency switch
        {
            PayrollFrequency.DAILY => new TableSSSDailyCalculator(),
            PayrollFrequency.WEEKLY => new TableSSSWeeklyCalculator(),
            PayrollFrequency.SEMI_MONTHLY => new TableSSSSemiMonthlyCalculator(),
            PayrollFrequency.MONTHLY => new TableSSSMonthlyCalculator(),
            _ => throw new NotImplementedException()
        };
    }
}

public class SSSFixSalaryFrequencyFactory
{
    public static IDeductionCalculator Create(DeductionPayloadContext context)
    {
        var config = context.Employee.SSSRate;
        if (config == null)
        {
            return new NoSSSDeductionCalculator();
        }
        return new FixedSSSPerPayroll();
    }
}

public class NoSSSDeductionCalculator : NoDeductionCalculator { }
