
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
    // Explicit, deterministic, and fast
    var sqlVersion = new MySqlServerVersion(new Version(8, 4, 8)); // or Version(9, 2, 0)
    options.UseMySql(conn, sqlVersion);
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
