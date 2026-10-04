namespace Hrms.Core.Calculators.DtrPay;

// Additive -- Special holiday falling on a rest day. Hours: RestSpecialDayHours / RestSpecialDayOTHours / RestSpecialDayNDHours / RestSpecialDayNDOTHours.
//   Hrs         x hourly x RESTDAY_SPECIAL   (night included, via RestSpecialDayPipeLine)
//   OT          x hourly x RESTSPECIAL_OT_PREMIUM (else HOLIDAY_OT)
//   ND          x hourly x (NIGHTDIFF - 1)
//   NDOT        -> 0
internal sealed class AdditiveRestSpecialDayPay : AdditiveDayTypePayBase
{
    public AdditiveRestSpecialDayPay(RestSpecialDayPipeLine dayPipeline, DayTypePayInputs inputs, AdditivePricers pricers)
        : base(dayPipeline, inputs, pricers) { }

    protected override PayCategory Category => PayCategories.RestSpecial;
}
