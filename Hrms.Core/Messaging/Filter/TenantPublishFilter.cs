using MassTransit;

namespace Hrms.Core.Messaging.Filter;

public class TenantPublishFilter<T> : IFilter<PublishContext<T>> where T : class
{
    public const string HeaderName = "X-Tenant-ID";

    private readonly ITenantProvider _tenantProvider;
    public TenantPublishFilter(ITenantProvider tenantProvider) => _tenantProvider = tenantProvider;
    public void Probe(ProbeContext context) => context.CreateFilterScope("hrms-publish-filter");

    // Never replaces a company the message already carries. A publish made outside a request
    // (background scheduler, outbox delivery) resolves an empty tenant here -- overwriting the
    // header stamped at the original publish with Guid.Empty would leave the consumer without a
    // company (e.g. ApprovalPushNotificationWorker can then no longer address the recipient's
    // company + email group, so email-matched employees get no live update). With no company on
    // either side the header is still stamped (empty), since TenantConsumeFilter rejects a
    // message without one.
    public async Task Send(PublishContext<T> context, IPipe<PublishContext<T>> next)
    {
        if (!HasTenant(context))
            context.Headers.Set(HeaderName, _tenantProvider.TenantId.ToString());
        await next.Send(context);
    }

    private static bool HasTenant(PublishContext<T> context) =>
        context.Headers.TryGetHeader(HeaderName, out var value)
        && Guid.TryParse(value?.ToString(), out var existing)
        && existing != Guid.Empty;
}
