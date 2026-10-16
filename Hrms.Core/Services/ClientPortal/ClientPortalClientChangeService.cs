using Hrms.Domain.Entities;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Onepunch.Common.Lib.DTO;

namespace Hrms.Core.Services.ClientPortal;

/// <summary>
/// Tells the Client Portal that a client changed in HRIS, so the portal's copy (same id) follows and a client that is made inactive or deleted
/// has its users switched off there. The message goes through the transactional outbox, so it leaves the server only when the unit of work
/// it was staged in commits; staging never commits itself and never fails the save it rides on.
/// </summary>
public class ClientPortalClientChangeService
{
    public const string Created = "Created";
    public const string Updated = "Updated";
    public const string Deleted = "Deleted";

    private readonly ClientPortalSettingsService _settings;
    private readonly ITenantProvider _tenant;
    private readonly IServiceProvider _services;
    private readonly ILogger<ClientPortalClientChangeService> _logger;

    public ClientPortalClientChangeService(
        ClientPortalSettingsService settings,
        ITenantProvider tenant,
        IServiceProvider services,
        ILogger<ClientPortalClientChangeService> logger)
    {
        _settings = settings;
        _tenant = tenant;
        _services = services;
        _logger = logger;
    }

    /// <summary>
    /// Queues the message WITHOUT committing, for the caller that commits once itself. Does nothing when the company hasn't switched the portal
    /// connection on, or when no message bus is configured (development without RabbitMQ).
    /// </summary>
    public async Task<bool> StageAsync(Client client, string changeType, CancellationToken token) =>
        await StageManyAsync([client], changeType, token) > 0;

    /// <summary>
    /// Queues one message per client (one credentials check for all), WITHOUT committing. Used to send every client at once, for example when the
    /// connection is first switched on. Returns how many were queued; 0 when the connection is off, no bus is configured, or queuing failed.
    /// </summary>
    public async Task<int> StageManyAsync(IEnumerable<Client> clients, string changeType, CancellationToken token)
    {
        var list = clients.ToList();
        if (list.Count == 0)
            return 0;

        try
        {
            var publisher = _services.GetService<IPublishEndpoint>();
            if (publisher is null || await _settings.GetCredentialsAsync(token) is null)
                return 0;

            var tenantId = _tenant.TenantId;
            foreach (var client in list)
            {
                await publisher.Publish(new ClientChanged
                {
                    TenantId = tenantId,
                    ClientId = client.Id,
                    Code = client.Code,
                    Name = client.Name,
                    Status = client.Status,
                    ChangeType = changeType,
                    OccurredAtUtc = DateTime.UtcNow,
                }, token);
            }
            return list.Count;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The client is saved whatever happens to the portal; the portal catches up from the next change, the next DTR batch, or "send all clients".
            _logger.LogWarning(ex, "Could not queue the Client Portal update for {Count} client(s).", list.Count);
            return 0;
        }
    }
}
