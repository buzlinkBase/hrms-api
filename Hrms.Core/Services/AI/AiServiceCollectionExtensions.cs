using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;


namespace Hrms.Core.Services.AI;

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
