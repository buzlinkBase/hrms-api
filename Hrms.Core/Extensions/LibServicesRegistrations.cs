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
