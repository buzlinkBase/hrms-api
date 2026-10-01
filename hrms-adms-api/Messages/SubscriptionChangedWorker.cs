using Onepunch.Common.Lib.Subscriptions;

namespace Hrms.adms.Messages;

/// <summary>
/// An account's subscription changed (Tenant Service's SubscriptionChanged): drop the cached
/// entitlements of each of its companies, so the next device registration is checked against
/// the new plan. Idempotent.
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
        EndpointName = "adms-subscription-changed-que";
    }
}
