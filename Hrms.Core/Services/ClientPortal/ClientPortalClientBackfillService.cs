namespace Hrms.Core.Services.ClientPortal;

/// <summary>
/// Sends every client this company has to the Client Portal. A client reaches the portal when it is created or changed, so the ones that
/// already existed when the connection was first switched on would otherwise wait until someone edits them. The portal makes (or updates) its
/// copy of each by id, so sending them again is harmless.
/// </summary>
public class ClientPortalClientBackfillService
{
    private readonly ClientService _clients;
    private readonly ClientPortalClientChangeService _changes;
    private readonly ClientPortalSettingsService _settings;

    public ClientPortalClientBackfillService(ClientService clients, ClientPortalClientChangeService changes, ClientPortalSettingsService settings)
    {
        _clients = clients;
        _changes = changes;
        _settings = settings;
    }

    /// <summary>Queues every client and commits once. Returns how many were queued.</summary>
    public async Task<int> SendAllAsync(CancellationToken token)
    {
        if (await _settings.GetCredentialsAsync(token) is null)
            throw new ValidationException("The client portal connection is switched off or incomplete.");

        var clients = await _clients.FindAllAsync(token);
        var queued = await _changes.StageManyAsync(clients, ClientPortalClientChangeService.Updated, token);
        await _settings.CommitChangesAsync(token);
        return queued;
    }
}
