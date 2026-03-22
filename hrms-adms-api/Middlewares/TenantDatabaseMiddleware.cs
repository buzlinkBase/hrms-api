using Onepunch.Common.Lib.Interfaces;

namespace Hrms.adms.Middleware; 
public class TenantDatabaseMiddleware
{
    private readonly RequestDelegate _next;
    public TenantDatabaseMiddleware(RequestDelegate next) => _next = next;
    public async Task InvokeAsync(
        HttpContext context,
        IConfiguration configuration,
        ITenantProvider tenantProvider,
        IConnectionClient connectionClient,
        TenantConnectionInfo connectionInfo)
    {

        var path = context.Request.Path;
        if (path.StartsWithSegments("/swagger") ||
            path.StartsWithSegments("/adms") ||
            path.StartsWithSegments("/favicon.ico") ||
            path.StartsWithSegments("/index.html") ||
            path.StartsWithSegments("/iclock"))
        {
            return;
        }
        var tid = tenantProvider.TenantId;
        connectionInfo.TenantId = tid;
        if (tid != Guid.Empty)
        {
            var response = await connectionClient.FindConnectionAsync(tid,"adms");
            if (response == null || response.Data == null) throw new Exception("Invalid Tenant Header");
            if (!response.Data.Success)
            {
                throw new Exception("Database connection error");
            }
            connectionInfo.ConnectionString = response.Data.ConnectionString;
        }
        await _next(context);
    }
}