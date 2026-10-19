using Hrms.Core.Services.ClientPortal;
using MassTransit;

namespace Hrms.Core.Messaging;

/// <summary>
/// Sends posted DTR rows to the Client Portal (see <see cref="ClientPortalSyncService"/>): a whole batch once it is approved, or just the
/// rows a changed punch belongs to. Runs outside the request that asked for it, so building a large batch never slows down approving a DTR.
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
        var m = context.Message;
        var summary = m.DailyRecordIds is { Count: > 0 } ids
            ? await _sync.SyncRecordsAsync(ids, m.Reason, context.CancellationToken)
            : await _sync.SyncBatchAsync(m.DtrBatchId, m.Reason, context.CancellationToken);
        if (summary is null)
            return;
        Log.Information("Client portal sync [{Tenant}] {Batch}: {Records} records sent in {Parts} message(s) ({Reason})",
            _tenantProvider.TenantId, summary.BatchCode, summary.Records, summary.Parts, m.Reason);
    }
}

public class ClientPortalSyncWorkerDefinition : ConsumerDefinition<ClientPortalSyncWorker>
{
    public ClientPortalSyncWorkerDefinition()
    {
        EndpointName = "hrms-client-portal-sync-que";
        // One request at a time per instance, so two sends close together leave in the order they were asked for.
        ConcurrentMessageLimit = 1;
    }
}
