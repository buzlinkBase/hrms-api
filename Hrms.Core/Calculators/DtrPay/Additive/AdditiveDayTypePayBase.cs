namespace Hrms.Core.Calculators.DtrPay;

// The Additive method's bucket pricers, registered once and shared by its 8 day types.
internal sealed record AdditivePricers(IBucketPricer NdBase, IBucketPricer Ot, IBucketPricer NdPremium);

// Additive, for one day type: the DTR display hours, each bucket at ONE rate, added up --
//   Hrs (Hrs + ND)  x day rate       -> day pay (the ND part at the full day rate)
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

    protected sealed override CategoryPay PriceHours(CategoryHours saved, CategoryRates rates, DayPay day, PayrollContext context)
    {
        var shown = DisplayHoursShaper.Shape(saved);
        // The night hours the display folded into "Hrs" -- the day pipeline already paid the rest.
        var nightHoursInHrs = shown.Hrs - saved.Hrs;

        var ot = PriceOt(shown.Ot, rates);
        var nd = PriceNdPremium(shown.Nd, rates);
        (day, nd) = FoldNightHoursIntoDayPay(_pricers.NdBase.Price(nightHoursInHrs, rates).Value, day, nd, context);

        return new CategoryPay(Kind, day, ot, nd, BucketPay.Zero);
    }

    // OT + NDOT hours at this day type's raw OT rate.
    protected virtual BucketPay PriceOt(decimal hours, CategoryRates rates) => _pricers.Ot.Price(hours, rates);

    // ND + NDOT hours at the night premium only.
    protected virtual BucketPay PriceNdPremium(decimal hours, CategoryRates rates) => _pricers.NdPremium.Price(hours, rates);

    // Where the night hours' day-rate base is paid: in the day pay, like any other "Hrs".
    protected virtual (DayPay Day, BucketPay Nd) FoldNightHoursIntoDayPay(decimal nightBase, DayPay day, BucketPay nd, PayrollContext context) =>
        (day.AddWorked(nightBase), nd);
}
