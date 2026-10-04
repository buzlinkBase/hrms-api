namespace Hrms.Core.Calculators.DtrPay;

// Additive -- Legal holiday falling on a rest day. Hours: RestLegalDayHours / RestLegalDayOTHours / RestLegalDayNDHours / RestLegalDayNDOTHours.
//   Hrs + ND    x hourly x RESTDAY_DUTY x LEGAL_HOLIDAY_DUTY   (plain hours via RestLegalDayPipeLine)
//   OT + NDOT   x hourly x RESTLEGAL_OT_PREMIUM (else HOLIDAY_OT)
//   ND + NDOT   x hourly x (NIGHTDIFF - 1)
//   NDOT        -> 0
internal sealed class AdditiveRestLegalDayPay : AdditiveDayTypePayBase
{
    public AdditiveRestLegalDayPay(RestLegalDayPipeLine dayPipeline, DayTypePayInputs inputs, AdditivePricers pricers)
        : base(dayPipeline, inputs, pricers) { }

    protected override PayCategory Category => PayCategories.RestLegal;
}
