
namespace Hrms.adms.Middlewares;

public class TenantDatabaseMiddleware
{
    private readonly RequestDelegate _next;
    public TenantDatabaseMiddleware(RequestDelegate next) => _next = next;
    public async Task InvokeAsync(HttpContext context, ITenantProvider tenantProvider)
    {
        await _next(context);
    }
}