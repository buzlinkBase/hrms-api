namespace Hrms.Core.Calculators.DtrPay;

// Compounded -- Double legal holiday. Hours: DoubleLegalHours / DoubleLegalOTHours / DoubleLegalNDHours / DoubleLegalNDOTHours.
//   daytime Hrs (Hrs - (ND - NDOT)) via DoubleLegalPipeLine
//   OT - NDOT x hourly x LEGAL_HOLIDAY_DUTY x LEGAL_HOLIDAY_DUTY x DOUBLELEGAL_OT_PREMIUM (else HOLIDAY_OT)
//   ND - NDOT x hourly x LEGAL_HOLIDAY_DUTY x LEGAL_HOLIDAY_DUTY x NIGHTDIFF
//   NDOT   x hourly x LEGAL_HOLIDAY_DUTY x LEGAL_HOLIDAY_DUTY x DOUBLELEGAL_OT_PREMIUM (else HOLIDAY_OT) x NIGHTDIFF
internal sealed class CompoundedDoubleLegalPay : CompoundedDayTypePayBase
{
    public CompoundedDoubleLegalPay(DoubleLegalPipeLine dayPipeline, DayTypePayInputs inputs, CompoundedPricers pricers)
        : base(dayPipeline, inputs, pricers) { }

    protected override PayCategory Category => PayCategories.DoubleLegal;
}
