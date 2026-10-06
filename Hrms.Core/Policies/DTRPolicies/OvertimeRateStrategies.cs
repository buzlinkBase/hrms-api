namespace Hrms.Core.Policies.DTRPolicies; 
internal interface IOtRateStrategy
{
    decimal ResolveDayRate(PayrollContext context); 
    decimal ResolveRawOtRate(PayrollContext context);
}
 
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
