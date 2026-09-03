
using Hrms.Core.Policies.DeductionPolicies.WtaxCalculators;

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
            ComputationBasis.Table => WTaxTableSalaryFrequencyFactory.Create(context),
            _ => throw new NotImplementedException()
        };
    }
}

public class WTaxTableSalaryFrequencyFactory
{
    // One calculator for every frequency now — WTax is computed independently per period
    // (see TableWTaxCalculator), so PayrollFrequency only ever mattered for which bracket
    // table applied, which TableWTaxCalculator/WTaxHelper.GetTable already handle internally.
    public static IDeductionCalculator Create(DeductionPayloadContext context) => new TableWTaxCalculator();
}

public class WTaxFixSalaryFrequencyFactory
{
    public static IDeductionCalculator Create(DeductionPayloadContext context)
    {
        var config = context.Employee.TaxRate;
        if (config == null) return new NoWTaxDeductionCalculator();
        return new FixedWTaxPerPayroll();
    }
}

public class NoWTaxDeductionCalculator : NoDeductionCalculator { }
