namespace Hrms.Core.Calculators.DtrPay;

// Compounded -- Double legal holiday falling on a rest day. Hours: RestDoubleLegalHours / RestDoubleLegalOTHours / RestDoubleLegalNDHours / RestDoubleLegalNDOTHours.
//   Hrs    via RestDoubleLegalPipeLine
//   OT     x hourly x RESTDAY_DUTY x LEGAL_HOLIDAY_DUTY x LEGAL_HOLIDAY_DUTY x RESTDOUBLELEGAL_OT_PREMIUM (else HOLIDAY_OT)
//   ND     x hourly x RESTDAY_DUTY x LEGAL_HOLIDAY_DUTY x LEGAL_HOLIDAY_DUTY x NIGHTDIFF
//   NDOT   x hourly x RESTDAY_DUTY x LEGAL_HOLIDAY_DUTY x LEGAL_HOLIDAY_DUTY x RESTDOUBLELEGAL_OT_PREMIUM (else HOLIDAY_OT) x NIGHTDIFF
internal sealed class CompoundedRestDoubleLegalPay : CompoundedDayTypePayBase
{
    public CompoundedRestDoubleLegalPay(RestDoubleLegalPipeLine dayPipeline, DayTypePayInputs inputs, CompoundedPricers pricers)
        : base(dayPipeline, inputs, pricers) { }

    protected override PayCategory Category => PayCategories.RestDoubleLegal;
}
