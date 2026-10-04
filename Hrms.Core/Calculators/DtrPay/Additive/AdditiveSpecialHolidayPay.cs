namespace Hrms.Core.Calculators.DtrPay;

// Additive -- Special non-working holiday. Hours: SpecialHolHours / SpecialHolOTHours / SpecialHolNightDiffHours / SpecialHolNightDiffOTHours.
//   Hrs         x hourly x SPECIAL_NON_WORKING   (night included, via SpecialWorkDayPipeLine)
//   OT          x hourly x SPECIAL_HOLIDAY_OT_PREMIUM (else HOLIDAY_OT)
//   ND          x hourly x (NIGHTDIFF - 1)
//   NDOT        -> 0
internal sealed class AdditiveSpecialHolidayPay : AdditiveDayTypePayBase
{
    public AdditiveSpecialHolidayPay(SpecialWorkDayPipeLine dayPipeline, DayTypePayInputs inputs, AdditivePricers pricers)
        : base(dayPipeline, inputs, pricers) { }

    protected override PayCategory Category => PayCategories.Special;
}
