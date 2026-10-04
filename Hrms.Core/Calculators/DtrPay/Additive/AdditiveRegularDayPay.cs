namespace Hrms.Core.Calculators.DtrPay;

// Additive -- Regular work day. Hours: RegularNetHours / RegularOTHours / RegularNDHours / RegularNDOTHours.
//   Hrs         x hourly x 1.0 (plain hourly rate)   (RegularWorkDayPolicy, night included)
//   OT          x hourly x OVERTIME
//   ND          x hourly x (NIGHTDIFF - 1)
//   NDOT        -> 0
internal sealed class AdditiveRegularDayPay : AdditiveDayTypePayBase
{
    public AdditiveRegularDayPay(RegularPipeline dayPipeline, DayTypePayInputs inputs, AdditivePricers pricers)
        : base(dayPipeline, inputs, pricers) { }

    protected override PayCategory Category => PayCategories.Regular;

    // A Fixed-salary employee's Basic Pay is the monthly salary, not this day type's day pay
    // (EmployeePayrollLineService.GetBasicPay), while their regular night hours' base has always
    // been paid on top of it through the ND bucket. RegularWorkDayPolicy leaves those hours out of
    // the day pay for them, so the base is paid here (RegularNDBasePay), exactly where it always was.
    protected override BucketPay AddNightBaseToNd(decimal nightRegularHours, CategoryRates rates, BucketPay nd, PayrollContext context) =>
        context.Employee.SalaryType == SalaryType.FIXED
            ? nd with { Value = Pricers.NdBase.Price(nightRegularHours, rates).Value + nd.Value } // NDPremium unchanged, so BasePortion == the base
            : nd;
}
