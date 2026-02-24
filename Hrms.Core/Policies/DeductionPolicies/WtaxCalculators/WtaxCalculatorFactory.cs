
namespace Hrms.Core.Policies.DeductionPolicies;

public class WTaxCalculatorFactory
{
    public static IDeductionCalculator Create(DeductionPayloadContext context)
    {
        var config = context.Employee.TaxRate;
        if (config == null) return new NoWTaxDeductionCalculator();
        return config.ComputationType switch
        {
            ComputationBasis.None => new NoWTaxDeductionCalculator(),
            ComputationBasis.FixedPerPayroll => WTaxFixSalaryFrequencyFactory.Create(context),
            ComputationBasis.FixedMonthly => WTaxFixSalaryFrequencyFactory.Create(context),
            ComputationBasis.Table => WTaxTableSalaryFrequencyFactory.Create(context),
            _ => throw new NotImplementedException()
        };
    }
}

public class WTaxTableSalaryFrequencyFactory
{
    public static IDeductionCalculator Create(DeductionPayloadContext context)
    {
        var resolver = new CutoffPolicyResolver();
        return context.Employee.PayrollFrequency switch
        {
            PayrollFrequency.DAILY => new TableWTaxDailyCalculator(resolver),
            PayrollFrequency.WEEKLY => new TableWTaxWeeklyCalculator(resolver),
            PayrollFrequency.SEMI_MONTHLY => new TableWTaxSemiMonthlyCalculator(resolver),
            PayrollFrequency.MONTHLY => new TableWTaxMonthlyCalculator(resolver),
            _ => throw new NotImplementedException()
        };
    }
}

public class WTaxFixSalaryFrequencyFactory
{
    public static IDeductionCalculator Create(DeductionPayloadContext context)
    {
        var config = context.Employee.TaxRate;
        if (config == null) return new NoWTaxDeductionCalculator();

        if (config.ComputationType == ComputationBasis.FixedPerPayroll)
        {
            return new FixedWTaxPerPayroll();
        }

        if (config.ComputationType == ComputationBasis.FixedMonthly)
        {
            return context.Employee.PayrollFrequency switch
            {
                PayrollFrequency.DAILY => new FixedWTaxDailyCalculator(),
                PayrollFrequency.WEEKLY => new FixedWTaxWeeklyCalculator(),
                PayrollFrequency.SEMI_MONTHLY => new FixedWTaxSemiMonthlyCalculator(),
                PayrollFrequency.MONTHLY => new FixedWTaxMonthlyCalculator(),
                _ => throw new NotImplementedException()
            };
        }
        else
        {
            throw new NotImplementedException();
        }
    }
}

public class NoWTaxDeductionCalculator : NoDeductionCalculator { }
