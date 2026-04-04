
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MigrationHrns;
using TenantStoreApi.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables(); // GitHub Secrets override appsettings
builder.rmqConfig();
builder.Services.AddDbContext<TenantContext>(options =>
{
    var conn = builder.Configuration.GetConnectionString("CatalogDb");
    options.UseMySql(conn, ServerVersion.AutoDetect(conn));
});
IHost host = builder.Build();
await new MigrationRunner().Runner(host);


