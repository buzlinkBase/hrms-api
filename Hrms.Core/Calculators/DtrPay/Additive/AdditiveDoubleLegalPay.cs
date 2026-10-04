namespace Hrms.Core.Calculators.DtrPay;

// Additive -- Double legal holiday. Hours: DoubleLegalHours / DoubleLegalOTHours / DoubleLegalNDHours / DoubleLegalNDOTHours.
//   Hrs         x hourly x LEGAL_HOLIDAY_DUTY x LEGAL_HOLIDAY_DUTY   (night included, via DoubleLegalPipeLine)
//   OT          x hourly x DOUBLELEGAL_OT_PREMIUM (else HOLIDAY_OT)
//   ND          x hourly x (NIGHTDIFF - 1)
//   NDOT        -> 0
internal sealed class AdditiveDoubleLegalPay : AdditiveDayTypePayBase
{
    public AdditiveDoubleLegalPay(DoubleLegalPipeLine dayPipeline, DayTypePayInputs inputs, AdditivePricers pricers)
        : base(dayPipeline, inputs, pricers) { }

    protected override PayCategory Category => PayCategories.DoubleLegal;
}
