namespace Hrms.Core.Calculators.DtrPay;

// Additive -- Double legal holiday falling on a rest day. Hours: RestDoubleLegalHours / RestDoubleLegalOTHours / RestDoubleLegalNDHours / RestDoubleLegalNDOTHours.
//   Hrs         x hourly x RESTDAY_DUTY x LEGAL_HOLIDAY_DUTY x LEGAL_HOLIDAY_DUTY   (night included, via RestDoubleLegalPipeLine)
//   OT          x hourly x RESTDOUBLELEGAL_OT_PREMIUM (else HOLIDAY_OT)
//   ND          x hourly x (NIGHTDIFF - 1)
//   NDOT        -> 0
internal sealed class AdditiveRestDoubleLegalPay : AdditiveDayTypePayBase
{
    public AdditiveRestDoubleLegalPay(RestDoubleLegalPipeLine dayPipeline, DayTypePayInputs inputs, AdditivePricers pricers)
        : base(dayPipeline, inputs, pricers) { }

    protected override PayCategory Category => PayCategories.RestDoubleLegal;
}
