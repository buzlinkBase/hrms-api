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