namespace Hrms.Core.Calculators.DtrPay;

// Additive -- Regular work day. Hours: RegularNetHours / RegularOTHours / RegularNDHours / RegularNDOTHours.
//   Hrs + ND    x hourly x 1.0 (plain hourly rate)   (plain hours via RegularPipeline)
//   OT + NDOT   x hourly x OVERTIME
//   ND + NDOT   x hourly x (NIGHTDIFF - 1)
//   NDOT        -> 0
internal sealed class AdditiveRegularDayPay : AdditiveDayTypePayBase
{
    public AdditiveRegularDayPay(RegularPipeline dayPipeline, DayTypePayInputs inputs, AdditivePricers pricers)
        : base(dayPipeline, inputs, pricers) { }

    protected override PayCategory Category => PayCategories.Regular;

    // A Fixed-salary employee's Basic Pay is the monthly salary, not this day type's day pay
    // (EmployeePayrollLineService.GetBasicPay), while their regular night hours' base has always
    // been paid on top of it through the ND bucket. Folding it into day pay would silently drop
    // it, so for them it stays in the ND bucket (RegularNDBasePay), exactly where it always was.
    protected override (DayPay Day, BucketPay Nd) FoldNightHoursIntoDayPay(decimal nightBase, DayPay day, BucketPay nd, PayrollContext context) =>
        context.Employee.SalaryType == SalaryType.FIXED
            ? (day, nd with { Value = nightBase + nd.Value }) // NDPremium unchanged, so BasePortion == nightBase
            : base.FoldNightHoursIntoDayPay(nightBase, day, nd, context);
}
