namespace Hrms.Core.Policies.DTRPolicies;

// Strategy for how a day-type category (Calculators/DtrPay PayCategories) resolves its rates for a
// given day/employee: the day-type multiplier (e.g. 2.00 for Legal Holiday) and the category's own
// raw OT multiplier. Each pay method combines them its own way (Compounded: day x OT; Additive: OT
// alone).
internal interface IOtRateStrategy
{
    decimal ResolveDayRate(PayrollContext context);

    // The category's own OT/premium RateType, ignoring day-type compounding.
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

    public decimal ResolveDayRate(PayrollContext context)
    {
        // Multiplies out compound day bases like Rest Day + Legal Day (1.30 * 2.00 = 2.60).
        decimal dayTypeMultiplier = 1.0m;
        foreach (var rateType in _dayTypeRateTypes)
        {
            dayTypeMultiplier *= PremiumRateHelper.GetRate(context, rateType, RATE_DEFAULT.For(rateType));
        }
        return dayTypeMultiplier;
    }

    public decimal ResolveRawOtRate(PayrollContext context)
    {
        var fallback = _otFallbackType.HasValue
            ? PremiumRateHelper.GetRate(context, _otFallbackType.Value, RATE_DEFAULT.For(_otFallbackType.Value))
            : RATE_DEFAULT.For(_otType);
        return PremiumRateHelper.GetRate(context, _otType, fallback);
    }
}
