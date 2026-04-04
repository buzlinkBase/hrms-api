
using Onepunch.Common.Lib.Interfaces;
using Serilog;
namespace Hrms.adms.Middlewares;

public class TenantDatabaseMiddleware
{
    private readonly RequestDelegate _next;
    public TenantDatabaseMiddleware(RequestDelegate next) => _next = next;
    public async Task InvokeAsync(
      HttpContext context,
      ITenantProvider tenantProvider,
      ICacheService cacheService,
      IConnectionClient connectionClient,
      TenantConnectionInfo connectionInfo)
    {
        var tid = tenantProvider.TenantId;
        connectionInfo.TenantId = tid;

        if (tid != Guid.Empty)
        {
            var key = $"connection:{tid}"; // Use colon for better Redis grouping
            var cache = await cacheService.GetAsync<string>(key);

            if (!string.IsNullOrWhiteSpace(cache))
            {
                connectionInfo.ConnectionString = cache;
            }
            else
            {
                var response = await connectionClient.FindConnectionAsync(tid, "hrms");
                if (response?.Data == null || !response.Data.Success)
                {
                    // Better to return 401/404 than throwing a 500 exception
                    context.Response.StatusCode = 401;
                    Log.Error("Unable to grab connection string for tenant {0}", tid);
                    await context.Response.WriteAsync("Workspace connection service is down");
                    return;
                }
                connectionInfo.ConnectionString = response.Data.ConnectionString;
                //TODO cache connection string in Redis with an appropriate expiration time
                await cacheService.SetAsync(key, connectionInfo.ConnectionString, TimeSpan.FromHours(1));
            }
        }
        // Single exit point for the whole middleware
        await _next(context);
    }
}