namespace Hrms.Core.Calculators.DtrPay;

// Compounded -- Special holiday falling on a rest day. Hours: RestSpecialDayHours / RestSpecialDayOTHours / RestSpecialDayNDHours / RestSpecialDayNDOTHours.
//   Hrs    via RestSpecialDayPipeLine
//   OT     x hourly x RESTDAY_SPECIAL x RESTSPECIAL_OT_PREMIUM (else HOLIDAY_OT)
//   ND     x hourly x RESTDAY_SPECIAL x NIGHTDIFF
//   NDOT   x hourly x RESTDAY_SPECIAL x RESTSPECIAL_OT_PREMIUM (else HOLIDAY_OT) x NIGHTDIFF
internal sealed class CompoundedRestSpecialDayPay : CompoundedDayTypePayBase
{
    public CompoundedRestSpecialDayPay(RestSpecialDayPipeLine dayPipeline, DayTypePayInputs inputs, CompoundedPricers pricers)
        : base(dayPipeline, inputs, pricers) { }

    protected override PayCategory Category => PayCategories.RestSpecial;
}
