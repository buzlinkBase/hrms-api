using Hrms.Core.Messaging;
using MassTransit;

namespace Hrms.Core.Services.ClientPortal;

/// <summary>
/// Asks for a posted DTR batch to be pushed to the Client Portal. The request goes through the transactional outbox, so it
/// leaves the server only when the unit of work it was staged in commits.
/// </summary>
public class ClientPortalSyncRequestService
{
    private readonly ClientPortalSettingsService _settings;
    private readonly DTRBatchService _batches;
    private readonly IPublishEndpoint _publisher;

    public ClientPortalSyncRequestService(ClientPortalSettingsService settings, DTRBatchService batches, IPublishEndpoint publisher)
    {
        _settings = settings;
        _batches = batches;
        _publisher = publisher;
    }

    /// <summary>
    /// Queues the push WITHOUT committing, for callers that commit once themselves (approving a batch). The worker re-checks that the
    /// batch is posted, so staging it for an approval that is only an intermediate step is harmless.
    /// </summary>
    public async Task<bool> StageAsync(Guid dtrBatchId, CancellationToken token)
    {
        if (await _settings.GetCredentialsAsync(token) is null)
            return false;
        var batch = await _batches.FineOneAsync(dtrBatchId, token);
        if (batch is null)
            return false;

        await _publisher.Publish(new ClientPortalSyncRequested { DtrBatchId = batch.Id, BatchCode = batch.BatchCode }, token);
        return true;
    }

    /// <summary>Queues the push and commits: for the "send again" button, which has no other work in its request.</summary>
    public async Task<bool> RequestAsync(Guid dtrBatchId, CancellationToken token)
    {
        var batch = await _batches.FineOneAsync(dtrBatchId, token) ?? throw new NotFoundException("DTR batch not found.");
        if (!batch.IsPosted)
            throw new ValidationException("Only a posted DTR batch can be sent to the client portal.");
        if (!await StageAsync(dtrBatchId, token))
            throw new ValidationException("The client portal connection is switched off or incomplete.");

        await _settings.CommitChangesAsync(token);
        return true;
    }
}
