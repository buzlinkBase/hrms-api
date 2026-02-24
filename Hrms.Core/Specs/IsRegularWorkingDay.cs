namespace Hrms.Core.Specs;

internal class IsAbsent : IPayrollSpec<PayrollContext>
{
    public bool IsSatisfiedBy(PayrollContext context)
    {
        return context.WorkType == WorkType.Absent;
    }
}
internal class IsSpecialNonWorking : IPayrollSpec<PayrollContext>
{
    public bool IsSatisfiedBy(PayrollContext context)
    {
        return context.WorkType == WorkType.SpecialNonWorking;
    }
}
internal class IsSpecialNonWorkingDuty : IPayrollSpec<PayrollContext>
{
    public bool IsSatisfiedBy(PayrollContext context)
    {
        return context.WorkType == WorkType.SpecialHolidayDutyNW;
    }
}
internal class IsRegularHoliday : IPayrollSpec<PayrollContext>
{
    public bool IsSatisfiedBy(PayrollContext context)
    {
        return context.WorkType == WorkType.RegularHoliday;
    }
}
internal class IsLeave : IPayrollSpec<PayrollContext>
{
    public bool IsSatisfiedBy(PayrollContext context)
    {
        var key = new EmployeePayDateKey(context.Employee.Id, context.PayrollDate);
        context.Payload.Leaves.TryGetValue(key, out var leave);
        return leave != null && leave.Any();
    }
}

internal class IsWholeDayLeave : IPayrollSpec<PayrollContext>
{
    public bool IsSatisfiedBy(PayrollContext context)
    {

        var key = new EmployeePayDateKey(context.Employee.Id, context.PayrollDate);
        context.Payload.Leaves.TryGetValue(key, out var leave);

        return leave != null && leave
            .Where(x => x.DayType == LeaveDayType.WholeDay)
            .Any();

    }
}
internal class IsPaidLeaveOnLegalHoliday : IPayrollSpec<PayrollContext>
{
    public bool IsSatisfiedBy(PayrollContext context)
    {
        return context.WorkType == WorkType.PaidLeaveOnLegalHoliday;
    }
}
internal class IsRestDay : IPayrollSpec<PayrollContext>
{
    public bool IsSatisfiedBy(PayrollContext context)
    {
        return context.WorkType == WorkType.RestDay;
    }
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
        return context.WorkType == WorkType.RegularHolidayDuty;
    }
}
internal class IsSpecialHoliday : IPayrollSpec<PayrollContext>
{
    public bool IsSatisfiedBy(PayrollContext context)
    {
        return context.WorkType == WorkType.SpecialHoliday;
    }
}
internal class IsSpecialHolidayDuty : IPayrollSpec<PayrollContext>
{
    public bool IsSatisfiedBy(PayrollContext context)
    {
        return context.WorkType == WorkType.SpecialHolidayDuty;
    }
}
internal class IsSpecialHolidayDutyNW : IPayrollSpec<PayrollContext>
{
    public bool IsSatisfiedBy(PayrollContext context)
    {
        return context.WorkType == WorkType.SpecialHolidayDutyNW;
    }
}
internal class IsRestDayDuty : IPayrollSpec<PayrollContext>
{
    public bool IsSatisfiedBy(PayrollContext context)
    {
        return context.WorkType == WorkType.RestDayDuty;
    }
}
internal class IsRestDayLegalHolidayDuty : IPayrollSpec<PayrollContext>
{
    public bool IsSatisfiedBy(PayrollContext context)
    {
        return context.WorkType == WorkType.RestDayLegalHolidayDuty;
    }
}
internal class IsPaidLeaveOnSpecialHoliday : IPayrollSpec<PayrollContext>
{
    public bool IsSatisfiedBy(PayrollContext context)
    {
        return context.WorkType == WorkType.PaidLeaveOnSpecialHoliday;
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