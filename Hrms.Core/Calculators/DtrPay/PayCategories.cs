namespace Hrms.Core.Calculators.DtrPay;

// The 8 DTR day-type categories a day's hours can fall into. Every one of them has the same four
// hour buckets (Hrs / OT / ND / NDOT) on DailyRecord, priced against that day type's own rates.
internal enum PayCategoryKind
{
    Regular,
    RestDay,
    Legal,
    Special,
    RestLegal,
    RestSpecial,
    DoubleLegal,
    RestDoubleLegal,
}

// One row of the category table (Registry): which DailyRecord fields hold this day type's hours,
// and which rates price them. OtRates is the same CompoundedOtRateStrategy each category's OT and
// NDOT policies used to own -- it also yields the day-type multiplier (Resolve().DayRate), which is
// exactly the product the ND policies computed from their own rate list, in the same order -- so a
// category's OT, ND and NDOT hours can never resolve to different rates in either pay method.
internal sealed record PayCategory(
    PayCategoryKind Kind,
    Func<DailyRecordRunModel, double> Hrs,
    Func<DailyRecordRunModel, double> Ot,
    Func<DailyRecordRunModel, double> Nd,
    Func<DailyRecordRunModel, double> Ndot,
    IOtRateStrategy OtRates)
{
    public CategoryHours HoursOf(DailyRecordRunModel r) =>
        new((decimal)Hrs(r), (decimal)Ot(r), (decimal)Nd(r), (decimal)Ndot(r));
}

// Adding a day type is one row here plus its day-type pipeline in DayTypePayPricer -- nothing in
// either pay method or the model builder's per-category plumbing has to learn about it.
internal static class PayCategories
{
    public static readonly PayCategory Regular = new(
        PayCategoryKind.Regular,
        r => r.RegularNetHours, r => r.RegularOTHours, r => r.RegularNDHours, r => r.RegularNDOTHours,
        new CompoundedOtRateStrategy(RateType.OVERTIME, null));

    public static readonly PayCategory RestDay = new(
        PayCategoryKind.RestDay,
        r => r.RestDayHours, r => r.RestDayOTHours, r => r.RestDayNDHours, r => r.RestDayNDOTHours,
        new CompoundedOtRateStrategy(RateType.RESTDAY_OT_PREMIUM, RateType.HOLIDAY_OT, RateType.RESTDAY_DUTY));

    public static readonly PayCategory Legal = new(
        PayCategoryKind.Legal,
        r => r.LegalHolHours, r => r.LegalHolOTHours, r => r.LegalHolNightDiffHours, r => r.LegalHolNightDiffOTHours,
        new CompoundedOtRateStrategy(RateType.LEGAL_HOLIDAY_OT_PREMIUM, RateType.HOLIDAY_OT, RateType.LEGAL_HOLIDAY_DUTY));

    public static readonly PayCategory Special = new(
        PayCategoryKind.Special,
        r => r.SpecialHolHours, r => r.SpecialHolOTHours, r => r.SpecialHolNightDiffHours, r => r.SpecialHolNightDiffOTHours,
        new CompoundedOtRateStrategy(RateType.SPECIAL_HOLIDAY_OT_PREMIUM, RateType.HOLIDAY_OT, RateType.SPECIAL_NON_WORKING));

    public static readonly PayCategory RestLegal = new(
        PayCategoryKind.RestLegal,
        r => r.RestLegalDayHours, r => r.RestLegalDayOTHours, r => r.RestLegalDayNDHours, r => r.RestLegalDayNDOTHours,
        new CompoundedOtRateStrategy(RateType.RESTLEGAL_OT_PREMIUM, RateType.HOLIDAY_OT, RateType.RESTDAY_DUTY, RateType.LEGAL_HOLIDAY_DUTY));

    public static readonly PayCategory RestSpecial = new(
        PayCategoryKind.RestSpecial,
        r => r.RestSpecialDayHours, r => r.RestSpecialDayOTHours, r => r.RestSpecialDayNDHours, r => r.RestSpecialDayNDOTHours,
        new CompoundedOtRateStrategy(RateType.RESTSPECIAL_OT_PREMIUM, RateType.HOLIDAY_OT, RateType.RESTDAY_SPECIAL));

    public static readonly PayCategory DoubleLegal = new(
        PayCategoryKind.DoubleLegal,
        r => r.DoubleLegalHours, r => r.DoubleLegalOTHours, r => r.DoubleLegalNDHours, r => r.DoubleLegalNDOTHours,
        new CompoundedOtRateStrategy(RateType.DOUBLELEGAL_OT_PREMIUM, RateType.HOLIDAY_OT, RateType.LEGAL_HOLIDAY_DUTY, RateType.LEGAL_HOLIDAY_DUTY));

    public static readonly PayCategory RestDoubleLegal = new(
        PayCategoryKind.RestDoubleLegal,
        r => r.RestDoubleLegalHours, r => r.RestDoubleLegalOTHours, r => r.RestDoubleLegalNDHours, r => r.RestDoubleLegalNDOTHours,
        new CompoundedOtRateStrategy(RateType.RESTDOUBLELEGAL_OT_PREMIUM, RateType.HOLIDAY_OT, RateType.RESTDAY_DUTY, RateType.LEGAL_HOLIDAY_DUTY, RateType.LEGAL_HOLIDAY_DUTY));

    public static readonly IReadOnlyList<PayCategory> All =
        [Regular, RestDay, Legal, Special, RestLegal, RestSpecial, DoubleLegal, RestDoubleLegal];
}
