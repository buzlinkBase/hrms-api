using Hrms.Core.Calculators.DtrPay;
using Microsoft.Extensions.DependencyInjection;

namespace hrms.test.PayrollRunTests.DtrPay;

// Test entry points into the DTR pay calculators: price one bucket of one category the way a
// calculator would (eligibility -> rates -> pricer), or resolve a whole calculator from the same
// DI wiring production uses.
internal static class DtrPayKit
{
    private static readonly EligibleHoursFilter Eligibility = new();
    private static readonly CategoryRateResolver Rates = new();

    private static readonly Lazy<IDtrPayCalculatorFactory> Factory = new(() =>
        new ServiceCollection().AddDtrPayCalculators().BuildServiceProvider().GetRequiredService<IDtrPayCalculatorFactory>());

    public static IDtrPayCalculator Calculator(OtNdCalculationMethod method) => Factory.Value.For(method);

    public static BucketPay CompoundedOt(PayCategory category, PayrollContext context) =>
        Price(new CompoundedOtPricer(), category, context, h => h.Ot);

    public static BucketPay CompoundedNd(PayCategory category, PayrollContext context) =>
        Price(new CompoundedNdPricer(), category, context, h => h.Nd);

    public static BucketPay CompoundedNdot(PayCategory category, PayrollContext context) =>
        Price(new CompoundedNdotPolicy(), category, context, h => h.Ndot);

    private static BucketPay Price(IBucketPricer pricer, PayCategory category, PayrollContext context, Func<CategoryHours, decimal> pick)
    {
        var hours = Eligibility.Filter(category.HoursOf(context.DailyRecord), context);
        return hours.HasPremiumHours ? pricer.Price(pick(hours), Rates.Resolve(category, context)) : BucketPay.Zero;
    }
}
