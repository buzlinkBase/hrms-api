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
public class IsEligibleForLeavePay  : IPayrollSpec<PayrollContext>
{
    public bool IsSatisfiedBy(PayrollContext context)
    {
        return context.Employee.Settings?.IsEligibleForLeaveCredits ?? true;
    }
}
// Regular Holiday is legally mandatory pay (worked or not) regardless of any CBA — used by
// the Regular/DoubleLegal/RestLegalDay/RestDoubleLegal calculators.
public class IsEligibleForRegularHolidayPay : IPayrollSpec<PayrollContext>
{
    public bool IsSatisfiedBy(PayrollContext context)
    {
        return context.Employee.Settings?.IsEligibleForRegularHolidayPay ?? true;
    }
}

// Special Non-Working Holiday is "no work, no pay" by default under DOLE rules — this flag
// exists for the CBA case where an employer opts to pay it anyway even when unworked. Used
// by the SpecialNonWorkingDay/RestSpecialDay calculators.
public class IsEligibleForSpecialHolidayPay : IPayrollSpec<PayrollContext>
{
    public bool IsSatisfiedBy(PayrollContext context)
    {
        return context.Employee.Settings?.IsEligibleForSpecialHolidayPay ?? false;
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

public class IsCrossMonth : IPayrollSpec<CalculatorPayload>
{
    public bool IsSatisfiedBy(CalculatorPayload context)
    {
        return context.FromDate.Year != context.ToDate.Year ||
            context.FromDate.Month != context.ToDate.Month;
    }
}