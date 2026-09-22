namespace Hrms.Core.Policies.DTRPolicies;

// Strategy for how a SingleCategoryOTPolicy resolves its two rate tiers for a given day/employee.
// DayRate is the multiplier for a non-OT hour of the same day-type (e.g. 2.00 for Legal Holiday)
// -- kept separate from FullRate purely so OTPremium can keep meaning "the amount above the base
// day rate," same as before this was extracted. FullRate is the multiplier actually applied to
// the OT hours themselves.
internal interface IOtRateStrategy
{
    (decimal DayRate, decimal FullRate) Resolve(PayrollContext context);

    // The raw OT-only multiplier (this category's own OT/premium RateType), ignoring day-type
    // compounding -- used for the segregated "OT Base" recording (BasicPipelineData.
    // FlatOvertimeBase) as well as Resolve's FullRate above.
    decimal ResolveRawOtRate(PayrollContext context);
}

// The standard formula -- day-type rate(s) compounded with this category's own OT-tier rate.
// otType is resolved client-first, then company-wide, then (if neither is configured) via
// otFallbackType's own full resolution -- e.g. a category's dedicated OT premium
// (RESTDAY_OT_PREMIUM) falls back to the shared HOLIDAY_OT rate when a client hasn't overridden
// it, rather than a flat constant, so "not overridden" means "use the shared Non-Regular OT
// rate" exactly like before per-category premiums existed. otFallbackType is null for plain
// OVERTIME (Regular was never shared with anything to fall back to).
internal class CompoundedOtRateStrategy : IOtRateStrategy
{
    private readonly RateType _otType;
    private readonly RateType? _otFallbackType;
    private readonly RateType[] _dayTypeRateTypes;

    public CompoundedOtRateStrategy(RateType otType, RateType? otFallbackType, params RateType[] dayTypeRateTypes)
    {
        _otType = otType;
        _otFallbackType = otFallbackType;
        _dayTypeRateTypes = dayTypeRateTypes;
    }

    public (decimal DayRate, decimal FullRate) Resolve(PayrollContext context)
    {
        // Establish the Day-Type Base Multiplier (e.g., Regular = 1.0, RestDay = 1.3, Legal = 2.0)
        // -- multiplies out compound day bases like Rest Day + Legal Day (1.30 * 2.00 = 2.60).
        decimal dayTypeMultiplier = 1.0m;
        foreach (var rateType in _dayTypeRateTypes)
        {
            dayTypeMultiplier *= PremiumRateHelper.GetRate(context, rateType, RATE_DEFAULT.For(rateType));
        }

        var otRateMultiplier = ResolveRawOtRate(context);
        return (dayTypeMultiplier, dayTypeMultiplier * otRateMultiplier);
    }

    public decimal ResolveRawOtRate(PayrollContext context)
    {
        var fallback = _otFallbackType.HasValue
            ? PremiumRateHelper.GetRate(context, _otFallbackType.Value, RATE_DEFAULT.For(_otFallbackType.Value))
            : RATE_DEFAULT.For(_otType);
        return PremiumRateHelper.GetRate(context, _otType, fallback);
    }
}
