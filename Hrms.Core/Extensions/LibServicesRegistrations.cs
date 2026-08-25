using Hrms.Core.Calculators;
using Hrms.Core.Services.AI;
using Hrms.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Hrms.Core.Extensions;

public static class LibServicesRegistrations
{
    public static void RegisterHRCoreServices(this IServiceCollection services)
    {
        AddLibraryAssemblyDependencies(services, "Hrms.Core");
        AddLibraryAssemblyDependencies(services, "DTR.Core");
        services.AddScoped<IUnitOfWorkService, UnitOfWorkService>();
        services.AddScoped<IMigrationService, EvolveMigrationService>();
        services.AddScoped<Messaging.InstanceProvisioner>();
        services.AddScoped<Messaging.DedicatedProvisioner>();
        // Payroll-run calculators are stateless (context flows through Calculate(), never
        // stored), so they're singletons instead of being `new`'d per employee per run.
        services.AddSingleton<ICalculator<BasicRateModel, PayrollContext>, BasicPayrollCalculator>();
        services.AddSingleton<ICalculator<AllowancePipeData, PayrollContext>, AllowancesCalculator>();
        services.AddSingleton<ICalculator<DeductionPipeData, DeductionPayloadContext>, DeductionCalculator>();
        services.AddSingleton<IDailyRateResolver, DailyRateResolver>();
        services.AddSingleton<IPipeLine<PayrollContext, AllowancePipeData>, AllowancePipeline>();
        services.AddSingleton<IPipeLine<DeductionPayloadContext, DeductionPipeData>, DeductionPipeline>();

        // The 36 single-policy pipelines BasicPayrollCalculator runs per employee, per day.
        // Registered by concrete type (not through the shared IPipeLine<> interface) since
        // BasicPayrollCalculator needs each one by its specific role, not interchangeably.
        services.AddSingleton<RegularPipeline>();
        services.AddSingleton<RestDayPipeLine>();
        services.AddSingleton<RegularHolidayPipeLine>();
        services.AddSingleton<SpecialWorkDayPipeLine>();
        services.AddSingleton<RestLegalDayPipeLine>();
        services.AddSingleton<RestSpecialDayPipeLine>();
        services.AddSingleton<DoubleLegalPipeLine>();
        services.AddSingleton<RestDoubleLegalPipeLine>();

        services.AddSingleton<RegularOTPipeLine>();
        services.AddSingleton<RestDayOTPipeLine>();
        services.AddSingleton<LegalHolOTPipeLine>();
        services.AddSingleton<SpecialNonWorkingOTPipeLine>();
        services.AddSingleton<RestLegalDayOTPipeLine>();
        services.AddSingleton<RestSpecialDayOTPipeLine>();
        services.AddSingleton<DoubleLegalOTPipeLine>();
        services.AddSingleton<RestDoubleLegalOTPipeLine>();

        services.AddSingleton<RegularNDPipeLine>();
        services.AddSingleton<RestDayNDPipeLine>();
        services.AddSingleton<LegalHolNDPipeLine>();
        services.AddSingleton<SpecialNonWorkingNDPipeLine>();
        services.AddSingleton<RestLegalDayNDPipeLine>();
        services.AddSingleton<RestSpecialDayNDPipeLine>();
        services.AddSingleton<DoubleLegalNDPipeLine>();
        services.AddSingleton<RestDoubleLegalNDPipeLine>();

        services.AddSingleton<RegularNDOTPipeLine>();
        services.AddSingleton<RestDayNDOTPipeLine>();
        services.AddSingleton<LegalHolNDOTPipeLine>();
        services.AddSingleton<RestLegalDayNDOTPipeLine>();
        services.AddSingleton<SpecialNonWorkingNDOTPipeLine>();
        services.AddSingleton<RestSpecialDayNDOTPipeLine>();
        services.AddSingleton<DoubleLegalNDOTPipeLine>();
        services.AddSingleton<RestDoubleLegalNDOTPipeLine>();

        services.AddSingleton<AbsentPipeline>();
        services.AddSingleton<LatesPipeLine>();
        services.AddSingleton<UnderTimePipeLine>();
        services.AddSingleton<LeavePipeline>();
    }

    public static void AddLibraryAssemblyDependencies(IServiceCollection services, string assemblyName)
    {
        var libraryAssembly = Assembly.Load(assemblyName);
        var typesToRegister = libraryAssembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && type.Name.EndsWith("Service"));

        foreach (var type in typesToRegister)
        {
            var attr = type.GetCustomAttribute<ServiceRegistrationAttribute>();
            if (attr != null && attr.Exclude) continue;
            var lifetime = attr?.Lifetime ?? ServiceLifetime.Scoped;
            switch (lifetime)
            {
                case ServiceLifetime.Singleton:
                    services.AddSingleton(type);
                    break;
                case ServiceLifetime.Transient:
                    services.AddTransient(type);
                    break;
                default:
                    services.AddScoped(type);
                    break;
            }
        }
    }
}


public static class AiServiceCollectionExtensions
{
    /// <summary>
    /// Registers the generic AI extraction utility. Consumers inject
    /// IAiExtractionService; the Gemini implementation and schema generator
    /// are singletons (the SDK client is thread-safe and schemas are cached).
    /// </summary>
    public static IServiceCollection AddGeminiAiExtraction(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GeminiOptions>(configuration.GetSection(GeminiOptions.SectionName));
        services.AddSingleton<IAiSchemaGenerator, ReflectionAiSchemaGenerator>();
        services.AddSingleton<IAiExtractionService, GeminiAiExtractionService>();
        services.AddSingleton<IAiNotificationService, SignalRAiNotificationService>();
        return services;
    }
}
