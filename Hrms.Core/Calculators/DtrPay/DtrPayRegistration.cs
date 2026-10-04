using Microsoft.Extensions.DependencyInjection;

namespace Hrms.Core.Calculators.DtrPay;

// Composition root for the DTR pay calculators: each pay method is its 8 day-type classes plus
// its bucket pricers; the calculators and day types depend only on abstractions. Everything here
// is stateless, so singletons are safe.
public static class DtrPayRegistration
{
    public static IServiceCollection AddDtrPayCalculators(this IServiceCollection services)
    {
        // The day-type and attendance pipelines both pay methods share.
        services.AddSingleton<RegularPipeline>();
        services.AddSingleton<RestDayPipeLine>();
        services.AddSingleton<RegularHolidayPipeLine>();
        services.AddSingleton<SpecialWorkDayPipeLine>();
        services.AddSingleton<RestLegalDayPipeLine>();
        services.AddSingleton<RestSpecialDayPipeLine>();
        services.AddSingleton<DoubleLegalPipeLine>();
        services.AddSingleton<RestDoubleLegalPipeLine>();
        services.AddSingleton<AbsentPipeline>();
        services.AddSingleton<LatesPipeLine>();
        services.AddSingleton<UnderTimePipeLine>();
        services.AddSingleton<LeavePipeline>();

        services.AddSingleton<IAttendancePayCalculator, AttendancePayCalculator>();
        services.AddSingleton<ICategoryRateResolver, CategoryRateResolver>();
        services.AddSingleton<IEligibleHoursFilter, EligibleHoursFilter>();
        services.AddSingleton<IDtrPayModelBuilder, DtrPayModelBuilder>();
        services.AddSingleton<DayTypePayInputs>();

        // Compounded: one class per day type, sharing the compounded bucket pricers.
        services.AddSingleton(new CompoundedPricers(
            Ot: new CompoundedOtPricer(),
            Nd: new CompoundedNdPricer(),
            Ndot: new CompoundedNdotPricer()));
        services.AddSingleton<ICompoundedDayTypePay, CompoundedRegularDayPay>();
        services.AddSingleton<ICompoundedDayTypePay, CompoundedRestDayPay>();
        services.AddSingleton<ICompoundedDayTypePay, CompoundedLegalHolidayPay>();
        services.AddSingleton<ICompoundedDayTypePay, CompoundedSpecialHolidayPay>();
        services.AddSingleton<ICompoundedDayTypePay, CompoundedRestLegalDayPay>();
        services.AddSingleton<ICompoundedDayTypePay, CompoundedRestSpecialDayPay>();
        services.AddSingleton<ICompoundedDayTypePay, CompoundedDoubleLegalPay>();
        services.AddSingleton<ICompoundedDayTypePay, CompoundedRestDoubleLegalPay>();
        services.AddSingleton<IDtrPayCalculator, CompoundedDtrPayCalculator>();

        // Additive: one class per day type, sharing the additive bucket pricers.
        services.AddSingleton(new AdditivePricers(
            NdBase: new AdditiveNdBasePricer(),
            Ot: new AdditiveOtPricer(),
            NdPremium: new AdditiveNdPremiumPricer()));
        services.AddSingleton<IAdditiveDayTypePay, AdditiveRegularDayPay>();
        services.AddSingleton<IAdditiveDayTypePay, AdditiveRestDayPay>();
        services.AddSingleton<IAdditiveDayTypePay, AdditiveLegalHolidayPay>();
        services.AddSingleton<IAdditiveDayTypePay, AdditiveSpecialHolidayPay>();
        services.AddSingleton<IAdditiveDayTypePay, AdditiveRestLegalDayPay>();
        services.AddSingleton<IAdditiveDayTypePay, AdditiveRestSpecialDayPay>();
        services.AddSingleton<IAdditiveDayTypePay, AdditiveDoubleLegalPay>();
        services.AddSingleton<IAdditiveDayTypePay, AdditiveRestDoubleLegalPay>();
        services.AddSingleton<IDtrPayCalculator, AdditiveDtrPayCalculator>();

        services.AddSingleton<IDtrPayCalculatorFactory, DtrPayCalculatorFactory>();
        return services;
    }
}
