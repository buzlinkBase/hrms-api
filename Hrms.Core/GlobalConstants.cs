namespace Hrms.Core;

public static class RATE_DEFAULT
{
    // ── Building-block multipliers ────────────────────────────────────────────
    public const decimal REGULAR = 1.00m;
    public const decimal NIGHTDIFF = 1.10m;
    public const decimal OVERTIME = 1.25m;
    public const decimal RESTDAY_DUTY = 1.30m;
    public const decimal LEGAL_HOLIDAY = 1.00m;   // no-work holiday pay
    public const decimal LEGAL_HOLIDAY_DUTY = 2.00m;   // worked on legal holiday
    public const decimal SPECIAL_WORKING = 1.00m;
    public const decimal SPECIAL_NON_WORKING = 1.30m;
    public const decimal RESTDAY_SPECIAL = 1.50m;

    // ── Compound DTR-column rates  (hours × rate = pay) ──────────────────────
    // Regular
    public const decimal REG        = 1.00m;
    public const decimal REG_OT     = 1.25m;            // 1.00 × 1.25
    public const decimal REG_ND     = 1.10m;            // 1.00 × 1.10
    public const decimal REG_ND_OT  = 1.375m;           // 1.00 × 1.10 × 1.25

    // Rest Day
    public const decimal RD         = 1.30m;
    public const decimal RD_OT      = 1.69m;            // 1.30 × 1.30
    public const decimal RD_ND      = 1.43m;            // 1.30 × 1.10
    public const decimal RD_ND_OT   = 1.859m;           // 1.30 × 1.10 × 1.30

    // Legal Holiday (worked)
    public const decimal LH         = 2.00m;
    public const decimal LH_OT      = 2.60m;            // 2.00 × 1.30
    public const decimal LH_ND      = 2.20m;            // 2.00 × 1.10
    public const decimal LH_ND_OT   = 2.86m;            // 2.00 × 1.10 × 1.30

    // Special Non-Working Holiday (worked)
    public const decimal SH         = 1.30m;
    public const decimal SH_OT      = 1.69m;            // 1.30 × 1.30
    public const decimal SH_ND      = 1.43m;            // 1.30 × 1.10
    public const decimal SH_ND_OT   = 1.859m;           // 1.30 × 1.10 × 1.30

    // Rest Day + Legal Holiday
    public const decimal RD_LH      = 2.60m;            // 2.00 + 30%
    public const decimal RD_LH_OT   = 3.38m;            // 2.60 × 1.30
    public const decimal RD_LH_ND   = 2.86m;            // 2.60 × 1.10
    public const decimal RD_LH_ND_OT = 3.718m;          // 2.60 × 1.10 × 1.30

    // Rest Day + Special Non-Working Holiday
    public const decimal RD_SH      = 1.50m;
    public const decimal RD_SH_OT   = 1.95m;            // 1.50 × 1.30
    public const decimal RD_SH_ND   = 1.65m;            // 1.50 × 1.10
    public const decimal RD_SH_ND_OT = 2.145m;          // 1.50 × 1.10 × 1.30

    // Special Working Holiday (same multiplier as regular)
    public const decimal SW         = 1.00m;
    public const decimal SW_OT      = 1.25m;
    public const decimal SW_ND      = 1.10m;
    public const decimal SW_ND_OT   = 1.375m;

    // Double Legal Holiday (worked) — DOLE: 300% for worked double holiday
    public const decimal DLH        = 3.00m;
    public const decimal DLH_OT     = 3.90m;            // 3.00 × 1.30
    public const decimal DLH_ND     = 3.30m;            // 3.00 × 1.10
    public const decimal DLH_ND_OT  = 4.29m;            // 3.00 × 1.10 × 1.30

    // Rest Day + Double Legal Holiday
    public const decimal RD_DLH     = 3.90m;            // 3.00 + 30%
    public const decimal RD_DLH_OT  = 5.07m;            // 3.90 × 1.30
    public const decimal RD_DLH_ND  = 4.29m;            // 3.90 × 1.10
    public const decimal RD_DLH_ND_OT = 5.577m;         // 3.90 × 1.10 × 1.30
}


public record PolicyRates(
    decimal Base = 1.00m,
    decimal NightDiff = 0.10m,        // +10%
    decimal Overtime = 0.25m,         // +25%
    decimal RestDayDuty = 0.30m,      // +30%
    decimal LegalHolidayDuty = 1.00m, // +100% (200% total)
    decimal SpecialHolidayDuty = 0.30m// +30% (130% total)
);

public static class RateCalculator
{
    // Derive rate dynamically: Base × (1 + ND%) × (1 + OT%)
    public static decimal Calculate(decimal dayRate, bool isOt, bool isNd, PolicyRates policy)
    {
        decimal rate = dayRate;
        if (isNd) rate *= (1 + policy.NightDiff);
        if (isOt) rate *= (1 + policy.Overtime);
        return Math.Round(rate, 4, MidpointRounding.AwayFromZero);
    }
}