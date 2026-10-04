namespace Hrms.Core.Calculators.DtrPay;

// Compounded -- Special non-working holiday. Hours: SpecialHolHours / SpecialHolOTHours / SpecialHolNightDiffHours / SpecialHolNightDiffOTHours.
//   daytime Hrs (Hrs - (ND - NDOT)) via SpecialWorkDayPipeLine
//   OT - NDOT x hourly x SPECIAL_NON_WORKING x SPECIAL_HOLIDAY_OT_PREMIUM (else HOLIDAY_OT)
//   ND - NDOT x hourly x SPECIAL_NON_WORKING x NIGHTDIFF
//   NDOT   x hourly x SPECIAL_NON_WORKING x SPECIAL_HOLIDAY_OT_PREMIUM (else HOLIDAY_OT) x NIGHTDIFF
internal sealed class CompoundedSpecialHolidayPay : CompoundedDayTypePayBase
{
    public CompoundedSpecialHolidayPay(SpecialWorkDayPipeLine dayPipeline, DayTypePayInputs inputs, CompoundedPricers pricers)
        : base(dayPipeline, inputs, pricers) { }

    protected override PayCategory Category => PayCategories.Special;
}
