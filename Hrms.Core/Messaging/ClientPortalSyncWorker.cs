using Hrms.Core.Services.ClientPortal;
using MassTransit;

namespace Hrms.Core.Messaging;

/// <summary>
/// Pushes a posted DTR batch to the Client Portal (see <see cref="ClientPortalSyncService"/>). Runs outside the approval request, so a
/// portal outage never blocks approving a DTR: a failed push is retried by the bus and shows on the connection screen.
/// </summary>
public class ClientPortalSyncWorker : IConsumer<ClientPortalSyncRequested>
{
    private readonly ClientPortalSyncService _sync;
    private readonly ITenantProvider _tenantProvider;

    public ClientPortalSyncWorker(ClientPortalSyncService sync, ITenantProvider tenantProvider)
    {
        _sync = sync;
        _tenantProvider = tenantProvider;
    }

    public async Task Consume(ConsumeContext<ClientPortalSyncRequested> context)
    {
        var summary = await _sync.SyncBatchAsync(context.Message.DtrBatchId, context.CancellationToken);
        if (summary is null)
            return;
        Log.Information("Client portal sync [{Tenant}] batch {Batch}: {Accepted}/{Records} accepted, {Rejected} refused",
            _tenantProvider.TenantId, summary.BatchCode, summary.Accepted, summary.Records, summary.Rejected);
    }
}

public class ClientPortalSyncWorkerDefinition : ConsumerDefinition<ClientPortalSyncWorker>
{
    public ClientPortalSyncWorkerDefinition()
    {
        EndpointName = "hrms-client-portal-sync-que";
        // One batch at a time per instance, so two approvals close together never push the same master rows concurrently.
        ConcurrentMessageLimit = 1;
    }
}
