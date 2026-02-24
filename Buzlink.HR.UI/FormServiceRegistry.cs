using Audit;
using Buzlink.HR.UI.Providers;
using Buzlink.HR.UI.UI.Config;
using BuzlinkRepository;
using Encryption;
using Hrms.Infrastructure;
using Hrms.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Shared.Contracts;
namespace Buzlink.HR.UI;

public static class FormServiceRegistry
{
    public static void RegisterForms(this IServiceCollection services)
    {
        services.AddTransient<IScopeFactory, ScopeFactory>();
        services.AddTransient<MainMenu>();
        services.AddTransient<DepartmentUI>();
        services.AddTransient<CreateHDMF>();
        services.AddTransient<OtherIncomeCategory>();
        services.AddTransient<DeductionCategory>();
        services.AddTransient<DeductionSetupUI>();
        services.AddTransient<OtherIncomeSetupUI>();
        services.AddTransient<PositionUI>();
        services.AddTransient<EmployeeUI>();
        services.AddTransient<SectionUI>();
        services.AddTransient<EmployeeMasterList>();
        services.AddTransient<CreateHoliday>();
        services.AddTransient<HolidayMasterRecord>();
        services.AddTransient<CreateLeave>();
        services.AddTransient<CreateLeaveMaster>();

        //services.AddTransient<PHICTableSetupUI>();
        //services.AddTransient<SSSTableSetupUI>();
        //services.AddTransient<LeaveMasterRecord>();
        //services.AddTransient<CreateOT>();
        //services.AddTransient<OTMasterRecord>();
        //services.AddTransient<TravelOrdersEntryUI>();
        //services.AddTransient<TravelOrdersList>();
        //services.AddTransient<CreateDeductionEntry>();
        //services.AddTransient<DeductionMasterRecord>();
        //services.AddTransient<AllowanceEntryUI>();

    }
    public static void RegisterProviders(this IServiceCollection services)
    {
        services.AddSingleton<ITenantProvider, TenantProvider>();
        services.AddSingleton<IKeyProvider, KeyProvider>();
        services.AddSingleton<IHRConnectionResolver, HRConnectionResolver>();
        services.AddScoped<IConnectionStringWriterProvider, ConnectionStringWriterProvider>();
        services.AddScoped<IConnectionStringReaderProvider, ConnectionStringReaderProvider>();
        services.AddScoped<ITenantContextAccessor, DesktopTenantContextAccessor>();
        services.AddScoped<IAppConfigurationProvider, WinAppConfigurationProvider>();
        services.AddScoped<IDbConnectionProvider, WinConnectionMetadataProvider>();
        services.AddScoped<IAuditTrail, AuditTrail>();
        services.AddScoped<IAuditTrailService, EfAuditTrailService<HrmsContext>>();
    }
}

public static class LoggerConfig
{
    public static void AddLogConfig(this IServiceCollection services)
    {
        Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Debug)
        .MinimumLevel.Override("System", LogEventLevel.Debug)
        .WriteTo.Console()
        .WriteTo.Debug(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
        .WriteTo.File(
            path: "Logs/log-.txt",
            rollingInterval: RollingInterval.Day,
            outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
        )
        .WriteTo.Seq("http://localhost:5341")
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithProperty("Project", "hrms-ui-winf")
        .Enrich.WithProperty("System", "hrms")
        .CreateLogger();

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog();
        });
    }
}
