using Hrms.Core.Services.Storage;
using Hrms.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Hrms.Core.Services.ClientPortal;

/// <summary>Lets whoever changes the company's name or logo tell the Client Portal, without knowing how.</summary>
public interface IClientPortalBrandingPublisher
{
    /// <summary>Sends the company's current name and logo. Best effort: false when nothing was sent, never an exception.</summary>
    Task<bool> PublishAsync(CancellationToken token);
}

/// <summary>
/// The agency's brand for its clients: the company name and logo, as HRIS has them now. Sent whole every time (a missing logo means "no
/// logo"), so the portal's copy always ends up matching HRIS, whichever change reached it last. A portal that is off, unreachable or
/// unhappy never blocks the company edit that triggered this; it catches up with the next change or "send all clients".
/// </summary>
public class ClientPortalBrandingService : BaseService<Company>, IClientPortalBrandingPublisher
{
    public const string Resource = "branding";
    public const int MaxLogoBytes = 2 * 1024 * 1024;

    private readonly ClientPortalSettingsService _settings;
    private readonly IClientPortalClient _client;
    private readonly IFileStorage _storage;
    private readonly ILogger<ClientPortalBrandingService> _logger;

    public ClientPortalBrandingService(
        IUnitOfWorkService uow,
        ClientPortalSettingsService settings,
        IClientPortalClient client,
        IFileStorage storage,
        ILogger<ClientPortalBrandingService> logger) : base(uow)
    {
        _settings = settings;
        _client = client;
        _storage = storage;
        _logger = logger;
    }

    public async Task<bool> PublishAsync(CancellationToken token)
    {
        try
        {
            var credentials = await _settings.GetCredentialsAsync(token);
            if (credentials is null)
                return false;

            var company = await Context.Companies.AsNoTracking().FirstOrDefaultAsync(token);
            var name = NameOf(company);
            if (name is null)
                return false;

            var row = new PortalBrandingRow(name, await LogoAsync(company?.LogoKey, token));
            var result = await _client.PutAsync(credentials, Resource, [row], token);
            if (result.Rejected > 0)
                _logger.LogWarning("The Client Portal refused the company branding: {Error}", result.Errors.FirstOrDefault()?.Error);
            return result.Accepted > 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not send the company name and logo to the Client Portal.");
            return false;
        }
    }

    /// <summary>"Company Name" (Description) is what the company is called everywhere; the short name only stands in when it is blank.</summary>
    public static string? NameOf(Company? company)
    {
        var name = !string.IsNullOrWhiteSpace(company?.Description) ? company!.Description : company?.ShortName;
        return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
    }

    private async Task<string?> LogoAsync(string? key, CancellationToken token)
    {
        if (!StorageKeys.IsValid(key))
            return null;

        await using var content = await _storage.OpenReadAsync(key!, token);
        if (content is null)
            return null;

        using var copy = new MemoryStream();
        await content.CopyToAsync(copy, token);
        // The upload limit already keeps logos small; a file that somehow isn't is left out rather than sent and refused.
        return copy.Length is > 0 and <= MaxLogoBytes ? Convert.ToBase64String(copy.ToArray()) : null;
    }
}
