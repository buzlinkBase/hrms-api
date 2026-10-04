namespace Hrms.Core.Calculators.DtrPay;

// Additive -- Legal (regular) holiday. Hours: LegalHolHours / LegalHolOTHours / LegalHolNightDiffHours / LegalHolNightDiffOTHours.
//   Hrs         x hourly x LEGAL_HOLIDAY_DUTY   (night included, via RegularHolidayPipeLine)
//   OT          x hourly x LEGAL_HOLIDAY_OT_PREMIUM (else HOLIDAY_OT)
//   ND          x hourly x (NIGHTDIFF - 1)
//   NDOT        -> 0
internal sealed class AdditiveLegalHolidayPay : AdditiveDayTypePayBase
{
    public AdditiveLegalHolidayPay(RegularHolidayPipeLine dayPipeline, DayTypePayInputs inputs, AdditivePricers pricers)
        : base(dayPipeline, inputs, pricers) { }

    protected override PayCategory Category => PayCategories.Legal;
}
