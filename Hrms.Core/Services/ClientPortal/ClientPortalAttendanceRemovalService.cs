using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hrms.Core.Services.ClientPortal;

/// <summary>
/// Tells the Client Portal that posted DTR rows are gone (batch deleted) or no longer posted (unposted, or reopened by a leave change), so it
/// drops its copies. The ids must be captured before the rows are deleted, and the message goes through the transactional outbox, so it leaves
/// the server only when that delete or unpost commits. Staging never commits itself and never fails the change it rides on.
/// </summary>
public class ClientPortalAttendanceRemovalService
{
    public const string BatchDeleted = "BatchDeleted";
    public const string Unposted = "Unposted";
    public const string LeaveChanged = "LeaveChanged";

    private readonly ClientPortalSettingsService _settings;
    private readonly ITenantProvider _tenant;
    private readonly IServiceProvider _services;
    private readonly ILogger<ClientPortalAttendanceRemovalService> _logger;

    public ClientPortalAttendanceRemovalService(
        ClientPortalSettingsService settings,
        ITenantProvider tenant,
        IServiceProvider services,
        ILogger<ClientPortalAttendanceRemovalService> logger)
    {
        _settings = settings;
        _tenant = tenant;
        _services = services;
        _logger = logger;
    }

    /// <summary>
    /// Queues the removal of these DailyRecord ids WITHOUT committing, one message per <see cref="ClientPortalAttendanceRemoved.MaxRecords"/>
    /// ids. Returns how many messages were queued; 0 when there is nothing to remove, the connection is off, no bus is configured, or queuing failed.
    /// </summary>
    public async Task<int> StageAsync(IEnumerable<Guid> dailyRecordIds, string batchCode, string reason, CancellationToken token)
    {
        var ids = dailyRecordIds.Distinct().Select(id => id.ToString("D")).ToList();
        if (ids.Count == 0)
            return 0;

        try
        {
            var publisher = _services.GetService<IPublishEndpoint>();
            if (publisher is null || !await _settings.IsEnabledAsync(token))
                return 0;

            var chunks = ids.Chunk(ClientPortalAttendanceRemoved.MaxRecords).ToList();
            for (var i = 0; i < chunks.Count; i++)
            {
                await publisher.Publish(new ClientPortalAttendanceRemoved
                {
                    TenantId = _tenant.TenantId,
                    BatchCode = batchCode,
                    Reason = reason,
                    Part = i + 1,
                    Parts = chunks.Count,
                    RecordExternalIds = chunks[i].ToList(),
                    OccurredAtUtc = DateTime.UtcNow,
                }, token);
            }
            return chunks.Count;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The delete/unpost goes ahead whatever happens to the portal; the rows stay there until removed by hand or sent again.
            _logger.LogWarning(ex, "Could not queue the Client Portal removal of {Count} DTR row(s) from {Batch}.", ids.Count, batchCode);
            return 0;
        }
    }
}
