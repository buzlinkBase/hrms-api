namespace Hrms.Core.Calculators.DtrPay;

// Additive -- Special holiday falling on a rest day. Hours: RestSpecialDayHours / RestSpecialDayOTHours / RestSpecialDayNDHours / RestSpecialDayNDOTHours.
//   Hrs + ND    x hourly x RESTDAY_SPECIAL   (plain hours via RestSpecialDayPipeLine)
//   OT + NDOT   x hourly x RESTSPECIAL_OT_PREMIUM (else HOLIDAY_OT)
//   ND + NDOT   x hourly x (NIGHTDIFF - 1)
//   NDOT        -> 0
internal sealed class AdditiveRestSpecialDayPay : AdditiveDayTypePayBase
{
    public AdditiveRestSpecialDayPay(RestSpecialDayPipeLine dayPipeline, DayTypePayInputs inputs, AdditivePricers pricers)
        : base(dayPipeline, inputs, pricers) { }

    protected override PayCategory Category => PayCategories.RestSpecial;
}
