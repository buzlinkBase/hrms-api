using Hrms.Core.Messaging;
using MassTransit;

namespace Hrms.Core.Services.ClientPortal;

/// <summary>
/// Asks for posted DTR rows to be sent to the Client Portal. The request goes through the transactional outbox, so it leaves the server
/// only when the unit of work it was staged in commits; <see cref="ClientPortalSyncWorker"/> then builds and sends the rows.
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
    public async Task<bool> StageAsync(Guid dtrBatchId, CancellationToken token, string reason = ClientPortalSyncRequested.Approved)
    {
        if (!await _settings.IsEnabledAsync(token))
            return false;
        var batch = await _batches.FineOneAsync(dtrBatchId, token);
        if (batch is null)
            return false;

        await _publisher.Publish(new ClientPortalSyncRequested { DtrBatchId = batch.Id, BatchCode = batch.BatchCode, Reason = reason }, token);
        return true;
    }

    /// <summary>
    /// Queues just these rows WITHOUT committing (the posted days a changed punch belongs to). The worker skips any that are not posted.
    /// </summary>
    public async Task<bool> StageRecordsAsync(IReadOnlyCollection<Guid> dailyRecordIds, string reason, CancellationToken token)
    {
        if (dailyRecordIds.Count == 0 || !await _settings.IsEnabledAsync(token))
            return false;

        await _publisher.Publish(new ClientPortalSyncRequested { DailyRecordIds = dailyRecordIds.Distinct().ToList(), Reason = reason }, token);
        return true;
    }

    /// <summary>Queues the push and commits: for the "send again" button, which has no other work in its request.</summary>
    public async Task<bool> RequestAsync(Guid dtrBatchId, CancellationToken token)
    {
        var batch = await _batches.FineOneAsync(dtrBatchId, token) ?? throw new NotFoundException("DTR batch not found.");
        if (!batch.IsPosted)
            throw new ValidationException("Only a posted DTR batch can be sent to the client portal.");
        if (!await StageAsync(dtrBatchId, token, ClientPortalSyncRequested.Resend))
            throw new ValidationException("The client portal connection is switched off.");

        await _settings.CommitChangesAsync(token);
        return true;
    }
}
