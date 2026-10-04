namespace Hrms.Core.Calculators.DtrPay;

// One day's DTR pay for one employee, under one OT/ND calculation method (Setup > Company Policy
// > OT/ND Calculation Method). Pick one through IDtrPayCalculatorFactory.
public interface IDtrPayCalculator
{
    OtNdCalculationMethod Method { get; }
    DTRPayModel Calculate(PayrollContext context);
}

// Template Method: the flow every pay method follows, in one place --
//   for each day-type category: saved hours -> eligible hours -> shaped hours -> priced pay,
//   then the day's attendance amounts, then the DTRPayModel.
// Subclasses supply only what actually differs: how hours are shaped and how a category's
// OT / ND / NDOT hours are priced. Day-type pay, rates, eligibility, attendance and the model
// mapping are shared collaborators, so the two methods can't drift apart on any of those.
internal abstract class DtrPayCalculatorBase : IDtrPayCalculator
{
    private readonly IDayTypePayPricer _dayPay;
    private readonly IAttendancePayCalculator _attendance;
    private readonly ICategoryRateResolver _rates;
    private readonly IEligibleHoursFilter _eligibility;
    private readonly IDtrPayModelBuilder _builder;

    protected DtrPayCalculatorBase(
        IDayTypePayPricer dayPay,
        IAttendancePayCalculator attendance,
        ICategoryRateResolver rates,
        IEligibleHoursFilter eligibility,
        IDtrPayModelBuilder builder)
    {
        _dayPay = dayPay;
        _attendance = attendance;
        _rates = rates;
        _eligibility = eligibility;
        _builder = builder;
    }

    public abstract OtNdCalculationMethod Method { get; }

    // How this method wants the saved DTR hours laid out before pricing them.
    protected abstract IHoursShaper Shaper { get; }

    // Prices one category's OT / ND / NDOT hours. saved = eligible hours as stored, shown = the
    // same hours after Shaper; day = the category's plain worked/unworked pay.
    protected abstract CategoryPay PriceCategory(
        PayCategory category, CategoryHours saved, CategoryHours shown, CategoryRates rates, DayPay day, PayrollContext context);

    public DTRPayModel Calculate(PayrollContext context)
    {
        var pay = new Dictionary<PayCategoryKind, CategoryPay>(PayCategories.All.Count);
        foreach (var category in PayCategories.All)
        {
            var day = _dayPay.Price(category.Kind, context);
            var saved = _eligibility.Filter(category.HoursOf(context.DailyRecord), context);
            pay[category.Kind] = saved.HasPremiumHours
                ? PriceCategory(category, saved, Shaper.Shape(saved), _rates.Resolve(category, context), day, context)
                : CategoryPay.DayOnly(category.Kind, day);
        }
        return _builder.Build(context, pay, _attendance.Calculate(context));
    }
}

// Compounded (DOLE-standard): the saved DTR hours exactly as stored, each bucket at its fully
// compounded rate --
//   OT   x day rate x OT rate
//   ND   x day rate x ND rate
//   NDOT x day rate x OT rate x ND rate
internal sealed class CompoundedDtrPayCalculator : DtrPayCalculatorBase
{
    private readonly IBucketPricer _ot;
    private readonly IBucketPricer _nd;
    private readonly IBucketPricer _ndot;

    public CompoundedDtrPayCalculator(
        IDayTypePayPricer dayPay,
        IAttendancePayCalculator attendance,
        ICategoryRateResolver rates,
        IEligibleHoursFilter eligibility,
        IDtrPayModelBuilder builder,
        IBucketPricer ot,
        IBucketPricer nd,
        IBucketPricer ndot)
        : base(dayPay, attendance, rates, eligibility, builder)
    {
        _ot = ot;
        _nd = nd;
        _ndot = ndot;
    }

    public override OtNdCalculationMethod Method => OtNdCalculationMethod.Compounded;

    protected override IHoursShaper Shaper { get; } = new SavedHoursShaper();

    protected override CategoryPay PriceCategory(
        PayCategory category, CategoryHours saved, CategoryHours shown, CategoryRates rates, DayPay day, PayrollContext context) =>
        new(category.Kind, day, _ot.Price(shown.Ot, rates), _nd.Price(shown.Nd, rates), _ndot.Price(shown.Ndot, rates));
}

// Additive: the DTR display hours (DisplayHoursShaper), each bucket at ONE rate, added up --
//   Hrs (Hrs + ND)    x day rate      -> day pay (the ND part at the full day rate)
//   OT  (OT + NDOT)   x raw OT rate   -> OT pay, no day-type multiplier
//   ND  (ND + NDOT)   x (ND rate - 1) -> ND pay, the night premium only
//   NDOT              -> always 0 (its hours are already in OT and ND)
// Pays exactly what the old Additive branches did; only the bucket each peso lands in changed.
internal sealed class AdditiveDtrPayCalculator : DtrPayCalculatorBase
{
    private readonly IBucketPricer _ndBase;
    private readonly IBucketPricer _ot;
    private readonly IBucketPricer _ndPremium;
    private readonly INdBaseRoutingRule _ndBaseRouting;

    public AdditiveDtrPayCalculator(
        IDayTypePayPricer dayPay,
        IAttendancePayCalculator attendance,
        ICategoryRateResolver rates,
        IEligibleHoursFilter eligibility,
        IDtrPayModelBuilder builder,
        IBucketPricer ndBase,
        IBucketPricer ot,
        IBucketPricer ndPremium,
        INdBaseRoutingRule ndBaseRouting)
        : base(dayPay, attendance, rates, eligibility, builder)
    {
        _ndBase = ndBase;
        _ot = ot;
        _ndPremium = ndPremium;
        _ndBaseRouting = ndBaseRouting;
    }

    public override OtNdCalculationMethod Method => OtNdCalculationMethod.Additive;

    protected override IHoursShaper Shaper { get; } = new DisplayHoursShaper();

    protected override CategoryPay PriceCategory(
        PayCategory category, CategoryHours saved, CategoryHours shown, CategoryRates rates, DayPay day, PayrollContext context)
    {
        // The night hours the display folded into "Hrs" -- the day-type pipeline already paid the
        // rest of Hrs, so only these are priced here.
        var ndBase = _ndBase.Price(shown.Hrs - saved.Hrs, rates).Value;
        var ot = _ot.Price(shown.Ot, rates);
        var nd = _ndPremium.Price(shown.Nd, rates);

        if (_ndBaseRouting.KeepInNdBucket(category, context))
            nd = nd with { Value = ndBase + nd.Value }; // NDPremium unchanged, so BasePortion == ndBase
        else
            day = day.AddWorked(ndBase);

        return new CategoryPay(category.Kind, day, ot, nd, BucketPay.Zero);
    }
}

// Factory: the one place that maps a company's OT/ND method to its calculator. Registry-based --
// every IDtrPayCalculator registered in DI is picked up, so a new method is a new class plus one
// registration, with no change here or in any caller.
public interface IDtrPayCalculatorFactory
{
    IDtrPayCalculator For(OtNdCalculationMethod method);
}

internal sealed class DtrPayCalculatorFactory : IDtrPayCalculatorFactory
{
    private readonly IReadOnlyDictionary<OtNdCalculationMethod, IDtrPayCalculator> _byMethod;

    public DtrPayCalculatorFactory(IEnumerable<IDtrPayCalculator> calculators)
    {
        _byMethod = calculators.ToDictionary(c => c.Method);
    }

    public IDtrPayCalculator For(OtNdCalculationMethod method) =>
        _byMethod.TryGetValue(method, out var calculator)
            ? calculator
            : throw new InvalidOperationException($"No DTR pay calculator is registered for OT/ND calculation method '{method}'.");
}
