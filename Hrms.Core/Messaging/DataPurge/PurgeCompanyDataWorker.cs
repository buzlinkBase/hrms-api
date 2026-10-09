using Hrms.Core.Messaging.Filter;
using Hrms.Core.Services.DataPurge;
using MassTransit;

namespace Hrms.Core.Messaging.DataPurge;

/// <summary>
/// Tenant Service asks for one locked company's data to be archived and removed (or, years later,
/// for its archive to be destroyed). The company comes from the X-Tenant-ID header, as for every
/// consumer. Always answers with <see cref="CompanyDataPurged"/> -- including a failure, which Tenant
/// Service records and asks again the next day, rather than this message being retried in a loop.
/// </summary>
public class PurgeCompanyDataWorker(CompanyDataPurgeService purge, ITenantProvider tenantProvider) : IConsumer<PurgeCompanyData>
{
    public async Task Consume(ConsumeContext<PurgeCompanyData> context)
    {
        // The header names the company the request is about; a request that names another is refused.
        if (context.Message.TenantId != tenantProvider.TenantId)
        {
            await context.Publish(new CompanyDataPurged
            {
                OwnerUserId = context.Message.OwnerUserId,
                SubscriptionId = context.Message.SubscriptionId,
                TenantId = context.Message.TenantId,
                Stage = context.Message.Stage,
                Succeeded = false,
                Error = "The request's company doesn't match its tenant header.",
            }, context.CancellationToken);
            return;
        }

        var result = await purge.RunAsync(context.Message, context.CancellationToken);
        await context.Publish(result, ctx => ctx.Headers.Set(TenantPublishFilter<CompanyDataPurged>.HeaderName, result.TenantId.ToString()), context.CancellationToken);
    }
}

public class PurgeCompanyDataWorkerDefinition : ConsumerDefinition<PurgeCompanyDataWorker>
{
    public PurgeCompanyDataWorkerDefinition()
    {
        EndpointName = "hrms-purge-company-data-que";
        // One company at a time: a purge reads and deletes a lot.
        ConcurrentMessageLimit = 1;
    }
}
