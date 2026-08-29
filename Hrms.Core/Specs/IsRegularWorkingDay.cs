namespace Hrms.Core.Specs;
internal class IsEligibleForOvertime : IPayrollSpec<PayrollContext>
{
    public bool IsSatisfiedBy(PayrollContext context)
    {
        return context.Employee.Settings != null &&
              (context.Employee.Settings?.IsEligibleForOvertime ?? true);
    }
}
public class IsEligibleForNightDifferential : IPayrollSpec<PayrollContext>
{
    public bool IsSatisfiedBy(PayrollContext context)
    {
        return context.Employee.Settings != null &&
               (context.Employee.Settings?.IsEligibleForNightDifferential ?? true);
    }
}
public class IsEligibleForHolidayPay : IPayrollSpec<PayrollContext>
{
    public bool IsSatisfiedBy(PayrollContext context)
    {
        return context.Employee.Settings?.IsEligibleForHolidayPay ?? true;
    }
}
public class IsComputeSSS : IPayrollSpec<DeductionPayloadContext>
{
    public bool IsSatisfiedBy(DeductionPayloadContext context)
    {
        return context.Employee.SSSRate != null &&
               context.Employee.SSSRate.ComputationType != ComputationBasis.None;
    }
}

public class IsComputePHIC : IPayrollSpec<DeductionPayloadContext>
{
    public bool IsSatisfiedBy(DeductionPayloadContext context)
    {
        return context.Employee.PHICRate != null &&
              context.Employee.PHICRate.ComputationType != ComputationBasis.None;
    }
}

public class IsComputeHDMF : IPayrollSpec<DeductionPayloadContext>
{
    public bool IsSatisfiedBy(DeductionPayloadContext context)
    {
        return context.Employee.HDMFRate != null &&
            context.Employee.HDMFRate.ComputationType != ComputationBasis.None;
    }
}

public class IsComputeWtax : IPayrollSpec<DeductionPayloadContext>
{
    public bool IsSatisfiedBy(DeductionPayloadContext context)
    {
        return context.Employee.TaxRate != null &&
              context.Employee.TaxRate.ComputationType != ComputationBasis.None;
    }
}

//public class IsQualityAsTaxable : IPayrollSpec<DeductionPayloadContext>
//{
//    private readonly decimal _allowances;

//    public IsQualityAsTaxable(decimal allowances)
//    {
//        _allowances = allowances;
//    }

//public bool IsSatisfiedBy(DeductionPayloadContext context)
//{
//    if (!new IsComputeWtax().IsSatisfiedBy(context)) return false;

//    var YearLyGross = context.Employee.DailyRate
//        * context.Payload.CompanyPolicy.TotalDaysInaYear;

//    YearLyGross += _allowances;

//    var firstNonZero = context.Payload.TaxTableModel
//        .Where(x => (x.BaseTaxDue > 0
//        || x.AddOnPercentage > 0
//        || x.PercentageInAmountOf > 0))
//        .FirstOrDefault();

//    if (firstNonZero == null) return false;

//    var amount = firstNonZero.PercentageInAmountOf == 0
//        ? firstNonZero.RangeFrom
//        : firstNonZero.PercentageInAmountOf;
//    var rate = firstNonZero.BaseTaxDue + (amount * firstNonZero.AddOnPercentage);
//    return rate <= YearLyGross;
//}

//}

public class IsCrossMonth : IPayrollSpec<CalculatorPayload>
{
    public bool IsSatisfiedBy(CalculatorPayload context)
    {
        return context.FromDate.Year != context.ToDate.Year ||
            context.FromDate.Month != context.ToDate.Month;
    }
}