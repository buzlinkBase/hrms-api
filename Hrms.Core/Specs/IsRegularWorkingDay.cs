namespace Hrms.Core.Specs;

internal class IsAbsent : IPayrollSpec<PayrollContext>
{
    public bool IsSatisfiedBy(PayrollContext context)
    {
        return context.WorkType == WorkType.Absent || context.DailyRecord.AbsentCount>0;
    }
}
//internal class IsSpecialNonWorking : IPayrollSpec<PayrollContext>
//{
//    public bool IsSatisfiedBy(PayrollContext context)
//    {
//        return context.WorkType == WorkType.SpecialNonWorking;
//    }
//}
internal class IsSpecialNonWorkingDuty : IPayrollSpec<PayrollContext>
{
    public bool IsSatisfiedBy(PayrollContext context)
    {
        return context.WorkType == WorkType.SpecialHolidayDuty;
    }
}
internal class IsRegularHoliday : IPayrollSpec<PayrollContext>
{
    public bool IsSatisfiedBy(PayrollContext context)
    {
        return context.WorkType == WorkType.RestDayLegalHoliday;
    }
}
internal class IsLeave : IPayrollSpec<PayrollContext>
{
    public bool IsSatisfiedBy(PayrollContext context)
    {
        var key = new Leavekey(context.Employee.Id);
        context.Payload.Leaves.TryGetValue(key, out var leave);
        return leave != null && leave.Any();
    }
}

internal class IsWholeDayLeave : IPayrollSpec<PayrollContext>
{
    public bool IsSatisfiedBy(PayrollContext context)
    {

        var key = new Leavekey(context.Employee.Id);
        context.Payload.Leaves.TryGetValue(key, out var leave);

        return leave != null && leave
            .Where(x => x.DayFraction == DayFraction.FullDay)
            .Any();

    }
}
//internal class IsPaidLeaveOnLegalHoliday : IPayrollSpec<PayrollContext>
//{
//    public bool IsSatisfiedBy(PayrollContext context)
//    {
//        return context.WorkType == WorkType.PaidLeaveOnLegalHoliday;
//    }
//}
internal class IsRestDay : IPayrollSpec<PayrollContext>
{
    public bool IsSatisfiedBy(PayrollContext context)
    {
        return context.WorkType == WorkType.RestDay;
    }
}

// Covers all legal-holiday work-type variants (with or without rest day / duty).
internal class IsLegalHolidayDay : IPayrollSpec<PayrollContext>
{
    public bool IsSatisfiedBy(PayrollContext context) =>
        context.WorkType is WorkType.LegalHoliday
            or WorkType.LegalHolidayDuty
            or WorkType.RestDayLegalHoliday
            or WorkType.RestDayLegalHolidayDuty;
}

// Covers all special-non-working-holiday work-type variants.
internal class IsSpecialNonWorkingDay : IPayrollSpec<PayrollContext>
{
    public bool IsSatisfiedBy(PayrollContext context) =>
        context.WorkType is WorkType.SpecialNonWorkingHoliday
            or WorkType.SpecialHolidayDuty
            or WorkType.RestDaySpecialHoliday
            or WorkType.RestDaySpecialHolidayDuty;
}

// Covers all rest-day work-type variants not already captured by holiday specs.
internal class IsRestDayType : IPayrollSpec<PayrollContext>
{
    public bool IsSatisfiedBy(PayrollContext context) =>
        context.WorkType is WorkType.RestDay
            or WorkType.RestDayDuty
            or WorkType.RestDayTravel;
}
internal class IsRegularWorkDay : IPayrollSpec<PayrollContext>
{
    public bool IsSatisfiedBy(PayrollContext context)
    {
        return context.WorkType == WorkType.RegularWorkDay;
    }
}
internal class IsRegularHolidayDuty : IPayrollSpec<PayrollContext>
{
    public bool IsSatisfiedBy(PayrollContext context)
    {
        return context.WorkType == WorkType.LegalHolidayDuty;
    }
}
 
internal class IsSpecialHolidayDuty : IPayrollSpec<PayrollContext>
{
    public bool IsSatisfiedBy(PayrollContext context)
    {
        return context.WorkType == WorkType.SpecialHolidayDuty;
    }
} 
internal class IsRestDaySpecialHolidayDuty : IPayrollSpec<PayrollContext>
{
    public bool IsSatisfiedBy(PayrollContext context)
    {
        return context.WorkType == WorkType.RestDaySpecialHolidayDuty;
    }
}
internal class IsPaidLeave : IPayrollSpec<PayrollContext>
{
    public bool IsSatisfiedBy(PayrollContext context)
    {
        return context.WorkType == WorkType.PaidLeave;
    }
}
internal class IsUnpaidLeave : IPayrollSpec<PayrollContext>
{
    public bool IsSatisfiedBy(PayrollContext context)
    {
        return context.WorkType == WorkType.UnpaidLeave;
    }
}
internal class IsIncomplete : IPayrollSpec<PayrollContext>
{
    public bool IsSatisfiedBy(PayrollContext context)
    {
        return context.WorkType == WorkType.Incomplete;
    }
}

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
public class IsEligibleForLeaveCredits : IPayrollSpec<PayrollContext>
{
    public bool IsSatisfiedBy(PayrollContext context)
    {
        return context.Employee.Settings?.IsEligibleForLeaveCredits ?? true;
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