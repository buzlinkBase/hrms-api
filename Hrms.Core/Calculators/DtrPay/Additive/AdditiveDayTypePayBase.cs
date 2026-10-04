namespace Hrms.Core.Calculators.DtrPay;

// The Additive method's bucket pricers, registered once and shared by its 8 day types. NdBase is
// only used where a day type keeps its night hours' base pay in the ND bucket (AdditiveRegularDayPay).
internal sealed record AdditivePricers(IBucketPricer NdBase, IBucketPricer Ot, IBucketPricer NdPremium);

// Additive, for one day type: the natural DTR hours, each bucket at ONE rate, added up --
//   Hrs (night included) x day rate  -> day pay, paid by the day-type policy itself (DayTypeHours)
//   OT  (OT + NDOT) x raw OT rate    -> OT pay, no day-type multiplier
//   ND  (ND + NDOT) x (ND rate - 1)  -> ND pay, the night premium only
//   NDOT            -> always 0 (its hours are already in OT and ND)
// Each step is its own virtual method so a day type with a genuine exception overrides just that
// step (see AdditiveRegularDayPay).
internal abstract class AdditiveDayTypePayBase : DayTypePayCalculatorBase, IAdditiveDayTypePay
{
    private readonly AdditivePricers _pricers;

    protected AdditiveDayTypePayBase(BasicPipelineBase dayPipeline, DayTypePayInputs inputs, AdditivePricers pricers)
        : base(dayPipeline, inputs)
    {
        _pricers = pricers;
    }

    protected AdditivePricers Pricers => _pricers;

    protected sealed override OtNdCalculationMethod Method => OtNdCalculationMethod.Additive;

    protected sealed override CategoryPay PriceHours(CategoryHours saved, CategoryRates rates, DayPay day, PayrollContext context)
    {
        var natural = NaturalHours.Join(saved);

        var ot = PriceOt(natural.Ot, rates);
        var nd = PriceNdPremium(natural.Nd, rates);
        nd = AddNightBaseToNd(saved.Nd, rates, nd, context);

        return new CategoryPay(Kind, day, ot, nd, BucketPay.Zero);
    }

    // OT + NDOT hours at this day type's raw OT rate.
    protected virtual BucketPay PriceOt(decimal hours, CategoryRates rates) => _pricers.Ot.Price(hours, rates);

    // ND + NDOT hours at the night premium only.
    protected virtual BucketPay PriceNdPremium(decimal hours, CategoryRates rates) => _pricers.NdPremium.Price(hours, rates);

    // Where the night regular hours' day-rate base is paid. Normally the day-type policy already
    // paid it with the rest of Hrs (DayTypeHours), so nothing is added here.
    protected virtual BucketPay AddNightBaseToNd(decimal nightRegularHours, CategoryRates rates, BucketPay nd, PayrollContext context) => nd;
}
