namespace Hrms.Core.Calculators.DtrPay;

// The Compounded method's bucket pricers, registered once and shared by its 8 day types.
internal sealed record CompoundedPricers(IBucketPricer Ot, IBucketPricer Nd, IBucketPricer Ndot);

// Compounded (DOLE-standard), for one day type: the DTR hours separated into daytime / night
// buckets (CategoryHours.Separate) -- the day-type policy pays the daytime Hrs -- each
// bucket at its fully compounded rate --
//   OT   x day rate x OT rate
//   ND   x day rate x ND rate
//   NDOT x day rate x OT rate x ND rate
internal abstract class CompoundedDayTypePayBase : DayTypePayCalculatorBase, ICompoundedDayTypePay
{
    private readonly CompoundedPricers _pricers;

    protected CompoundedDayTypePayBase(BasicPipelineBase dayPipeline, DayTypePayInputs inputs, CompoundedPricers pricers)
        : base(dayPipeline, inputs)
    {
        _pricers = pricers;
    }

    protected sealed override OtNdCalculationMethod Method => OtNdCalculationMethod.Compounded;

    protected sealed override CategoryPay PriceHours(CategoryHours saved, CategoryRates rates, DayPay day, PayrollContext context) =>
        new(Kind, day, PriceOt(saved.Ot, rates), PriceNd(saved.Nd, rates), PriceNdot(saved.Ndot, rates));

    protected virtual BucketPay PriceOt(decimal hours, CategoryRates rates) => _pricers.Ot.Price(hours, rates);

    protected virtual BucketPay PriceNd(decimal hours, CategoryRates rates) => _pricers.Nd.Price(hours, rates);

    protected virtual BucketPay PriceNdot(decimal hours, CategoryRates rates) => _pricers.Ndot.Price(hours, rates);
}
