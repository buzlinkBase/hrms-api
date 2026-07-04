
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MigrationHrns;
using Serilog;
using TenantStoreApi.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables(); // GitHub Secrets override appsettings

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

// 3. Tell the host builder to use Serilog
builder.Services.AddSerilog();


builder.HrmsConfigRabbitMq();

builder.Services.AddDbContext<TenantContext>(options =>
{
    var conn = builder.Configuration.GetConnectionString("TenantStoreConnection");
    options.UseMySql(conn, ServerVersion.AutoDetect(conn));
});

try
{
    Log.Information("Starting migration runner host...");
    IHost host = builder.Build();
    await new MigrationRunner().Runner(host);
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    // 4. Ensure logs are flushed before the console app closes
    await Log.CloseAndFlushAsync();
}
//IHost host = builder.Build();
//await new MigrationRunner().Runner(host);
