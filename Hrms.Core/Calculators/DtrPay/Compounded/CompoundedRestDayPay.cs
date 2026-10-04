namespace Hrms.Core.Calculators.DtrPay;

// Compounded -- Rest day. Hours: RestDayHours / RestDayOTHours / RestDayNDHours / RestDayNDOTHours.
//   Hrs    via RestDayPipeLine
//   OT     x hourly x RESTDAY_DUTY x RESTDAY_OT_PREMIUM (else HOLIDAY_OT)
//   ND     x hourly x RESTDAY_DUTY x NIGHTDIFF
//   NDOT   x hourly x RESTDAY_DUTY x RESTDAY_OT_PREMIUM (else HOLIDAY_OT) x NIGHTDIFF
internal sealed class CompoundedRestDayPay : CompoundedDayTypePayBase
{
    public CompoundedRestDayPay(RestDayPipeLine dayPipeline, DayTypePayInputs inputs, CompoundedPricers pricers)
        : base(dayPipeline, inputs, pricers) { }

    protected override PayCategory Category => PayCategories.RestDay;
}
