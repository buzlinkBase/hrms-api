namespace Hrms.Core.Calculators.DtrPay;

// Additive -- Rest day. Hours: RestDayHours / RestDayOTHours / RestDayNDHours / RestDayNDOTHours.
//   Hrs + ND    x hourly x RESTDAY_DUTY   (plain hours via RestDayPipeLine)
//   OT + NDOT   x hourly x RESTDAY_OT_PREMIUM (else HOLIDAY_OT)
//   ND + NDOT   x hourly x (NIGHTDIFF - 1)
//   NDOT        -> 0
internal sealed class AdditiveRestDayPay : AdditiveDayTypePayBase
{
    public AdditiveRestDayPay(RestDayPipeLine dayPipeline, DayTypePayInputs inputs, AdditivePricers pricers)
        : base(dayPipeline, inputs, pricers) { }

    protected override PayCategory Category => PayCategories.RestDay;
}
