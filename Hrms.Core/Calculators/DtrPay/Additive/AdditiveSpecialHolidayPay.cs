namespace Hrms.Core.Calculators.DtrPay;

// Additive -- Special non-working holiday. Hours: SpecialHolHours / SpecialHolOTHours / SpecialHolNightDiffHours / SpecialHolNightDiffOTHours.
//   Hrs + ND    x hourly x SPECIAL_NON_WORKING   (plain hours via SpecialWorkDayPipeLine)
//   OT + NDOT   x hourly x SPECIAL_HOLIDAY_OT_PREMIUM (else HOLIDAY_OT)
//   ND + NDOT   x hourly x (NIGHTDIFF - 1)
//   NDOT        -> 0
internal sealed class AdditiveSpecialHolidayPay : AdditiveDayTypePayBase
{
    public AdditiveSpecialHolidayPay(SpecialWorkDayPipeLine dayPipeline, DayTypePayInputs inputs, AdditivePricers pricers)
        : base(dayPipeline, inputs, pricers) { }

    protected override PayCategory Category => PayCategories.Special;
}
