namespace Hrms.Core.Calculators.DtrPay;

// Additive -- Double legal holiday. Hours: DoubleLegalHours / DoubleLegalOTHours / DoubleLegalNDHours / DoubleLegalNDOTHours.
//   Hrs + ND    x hourly x LEGAL_HOLIDAY_DUTY x LEGAL_HOLIDAY_DUTY   (plain hours via DoubleLegalPipeLine)
//   OT + NDOT   x hourly x DOUBLELEGAL_OT_PREMIUM (else HOLIDAY_OT)
//   ND + NDOT   x hourly x (NIGHTDIFF - 1)
//   NDOT        -> 0
internal sealed class AdditiveDoubleLegalPay : AdditiveDayTypePayBase
{
    public AdditiveDoubleLegalPay(DoubleLegalPipeLine dayPipeline, DayTypePayInputs inputs, AdditivePricers pricers)
        : base(dayPipeline, inputs, pricers) { }

    protected override PayCategory Category => PayCategories.DoubleLegal;
}
