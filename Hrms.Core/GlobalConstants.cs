namespace Hrms.Core;

public static class RATE_DEFAULT
{
    // BASE RATES
    public const decimal REGULAR = 1.00m;
    public const decimal NIGHTDIFF = 1.10m;
    public const decimal OVERTIME = 1.25m;
    public const decimal RESTDAY_DUTY = 1.30m;
    public const decimal LEGAL_HOLIDAY = 1.00m; // holiday pay (no work)
    public const decimal LEGAL_HOLIDAY_DUTY = 2.00m;
    public const decimal SPECIAL_NON_WORKING = 1.30m;
    public const decimal SPECIAL_WORKING = 1.00m;
    public const decimal RESTDAY_SPECIAL = 1.50m; // rest day + special non-working

    // REST DAY
    //public const decimal RESTDAYDUTY_NIGHTDIFF = 1.43m;
    // LEGAL HOLIDAY
    //public const decimal LEGALHOLIDAY_DUTY_NIGHTDIFF = 2.20m;
    //public const decimal RESTDAYLEGALHOLIDAY = 2.60m;
    //public const decimal RESTDAYLEGALHOLIDAY_NIGHTDIFF = 2.86m;
    //public const decimal RESTDAYSPECIAL_NIGHTDIFF = 1.65m; // (150% × 1.10)

    // SPECIAL WORKING HOLIDAY (same as regular)
    //public const decimal SPECIALWORKINGHOLIDAY = 1.00m;
    //public const decimal SPECIALWORKINGHOLIDAY_NIGHTDIFF = 1.10m;

    // SPECIAL NON-WORKING HOLIDAY
    //public const decimal SPECIALNONWORKINGHOLIDAY_NIGHTDIFF = 1.43m;

    // OVERTIME RATES
    //public const decimal OT_REGULAR = 1.25m;
    //public const decimal OT_REGULAR_NIGHTDIFF = 1.375m;

    // REST DAY OT
    //public const decimal OT_RESTDAYDUTY = 1.69m; // 130% × 1.30
    //public const decimal OT_RESTDAYDUTY_NIGHTDIFF = 1.86m; // 143% × 1.30
    //public const decimal OT_RESTDAYDUTYSPECIAL = 1.95m; // 150% × 1.30

    // LEGAL HOLIDAY OT
    //public const decimal OT_LEGALHOLIDAY_DUTY = 2.60m; // 200% × 1.30
    //public const decimal OT_LEGALHOLIDAY_DUTY_NIGHTDIFF = 2.86m; // 220% × 1.30
    //public const decimal OT_RESTDAYSPECIAL_NIGHTDIFF = 2.15m; // 165% × 1.30

    //public const decimal OT_RESTDAYLEGALHOLIDAY = 3.38m; // 260% × 1.30
    //public const decimal OT_RESTDAYLEGALHOLIDAY_NIGHTDIFF = 3.72m; // 286% × 1.30

    // SPECIAL WORKING HOLIDAY OT (same as regular)
    //public const decimal OT_SPECIALWORKINGHOLIDAY = 1.25m;
    //public const decimal OT_SPECIALWORKINGHOLIDAY_NIGHTDIFF = 1.375m;

    // SPECIAL NON-WORKING HOLIDAY OT
    //public const decimal OT_SPECIALNONWORKINGHOLIDAY = 1.69m; // 130% × 1.30
    //public const decimal OT_SPECIALNONWORKINGHOLIDAY_NIGHTDIFF = 1.86m; // 143% × 1.30

}