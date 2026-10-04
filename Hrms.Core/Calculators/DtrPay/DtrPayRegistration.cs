using Microsoft.Extensions.DependencyInjection;

namespace Hrms.Core.Calculators.DtrPay;

// Composition root for the DTR pay calculators: wires each pay method's bucket pricers explicitly,
// so the calculators themselves depend only on abstractions. Everything here is stateless, so
// singletons are safe.
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

        services.AddSingleton<IDayTypePayPricer, DayTypePayPricer>();
        services.AddSingleton<IAttendancePayCalculator, AttendancePayCalculator>();
        services.AddSingleton<ICategoryRateResolver, CategoryRateResolver>();
        services.AddSingleton<IEligibleHoursFilter, EligibleHoursFilter>();
        services.AddSingleton<IDtrPayModelBuilder, DtrPayModelBuilder>();
        services.AddSingleton<INdBaseRoutingRule, FixedSalaryRegularNdBaseRule>();

        services.AddSingleton<IDtrPayCalculator>(sp => new CompoundedDtrPayCalculator(
            sp.GetRequiredService<IDayTypePayPricer>(),
            sp.GetRequiredService<IAttendancePayCalculator>(),
            sp.GetRequiredService<ICategoryRateResolver>(),
            sp.GetRequiredService<IEligibleHoursFilter>(),
            sp.GetRequiredService<IDtrPayModelBuilder>(),
            ot: new CompoundedOtPricer(),
            nd: new CompoundedNdPricer(),
            ndot: new CompoundedNdotPricer()));

        services.AddSingleton<IDtrPayCalculator>(sp => new AdditiveDtrPayCalculator(
            sp.GetRequiredService<IDayTypePayPricer>(),
            sp.GetRequiredService<IAttendancePayCalculator>(),
            sp.GetRequiredService<ICategoryRateResolver>(),
            sp.GetRequiredService<IEligibleHoursFilter>(),
            sp.GetRequiredService<IDtrPayModelBuilder>(),
            ndBase: new AdditiveNdBasePricer(),
            ot: new AdditiveOtPricer(),
            ndPremium: new AdditiveNdPremiumPricer(),
            ndBaseRouting: sp.GetRequiredService<INdBaseRoutingRule>()));

        services.AddSingleton<IDtrPayCalculatorFactory, DtrPayCalculatorFactory>();
        return services;
    }
}
