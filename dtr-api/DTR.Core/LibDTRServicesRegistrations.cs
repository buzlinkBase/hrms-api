using Microsoft.Extensions.DependencyInjection;

namespace DTR.Core;

public static class LibDTRServicesRegistrations
{
    public static void RegisterDTRCoreServices(this IServiceCollection services)
    {
        services.AddScoped<WorkScheduleResolver>();
        services.AddScoped<RestDayResolver>();
        services.AddScoped<HolidayResolver>();
        services.AddScoped<CurrentRangeDTRPayloadService>();

    }
}