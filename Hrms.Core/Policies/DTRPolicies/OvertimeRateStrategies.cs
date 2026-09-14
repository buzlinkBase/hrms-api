namespace Hrms.Core.Policies.DTRPolicies;

// Strategy for how a SingleCategoryOTPolicy resolves its two rate tiers for a given day/employee.
// DayRate is the multiplier for a non-OT hour of the same day-type (e.g. 2.00 for Legal Holiday)
// -- kept separate from FullRate purely so OTPremium can keep meaning "the amount above the base
// day rate," same as before this was extracted. FullRate is the multiplier actually applied to
// the OT hours themselves.
internal interface IOtRateStrategy
{
    (decimal DayRate, decimal FullRate) Resolve(PayrollContext context);
}

// The standard formula -- day-type rate(s) compounded with the shared HOLIDAY_OT (or OVERTIME
// for plain overtime) rate. This is a straight relocation of what SingleCategoryOTPolicy used to
// compute inline; the logic itself is unchanged.
internal class CompoundedOtRateStrategy : IOtRateStrategy
{
    private readonly RateType[] _policyRateTypes;

    public CompoundedOtRateStrategy(params RateType[] policyRateTypes)
    {
        _policyRateTypes = policyRateTypes;
    }

    public (decimal DayRate, decimal FullRate) Resolve(PayrollContext context)
    {
        // Establish the Day-Type Base Multiplier (e.g., Regular = 1.0, RestDay = 1.3, Legal = 2.0)
        // -- multiplies out compound day bases like Rest Day + Legal Day (1.30 * 2.00 = 2.60).
        decimal dayTypeMultiplier = 1.0m;
        foreach (var rateType in _policyRateTypes)
        {
            if (rateType != RateType.HOLIDAY_OT && rateType != RateType.OVERTIME)
            {
                dayTypeMultiplier *= PremiumRateHelper.GetRate(context, rateType, 1.0m);
            }
        }

        // Fetch the standard operational Overtime multiplier -- typically defaults to 1.25.
        var otRateMultiplier = _policyRateTypes.Contains(RateType.HOLIDAY_OT) || _policyRateTypes.Contains(RateType.OVERTIME)
            ? PremiumRateHelper.GetRate(context, _policyRateTypes.Contains(RateType.HOLIDAY_OT) ? RateType.HOLIDAY_OT : RateType.OVERTIME, 1.25m)
            : 1.0m;

        return (dayTypeMultiplier, dayTypeMultiplier * otRateMultiplier);
    }
}

// Setup > Client > Settings > Rate Multipliers -- a client-negotiated flat OT rate for one
// holiday/rest-day category, entirely decoupled from the standard day-type x HOLIDAY_OT
// compounding. Decorates (wraps) the standard strategy rather than reimplementing it: when no
// override is configured for this client (or company), this is a pure no-op that returns
// exactly what the wrapped strategy already computed. DayRate is always the wrapped strategy's
// own DayRate -- an override only ever replaces FullRate (the OT rate), never the base day-type
// rate used for that same category's regular/non-OT pay elsewhere (DayTypePayCalculator), so a
// client's regular Legal/Special Holiday pay can never be silently changed by this.
internal class ClientOverrideOtRateStrategy : IOtRateStrategy
{
    private readonly RateType _overrideType;
    private readonly IOtRateStrategy _standard;

    public ClientOverrideOtRateStrategy(RateType overrideType, IOtRateStrategy standard)
    {
        _overrideType = overrideType;
        _standard = standard;
    }

    public (decimal DayRate, decimal FullRate) Resolve(PayrollContext context)
    {
        var (dayRate, standardFullRate) = _standard.Resolve(context);
        return PremiumRateHelper.TryGetRate(context, _overrideType, out var directTotal)
            ? (dayRate, directTotal)
            : (dayRate, standardFullRate);
    }
}
