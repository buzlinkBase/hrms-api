namespace Hrms.Core.Calculators.DtrPay;

// Compounded -- Special holiday falling on a rest day. Hours: RestSpecialDayHours / RestSpecialDayOTHours / RestSpecialDayNDHours / RestSpecialDayNDOTHours.
//   daytime Hrs (Hrs - (ND - NDOT)) via RestSpecialDayPipeLine
//   OT - NDOT x hourly x RESTDAY_SPECIAL x RESTSPECIAL_OT_PREMIUM (else HOLIDAY_OT)
//   ND - NDOT x hourly x RESTDAY_SPECIAL x NIGHTDIFF
//   NDOT   x hourly x RESTDAY_SPECIAL x RESTSPECIAL_OT_PREMIUM (else HOLIDAY_OT) x NIGHTDIFF
internal sealed class CompoundedRestSpecialDayPay : CompoundedDayTypePayBase
{
    public CompoundedRestSpecialDayPay(RestSpecialDayPipeLine dayPipeline, DayTypePayInputs inputs, CompoundedPricers pricers)
        : base(dayPipeline, inputs, pricers) { }

    protected override PayCategory Category => PayCategories.RestSpecial;
}
