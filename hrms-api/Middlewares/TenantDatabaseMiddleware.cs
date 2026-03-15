using Onepunch.Common.Lib.Interfaces;
namespace Hrms.Api.Middlewares;
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
        var tid = tenantProvider.TenantId;
        connectionInfo.TenantId = tid;  
        if (tid != Guid.Empty)
        {
            var response = await connectionClient.FindConnectionAsync(tid);
            connectionInfo.ConnectionString = response.Data 
                ?? configuration.GetConnectionString("DefaultConnection"); 
        } 
        await _next(context);
    }
}