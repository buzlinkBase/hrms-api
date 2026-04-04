namespace Hrms.adms.Extensions;

public class TenantConsumeFilter<T> : IFilter<ConsumeContext<T>> where T : class
{
    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        var scope = context.GetPayload<IServiceProvider>();
        var tenantProvider = scope.GetRequiredService<ITenantProvider>();
        if (context.Headers.TryGetHeader("X-Tenant-ID", out var value) && Guid.TryParse(value?.ToString(), out var tid))
        {
            tenantProvider.SetTenantId(tid);
        }
        else
        {
            throw new InvalidOperationException("Tenant ID header is missing.");
        }
        await next.Send(context);
    }
    public void Probe(ProbeContext context) => context.CreateFilterScope("hrms-tenant-filter");
}