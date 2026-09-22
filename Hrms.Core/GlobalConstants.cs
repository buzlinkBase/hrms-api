namespace Hrms.Core;

public static class RATE_DEFAULT
{
    public const decimal REGULAR = 1.00m;
    public const decimal NIGHTDIFF = 1.10m;
    public const decimal OVERTIME = 1.25m;
    public const decimal HOLIDAY_OT = 1.30m;
    public const decimal RESTDAY_DUTY = 1.30m;
    public const decimal LEGAL_HOLIDAY = 1.00m;
    public const decimal LEGAL_HOLIDAY_DUTY = 2.00m;
    public const decimal SPECIAL_WORKING = 1.00m;
    public const decimal SPECIAL_NON_WORKING = 1.30m;
    public const decimal RESTDAY_SPECIAL = 1.50m;

    // Per-category OT PREMIUM overrides (Setup > Client > Settings > Rate Multipliers) --
    // client-only by design (never seeded/edited company-wide). CompoundedOtRateStrategy.
    // ResolveRawOtRate's real fallback for these is HOLIDAY_OT's own full resolution, not these
    // constants directly -- these exist only for RATE_DEFAULT.For()'s defensive completeness
    // (matching every other RateType having an explicit case, per this session's RESTHOLOVERTIME/
    // fallback-bug cleanup) and default to the same 1.30 HOLIDAY_OT starts at.
    public const decimal RESTDAY_OT_PREMIUM = 1.30m;
    public const decimal LEGAL_HOLIDAY_OT_PREMIUM = 1.30m;
    public const decimal SPECIAL_HOLIDAY_OT_PREMIUM = 1.30m;
    public const decimal RESTLEGAL_OT_PREMIUM = 1.30m;
    public const decimal RESTSPECIAL_OT_PREMIUM = 1.30m;
    public const decimal DOUBLELEGAL_OT_PREMIUM = 1.30m;
    public const decimal RESTDOUBLELEGAL_OT_PREMIUM = 1.30m;

    // Single source of truth for "what should this RateType fall back to when neither a client
    // nor a company-wide rate is configured" (PremiumRateHelper.GetRate's `fallback` parameter).
    // Every DTRPolicies call site should resolve its fallback through here instead of a
    // hardcoded literal -- those literals had drifted out of sync with the constants above (a
    // day-type-multiplier loop was falling back to 1.0m for EVERY RateType regardless of which
    // one, e.g. paying an unconfigured Rest Day as if it were a Regular day; HOLIDAY_OT was
    // falling back to 1.25m, OVERTIME's rate, instead of its own 1.30m).
    public static decimal For(RateType type) => type switch
    {
        RateType.REGULAR => REGULAR,
        RateType.NIGHTDIFF => NIGHTDIFF,
        RateType.OVERTIME => OVERTIME,
        RateType.HOLIDAY_OT => HOLIDAY_OT,
        RateType.RESTDAY_DUTY => RESTDAY_DUTY,
        RateType.LEGAL_HOLIDAY => LEGAL_HOLIDAY,
        RateType.LEGAL_HOLIDAY_DUTY => LEGAL_HOLIDAY_DUTY,
        RateType.SPECIAL_WORKING => SPECIAL_WORKING,
        RateType.SPECIAL_NON_WORKING => SPECIAL_NON_WORKING,
        RateType.RESTDAY_SPECIAL => RESTDAY_SPECIAL,
        RateType.RESTDAY_OT_PREMIUM => RESTDAY_OT_PREMIUM,
        RateType.LEGAL_HOLIDAY_OT_PREMIUM => LEGAL_HOLIDAY_OT_PREMIUM,
        RateType.SPECIAL_HOLIDAY_OT_PREMIUM => SPECIAL_HOLIDAY_OT_PREMIUM,
        RateType.RESTLEGAL_OT_PREMIUM => RESTLEGAL_OT_PREMIUM,
        RateType.RESTSPECIAL_OT_PREMIUM => RESTSPECIAL_OT_PREMIUM,
        RateType.DOUBLELEGAL_OT_PREMIUM => DOUBLELEGAL_OT_PREMIUM,
        RateType.RESTDOUBLELEGAL_OT_PREMIUM => RESTDOUBLELEGAL_OT_PREMIUM,
        _ => HOLIDAY_OT,
    };
}


/// <summary>
/// The 9 configurable base-rate multipliers that drive all compound calculations.
/// All values are full multipliers (e.g. NightDiff = 1.10 means 110% of the base rate).
/// Defaults match RATE_DEFAULT constants so a default instance works out of the box.
/// </summary>
public record PolicyRates(
    decimal Regular          = RATE_DEFAULT.REGULAR,
    decimal NightDiff        = RATE_DEFAULT.NIGHTDIFF,
    decimal Overtime         = RATE_DEFAULT.OVERTIME,
    decimal RestDayDuty      = RATE_DEFAULT.RESTDAY_DUTY,
    decimal LegalHoliday     = RATE_DEFAULT.LEGAL_HOLIDAY,
    decimal LegalHolidayDuty = RATE_DEFAULT.LEGAL_HOLIDAY_DUTY,
    decimal SpecialWorking   = RATE_DEFAULT.SPECIAL_WORKING,
    decimal SpecialNonWorking = RATE_DEFAULT.SPECIAL_NON_WORKING,
    decimal RestDaySpecial   = RATE_DEFAULT.RESTDAY_SPECIAL
);

public static class RateCalculator
{
    /// <summary>
    /// Applies ND and OT multipliers to a pre-resolved day-rate multiplier.
    /// </summary>
    public static decimal Calculate(decimal dayRate, bool isOt, bool isNd, PolicyRates policy)
    {
        decimal rate = dayRate;
        if (isNd) rate *= policy.NightDiff;
        if (isOt) rate *= policy.Overtime;
        return Round(rate);
    }

    /// <summary>
    /// Returns the base day-rate multiplier for a given work type, before ND/OT.
    /// e.g. RestDayLegalHolidayDuty = RestDayDuty × LegalHolidayDuty
    /// </summary>
    public static decimal DayRate(WorkType workType, PolicyRates policy) => workType switch
    {
        WorkType.RegularWorkDay             => policy.Regular,
        WorkType.SpecialWorkingHoliday      => policy.SpecialWorking,
        WorkType.RestDayDuty                => policy.RestDayDuty,
        WorkType.LegalHoliday               => policy.LegalHoliday,
        WorkType.LegalHolidayDuty           => policy.LegalHolidayDuty,
        WorkType.SpecialNonWorkingHoliday   => policy.SpecialNonWorking,
        WorkType.SpecialHolidayDuty         => policy.SpecialNonWorking,
        WorkType.RestDayLegalHolidayDuty    => policy.RestDayDuty * policy.LegalHolidayDuty,
        WorkType.RestDaySpecialHolidayDuty  => policy.RestDaySpecial,
        _                                   => policy.Regular,
    };

    /// <summary>
    /// Derives the full compound multiplier for a given work type + ND/OT flags.
    /// </summary>
    public static decimal Compound(WorkType workType, bool isOt, bool isNd, PolicyRates policy)
        => Calculate(DayRate(workType, policy), isOt, isNd, policy);

    /// <summary>
    /// Returns only the night-diff premium portion for a given day rate.
    /// Used by NightDiffPolicy: hr × hourlyRate × NdPremium(dayRate)
    /// </summary>
    public static decimal NdPremium(decimal dayRate, PolicyRates policy)
        => Round(dayRate * (policy.NightDiff - 1.00m));

    /// <summary>
    /// Returns the night-diff premium for a given work type (before OT).
    /// </summary>
    public static decimal NdPremium(WorkType workType, PolicyRates policy)
        => NdPremium(DayRate(workType, policy), policy);

    private static decimal Round(decimal v)
        => Math.Round(v, 4, MidpointRounding.AwayFromZero);
}