namespace Hrms.Core.Calculators.DtrPay;

// The NDOT (night-differential overtime) policies, one per pay method. The DtrPay calculators call
// the one for their method for every day type's NDOT hours (see AdditiveDayTypePayBase /
// CompoundedDayTypePayBase).

// NDOT policy, Compounded: NDOT hrs x hourly x day rate x OT rate x ND rate. The OT and ND premiums are the differences between
// consecutive tiers, so day-rate portion + OTPremium + NDPremium telescopes back to Value.
internal sealed class CompoundedNdotPolicy : BucketPricerBase
{
    protected override BucketPay PriceBasePay(decimal basePay, CategoryRates rates)
    {
        var overtimeRate = rates.Day * rates.RawOt;
        var fullRate = overtimeRate * rates.Nd;
        return new BucketPay(
            Value: basePay * fullRate,
            OTPremium: basePay * (overtimeRate - rates.Day),
            NDPremium: basePay * (fullRate - overtimeRate),
            FlatOvertimeBase: basePay * rates.RawOt,
            FlatNightDiffPremium: basePay * (rates.Nd - 1.0m));
    }
}

// NDOT policy, Additive: always 0. NDOT hours are already paid in the natural buckets -- their base
// in the day pay, their OT in the OT bucket (OT + NDOT) and their night premium in the ND bucket
// (ND + NDOT) -- so there is nothing left to price.
internal sealed class AdditiveNdotPolicy : IBucketPricer
{
    public BucketPay Price(decimal hours, CategoryRates rates) => BucketPay.Zero;
}
