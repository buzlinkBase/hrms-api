using Microsoft.Extensions.DependencyInjection;

namespace DTR.Core.Extensions;

public static class LibDTRServicesRegistrations
{
    public static void RegisterDTRCoreServices(this IServiceCollection services)
    {
        services.AddScoped<WorkScheduleResolver>();
        services.AddScoped<RestDayResolver>();
        services.AddScoped<HolidayResolver>();
        services.AddScoped<CurrentRangeDTRPayloadService>();

        services.AddKeyedSingleton<IDTRTimePipeline, NonHolidayDutyTimePipeline>(DTRPipelineKeys.Regular);
        services.AddKeyedSingleton<IDTRTimePipeline, TravelPipeline>(DTRPipelineKeys.Travel);
        services.AddKeyedSingleton<IDTRTimePipeline, LeaveTimePipeline>(DTRPipelineKeys.Leave);
        services.AddKeyedSingleton<IDTRTimePipeline, HolidayPlus8TimePipeline>(DTRPipelineKeys.Plus8);
        services.AddKeyedSingleton<IDTRTimePipeline, OverTimePipeline>(DTRPipelineKeys.OT);
        services.AddKeyedSingleton<IDTRTimePipeline, LateTimePipeline>(DTRPipelineKeys.Late);
        services.AddKeyedSingleton<IDTRTimePipeline, UndertimeTimePipeline>(DTRPipelineKeys.UT);
        services.AddKeyedSingleton<IDTRTimePipeline, OverbreaktimePipeline>(DTRPipelineKeys.Overbreak);
        services.AddSingleton<IHolidayDutyTimePipeline, HolidayDutyTimePipeline>();

 
        services.AddSingleton<IWorkTypeResolver, WorkTypeResolver>();
        services.AddSingleton<IDTRDetailColumnDisplayProcessor, DTRDetailColumnDisplayProcessor>();
        services.AddSingleton<IDailyRecordBuilder, DailyRecordBuilder>();
        services.AddSingleton<CleanDTRDetailProcessor>();
    }
}