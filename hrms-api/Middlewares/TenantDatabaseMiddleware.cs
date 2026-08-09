using Onepunch.Common.Lib.Cache;
using Onepunch.Common.Lib.Interfaces;
using Serilog;

namespace Hrms.Api.Middlewares;

public class TenantDatabaseMiddleware
{
    private readonly RequestDelegate _next;
    public TenantDatabaseMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
      HttpContext context,
      ITenantProvider tenantProvider,
      ICacheService cacheService,
      IConnectionClient connectionClient,
      IConfiguration configuration,
      TenantConnectionStringInfo connectionInfo)
    {
        var tid = tenantProvider.TenantId;
        connectionInfo.TenantId = tid;

        var useDedicated = configuration.GetValue<bool?>("Hris:DedicatedDatabase") ?? false;
        if (!useDedicated || tid == Guid.Empty)
        {
            // Shared database: leave ConnectionString unset. HrmsContext.OnConfiguring
            // already falls back to the default "HrmsConnection" on its own, so there's
            // no need to resolve/duplicate that default here.
            await _next(context);
            return;
        }

        var key = $"connection:{tid}";
        var cached = await cacheService.GetAsync<string>(key);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            connectionInfo.ConnectionString = cached;
            await _next(context);
            return;
        }

        ConnectionQueryResponse? data;
        try
        {
            var response = await connectionClient.FindConnectionAsync(tid, "hrms");
            data = response?.Data;
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            Log.Error(ex, "Tenant connection service unreachable for tenant {TenantId}", tid);
            await context.Response.WriteAsync("Unable to reach the tenant connection service.");
            return;
        }

        if (data is not { Success: true } || string.IsNullOrWhiteSpace(data.ConnectionString))
        {
            // Dedicated mode expects every tenant hitting this instance to have a
            // registered connection; a missing one means the tenant isn't provisioned here.
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            Log.Error("No dedicated database registered for tenant {TenantId}", tid);
            await context.Response.WriteAsync("Unable to retrieve connection.");
            return;
        }
        connectionInfo.ConnectionString = data.ConnectionString;
        await cacheService.SetAsync(key, data.ConnectionString, TimeSpan.FromMinutes(30));
        await _next(context);
    }
}
