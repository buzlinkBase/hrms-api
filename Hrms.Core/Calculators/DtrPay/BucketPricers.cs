namespace Hrms.Core.Calculators.DtrPay;

// Strategy: prices one hour bucket of one category. Each implementation is one formula, with no
// knowledge of which pay method it belongs to -- the calculators compose them.
internal interface IBucketPricer
{
    BucketPay Price(decimal hours, CategoryRates rates);
}

// Shared guard + "hours x hourly rate" base every formula below starts from (same operation order
// the old policies used, so Compounded results stay identical to the decimal).
internal abstract class BucketPricerBase : IBucketPricer
{
    public BucketPay Price(decimal hours, CategoryRates rates) =>
        hours <= 0m ? BucketPay.Zero : PriceBasePay(hours * rates.Hourly, rates);

    protected abstract BucketPay PriceBasePay(decimal basePay, CategoryRates rates);
}

// ── Compounded (saved hours x compounded rates) ──────────────────────────────────────────────

// OT hours: day rate x OT rate. OTPremium is everything above the plain day rate -- negative
// when a client's negotiated OT rate is below its own day rate, which is correct.
internal sealed class CompoundedOtPricer : BucketPricerBase
{
    protected override BucketPay PriceBasePay(decimal basePay, CategoryRates rates)
    {
        var fullRate = rates.Day * rates.RawOt;
        return new BucketPay(
            Value: basePay * fullRate,
            OTPremium: basePay * (fullRate - rates.Day),
            NDPremium: 0m,
            FlatOvertimeBase: basePay * rates.RawOt,
            FlatNightDiffPremium: 0m);
    }
}

// ND hours: day rate x ND rate. NDPremium is the part above the plain day rate, so
// Value - NDPremium is the day-rate-only portion ({Cat}NDBasePay).
internal sealed class CompoundedNdPricer : BucketPricerBase
{
    protected override BucketPay PriceBasePay(decimal basePay, CategoryRates rates)
    {
        var fullRate = rates.Day * rates.Nd;
        return new BucketPay(
            Value: basePay * fullRate,
            OTPremium: 0m,
            NDPremium: basePay * (fullRate - rates.Day),
            FlatOvertimeBase: 0m,
            FlatNightDiffPremium: basePay * (rates.Nd - 1.0m));
    }
}

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

// ── Additive (natural hours x one rate each) ─────────────────────────────────────────────────

// Night regular hours' base: paid at this day's own rate, in full (night hours were never
// Fixed-salary pre-funded). Only used for a Fixed employee's regular day (AdditiveRegularDayPay),
// whose night base stays in the ND bucket. Value is the only figure.
internal sealed class AdditiveNdBasePricer : BucketPricerBase
{
    protected override BucketPay PriceBasePay(decimal basePay, CategoryRates rates) =>
        new(Value: basePay * rates.Day, OTPremium: 0m, NDPremium: 0m, FlatOvertimeBase: 0m, FlatNightDiffPremium: 0m);
}

// Natural OT hours (OT + NDOT): the raw OT rate alone, no day-type multiplier. With no day-rate
// portion to net out, the whole amount is OT premium, and it is also the flat OT base.
internal sealed class AdditiveOtPricer : BucketPricerBase
{
    protected override BucketPay PriceBasePay(decimal basePay, CategoryRates rates)
    {
        var pay = basePay * rates.RawOt;
        return new BucketPay(Value: pay, OTPremium: pay, NDPremium: 0m, FlatOvertimeBase: pay, FlatNightDiffPremium: 0m);
    }
}

// Natural ND hours (ND + NDOT): only the night premium, (ND rate - 1). Their base pay is already
// in the day pay (the day-type policy, via DayTypeHours) or, for NDOT, in the OT bucket.
internal sealed class AdditiveNdPremiumPricer : BucketPricerBase
{
    protected override BucketPay PriceBasePay(decimal basePay, CategoryRates rates)
    {
        var premium = basePay * (rates.Nd - 1.0m);
        return new BucketPay(Value: premium, OTPremium: 0m, NDPremium: premium, FlatOvertimeBase: 0m, FlatNightDiffPremium: premium);
    }
}

// NDOT policy, Additive: always 0. NDOT hours are already paid in the natural buckets -- their base
// in the day pay, their OT in the OT bucket (OT + NDOT) and their night premium in the ND bucket
// (ND + NDOT) -- so there is nothing left to price.
internal sealed class AdditiveNdotPolicy : IBucketPricer
{
    public BucketPay Price(decimal hours, CategoryRates rates) => BucketPay.Zero;
}
