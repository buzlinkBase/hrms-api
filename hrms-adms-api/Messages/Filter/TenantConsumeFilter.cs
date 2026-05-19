

namespace Hrms.adms.Messages.Filter;

public class TenantConsumeFilter<T> : IFilter<ConsumeContext<T>>
    where T : class
{
    private readonly ITenantProvider _tenantProvider;
    public TenantConsumeFilter(ITenantProvider tenantProvider)
    {
        _tenantProvider = tenantProvider;
    }

    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        if (!context.Headers.TryGetHeader("X-Tenant-ID", out var value) || !Guid.TryParse(value?.ToString(), out var tid))
        {
            throw new InvalidOperationException("Tenant ID header is missing or invalid.");
        }
        _tenantProvider.SetTenantId(tid);
        await next.Send(context);
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("hrms-tenant-filter");
}