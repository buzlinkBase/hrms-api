using MassTransit;

namespace Hrms.Core.Messaging.Filter;

public class TenantPublishFilter<T> : IFilter<PublishContext<T>> where T : class
{
    private readonly ITenantProvider _tenantProvider;
    public TenantPublishFilter(ITenantProvider tenantProvider) => _tenantProvider = tenantProvider;
    public void Probe(ProbeContext context) => context.CreateFilterScope("hrms-publish-filter");
    public async Task Send(PublishContext<T> context, IPipe<PublishContext<T>> next)
    {
        context.Headers.Set("X-Tenant-ID", _tenantProvider.TenantId.ToString());
        await next.Send(context);
    }
} 

