namespace Hrms.Core.Calculators.DtrPay;

// One day type's whole pay for one day (its plain worked/unworked hours plus its OT, ND and NDOT
// hours), under one pay method. Each pay method has one class per day type -- the same split as
// Pipelines/DTRPayPipelines -- under Additive/ and Compounded/.
internal interface IDayTypePayCalculator
{
    PayCategoryKind Kind { get; }
    CategoryPay Calculate(PayrollContext context);
}

// Markers so each pay method's calculator receives exactly its own 8 day types from DI.
internal interface IAdditiveDayTypePay : IDayTypePayCalculator { }
internal interface ICompoundedDayTypePay : IDayTypePayCalculator { }

// What every day-type calculator needs besides its own day pipeline and pricers.
internal sealed record DayTypePayInputs(ICategoryRateResolver Rates, IEligibleHoursFilter Eligibility);

// Template Method for one day type:
//   plain day pay (this day type's existing pipeline: rates, Fixed pre-funding, unworked pay)
//   -> this day type's OT / ND / NDOT hours, minus anything the employee isn't eligible for
//   -> nothing more to price? done : resolve this day type's rates -> PriceHours (per method).
internal abstract class DayTypePayCalculatorBase : IDayTypePayCalculator
{
    private readonly BasicPipelineBase _dayPipeline;
    private readonly DayTypePayInputs _inputs;

    protected DayTypePayCalculatorBase(BasicPipelineBase dayPipeline, DayTypePayInputs inputs)
    {
        _dayPipeline = dayPipeline;
        _inputs = inputs;
    }

    // This day type's row in PayCategories: which DailyRecord fields hold its hours, which rates
    // price them. Shared by the Additive and Compounded class of the same day type.
    protected abstract PayCategory Category { get; }

    public PayCategoryKind Kind => Category.Kind;

    // Prices this day type's eligible OT / ND / NDOT hours the way the pay method does.
    protected abstract CategoryPay PriceHours(CategoryHours saved, CategoryRates rates, DayPay day, PayrollContext context);

    public CategoryPay Calculate(PayrollContext context)
    {
        var line = _dayPipeline.Run(context);
        var day = new DayPay(line.Value, line.Worked, line.UnWork);

        var saved = _inputs.Eligibility.Filter(Category.HoursOf(context.DailyRecord), context);
        if (!saved.HasPremiumHours) return CategoryPay.DayOnly(Kind, day);

        return PriceHours(saved, _inputs.Rates.Resolve(Category, context), day, context);
    }
}
