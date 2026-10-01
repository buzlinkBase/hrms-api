using MassTransit;
using Onepunch.Common.Lib.Subscriptions;

namespace Hrms.Core.Messaging.Subscriptions;

/// <summary>
/// An account's subscription changed (Tenant Service's SubscriptionChanged): drop the cached
/// entitlements of each of its companies, so the next request enforces the new plan instead of
/// waiting out the cache. Idempotent.
/// </summary>
public class SubscriptionChangedWorker(IEntitlementProvider entitlements) : IConsumer<SubscriptionChanged>
{
    public Task Consume(ConsumeContext<SubscriptionChanged> context) =>
        entitlements.InvalidateAsync(context.Message.TenantIds);
}

public class SubscriptionChangedWorkerDefinition : ConsumerDefinition<SubscriptionChangedWorker>
{
    public SubscriptionChangedWorkerDefinition()
    {
        EndpointName = "hrms-subscription-changed-que";
    }
}
