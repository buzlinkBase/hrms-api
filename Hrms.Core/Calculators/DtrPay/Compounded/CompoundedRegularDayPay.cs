namespace Hrms.Core.Calculators.DtrPay;

// Compounded -- Regular work day. Hours: RegularNetHours / RegularOTHours / RegularNDHours / RegularNDOTHours.
//   daytime Hrs (Hrs - (ND - NDOT)) via RegularPipeline
//   OT - NDOT x hourly x 1.0 (plain hourly rate) x OVERTIME
//   ND - NDOT x hourly x 1.0 (plain hourly rate) x NIGHTDIFF
//   NDOT   x hourly x 1.0 (plain hourly rate) x OVERTIME x NIGHTDIFF
internal sealed class CompoundedRegularDayPay : CompoundedDayTypePayBase
{
    public CompoundedRegularDayPay(RegularPipeline dayPipeline, DayTypePayInputs inputs, CompoundedPricers pricers)
        : base(dayPipeline, inputs, pricers) { }

    protected override PayCategory Category => PayCategories.Regular;
}
