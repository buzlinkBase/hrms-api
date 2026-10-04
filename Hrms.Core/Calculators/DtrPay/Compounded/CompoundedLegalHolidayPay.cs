namespace Hrms.Core.Calculators.DtrPay;

// Compounded -- Legal (regular) holiday. Hours: LegalHolHours / LegalHolOTHours / LegalHolNightDiffHours / LegalHolNightDiffOTHours.
//   Hrs    via RegularHolidayPipeLine
//   OT     x hourly x LEGAL_HOLIDAY_DUTY x LEGAL_HOLIDAY_OT_PREMIUM (else HOLIDAY_OT)
//   ND     x hourly x LEGAL_HOLIDAY_DUTY x NIGHTDIFF
//   NDOT   x hourly x LEGAL_HOLIDAY_DUTY x LEGAL_HOLIDAY_OT_PREMIUM (else HOLIDAY_OT) x NIGHTDIFF
internal sealed class CompoundedLegalHolidayPay : CompoundedDayTypePayBase
{
    public CompoundedLegalHolidayPay(RegularHolidayPipeLine dayPipeline, DayTypePayInputs inputs, CompoundedPricers pricers)
        : base(dayPipeline, inputs, pricers) { }

    protected override PayCategory Category => PayCategories.Legal;
}
