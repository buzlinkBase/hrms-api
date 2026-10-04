namespace Hrms.Core.Calculators.DtrPay;

// Immutable value objects passed between the pay-calculation steps -- nothing is accumulated onto
// a shared mutable line, so every step is a pure function of its inputs.

// One category's hours for one day, as decimals (DailyRecord stores doubles).
internal readonly record struct CategoryHours(decimal Hrs, decimal Ot, decimal Nd, decimal Ndot)
{
    // True when there's anything for the OT / ND / NDOT pricers to price.
    public bool HasPremiumHours => Ot > 0 || Nd > 0 || Ndot > 0;
}

// One category's rates for one day: the employee's hourly rate, the day-type multiplier (e.g. 1.30
// for a rest day), this category's raw OT rate (client override aware) and the night-diff rate.
internal readonly record struct CategoryRates(decimal Hourly, decimal Day, decimal RawOt, decimal Nd);

// What one hour bucket (OT, ND or NDOT) of one category pays. Same five figures the old
// BasicPipelineData carried, with the same meanings:
// - Value: the amount actually paid.
// - OTPremium / NDPremium: the parts of Value above the plain day rate (summed into the run's
//   OTPremiumPay / NDPremiumPay).
// - FlatOvertimeBase / FlatNightDiffPremium: the single-rate "hours x one clean rate" figures the
//   Hours payslip and reports show ({Cat}OTBasePay / NDOTBasePay / NDPremiumPay / NDOTPremiumPay).
internal sealed record BucketPay(
    decimal Value,
    decimal OTPremium,
    decimal NDPremium,
    decimal FlatOvertimeBase,
    decimal FlatNightDiffPremium)
{
    public static readonly BucketPay Zero = new(0m, 0m, 0m, 0m, 0m);

    // The day-rate-only portion of an ND bucket ({Cat}NDBasePay).
    public decimal BasePortion => Value - NDPremium;
}

// What the plain worked/unworked hours of one category pay (the day-type policies' output).
// Worked/Unworked are only reported for the legal-holiday categories.
internal sealed record DayPay(decimal Value, decimal Worked, decimal Unworked)
{
    // Extra worked-hour pay priced at this day's own rate (Additive's folded-in night hours).
    public DayPay AddWorked(decimal amount) => this with { Value = Value + amount, Worked = Worked + amount };
}

// Everything one category pays for one day.
internal sealed record CategoryPay(PayCategoryKind Kind, DayPay Day, BucketPay Ot, BucketPay Nd, BucketPay Ndot)
{
    public static CategoryPay DayOnly(PayCategoryKind kind, DayPay day) =>
        new(kind, day, BucketPay.Zero, BucketPay.Zero, BucketPay.Zero);
}

// The day's attendance-driven amounts -- identical in both pay methods.
internal sealed record AttendancePay(decimal Absent, decimal Late, decimal Undertime, decimal PaidLeave, decimal UnpaidLeave);
