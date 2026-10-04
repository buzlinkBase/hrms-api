namespace Hrms.Core.Calculators.DtrPay;

// Compounded -- Legal holiday falling on a rest day. Hours: RestLegalDayHours / RestLegalDayOTHours / RestLegalDayNDHours / RestLegalDayNDOTHours.
//   daytime Hrs (Hrs - (ND - NDOT)) via RestLegalDayPipeLine
//   OT - NDOT x hourly x RESTDAY_DUTY x LEGAL_HOLIDAY_DUTY x RESTLEGAL_OT_PREMIUM (else HOLIDAY_OT)
//   ND - NDOT x hourly x RESTDAY_DUTY x LEGAL_HOLIDAY_DUTY x NIGHTDIFF
//   NDOT   x hourly x RESTDAY_DUTY x LEGAL_HOLIDAY_DUTY x RESTLEGAL_OT_PREMIUM (else HOLIDAY_OT) x NIGHTDIFF
internal sealed class CompoundedRestLegalDayPay : CompoundedDayTypePayBase
{
    public CompoundedRestLegalDayPay(RestLegalDayPipeLine dayPipeline, DayTypePayInputs inputs, CompoundedPricers pricers)
        : base(dayPipeline, inputs, pricers) { }

    protected override PayCategory Category => PayCategories.RestLegal;
}
