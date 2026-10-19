using System.Text.Json;
using Hrms.Domain.Entities;
using Microsoft.AspNetCore.DataProtection;

namespace Hrms.Core.Services.ClientPortal;

/// <summary>
/// A company's connection to the Client Portal: on/off, the portal's address and the API key it was given. Kept as
/// GeneralSetting rows. The key is encrypted with the data-protection API and is never returned to a caller.
/// </summary>
public class ClientPortalSettingsService : BaseService<GeneralSetting>
{
    public const string IdentityType = "ClientPortal";
    public const string EnabledKey = "Enabled";
    public const string PortalUrlKey = "PortalUrl";
    public const string ApiKeyKey = "ApiKey";
    public const string LastSyncKey = "LastSync";
    private const string Purpose = "ClientPortal.IngestionKey";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly IDataProtector _protector;

    public ClientPortalSettingsService(IUnitOfWorkService uow, IDataProtectionProvider protection) : base(uow)
    {
        _protector = protection.CreateProtector(Purpose);
    }

    public async Task<ClientPortalSettingsModel> GetAsync(CancellationToken token)
    {
        var rows = await LoadAsync(asTracking: false, token);
        return new ClientPortalSettingsModel(
            Enabled: rows.TryGetValue(EnabledKey, out var enabled) && bool.TryParse(enabled.Value, out var on) && on,
            PortalUrl: rows.TryGetValue(PortalUrlKey, out var url) ? url.Value : string.Empty,
            HasApiKey: rows.TryGetValue(ApiKeyKey, out var key) && !string.IsNullOrEmpty(key.Value),
            LastSync: rows.TryGetValue(LastSyncKey, out var last) ? ParseSummary(last.Value) : null);
    }

    /// <summary>
    /// Whether the connection is switched on. Attendance travels over the message bus, so it needs nothing else; the address and key are
    /// only for what still goes over HTTP (branding).
    /// </summary>
    public async Task<bool> IsEnabledAsync(CancellationToken token)
    {
        var rows = await LoadAsync(asTracking: false, token);
        return rows.TryGetValue(EnabledKey, out var enabled) && bool.TryParse(enabled.Value, out var on) && on;
    }

    /// <summary>The address and key to push with, or null when the connection is switched off or incomplete.</summary>
    public async Task<ClientPortalCredentials?> GetCredentialsAsync(CancellationToken token)
    {
        var rows = await LoadAsync(asTracking: false, token);
        if (!rows.TryGetValue(EnabledKey, out var enabled) || !bool.TryParse(enabled.Value, out var on) || !on)
            return null;
        if (!rows.TryGetValue(PortalUrlKey, out var url) || string.IsNullOrWhiteSpace(url.Value))
            return null;
        if (!rows.TryGetValue(ApiKeyKey, out var key) || string.IsNullOrEmpty(key.Value))
            return null;

        try
        {
            return new ClientPortalCredentials(url.Value, _protector.Unprotect(key.Value));
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            // Encrypted under keys this server no longer has: treat as not configured; the admin enters it again.
            return null;
        }
    }

    public async Task<ClientPortalSettingsModel> SaveAsync(ClientPortalSettingsRequest request, CancellationToken token)
    {
        var url = NormalizeUrl(request.PortalUrl);
        var rows = await LoadAsync(asTracking: true, token);

        var hasKey = rows.TryGetValue(ApiKeyKey, out var existingKey) && !string.IsNullOrEmpty(existingKey.Value);
        var newKey = string.IsNullOrWhiteSpace(request.ApiKey) ? null : request.ApiKey.Trim();
        if (request.Enabled && !hasKey && newKey is null)
            throw new ValidationException("Enter the API key the portal gave you before switching the connection on.");

        Upsert(rows, EnabledKey, request.Enabled ? "true" : "false");
        Upsert(rows, PortalUrlKey, url);
        if (newKey is not null)
            Upsert(rows, ApiKeyKey, _protector.Protect(newKey));

        await CommitChangesAsync(token);
        return await GetAsync(token);
    }

    public async Task RecordSyncAsync(ClientPortalSyncSummary summary, CancellationToken token)
    {
        var rows = await LoadAsync(asTracking: true, token);
        Upsert(rows, LastSyncKey, JsonSerializer.Serialize(summary, Json));
        await CommitChangesAsync(token);
    }

    /// <summary>Folds one of the portal's answers into the last sync shown on the settings screen and saves it.</summary>
    public async Task RecordResultAsync(ClientPortalAttendanceResult result, CancellationToken token)
    {
        var last = (await GetAsync(token)).LastSync;
        await RecordSyncAsync(Merge(last, result), token);
    }

    /// <summary>
    /// A send split into several messages gets one answer per message; they are added up while they belong to the same send (same batch,
    /// operation and number of parts, not all answered yet). Anything else starts a new summary. For an upsert the row count is the one
    /// recorded when it was sent; removals are never recorded when sent, so theirs is added up from the answers.
    /// </summary>
    public static ClientPortalSyncSummary Merge(ClientPortalSyncSummary? last, ClientPortalAttendanceResult result)
    {
        var errors = result.Errors ?? [];
        var sameSend = last is not null
            && last.BatchCode == result.BatchCode
            && last.Operation == result.Operation
            && last.Parts == result.Parts
            && last.PartsReceived < last.Parts;

        int records, accepted, rejected, received;
        List<PortalRowError> allErrors;
        if (sameSend)
        {
            records = result.Operation == ClientPortalAttendanceResult.Remove ? last!.Records + result.Records : last!.Records;
            accepted = last.Accepted + result.Accepted;
            rejected = last.Rejected + result.Rejected;
            received = last.PartsReceived + 1;
            allErrors = (last.Errors ?? []).Concat(errors).Take(50).ToList();
        }
        else
        {
            records = result.Records;
            accepted = result.Accepted;
            rejected = result.Rejected;
            received = 1;
            allErrors = errors.Take(50).ToList();
        }

        var parts = Math.Max(result.Parts, 1);
        var waiting = received < parts;
        var message = waiting ? $"Waiting for the portal ({received} of {parts} parts answered)."
            : rejected > 0 ? "Some rows were refused by the portal."
            : null;
        return new ClientPortalSyncSummary(result.BatchCode, result.AtUtc, records, accepted, rejected,
            Succeeded: rejected == 0, Message: message, Errors: allErrors.Count == 0 ? null : allErrors,
            Operation: result.Operation, Waiting: waiting, PartsReceived: received, Parts: parts);
    }

    /// <summary>Only https is accepted, except for a portal running on this machine while developing.</summary>
    public static string NormalizeUrl(string? value)
    {
        var text = (value ?? string.Empty).Trim().TrimEnd('/');
        if (text.Length == 0)
            return string.Empty;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new ValidationException("The portal address must be a full web address, for example https://portal.example.com.");
        if (uri.Scheme == Uri.UriSchemeHttp && !uri.IsLoopback)
            throw new ValidationException("The portal address must use https.");
        if (!string.IsNullOrEmpty(uri.UserInfo))
            throw new ValidationException("Do not put a user name or password in the portal address.");
        return uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
    }

    private async Task<Dictionary<string, GeneralSetting>> LoadAsync(bool asTracking, CancellationToken token)
    {
        var list = await GetQueryable(x => x.IdentityType == IdentityType, noTracking: !asTracking).ToListAsync(token);
        return list.GroupBy(x => x.Description).ToDictionary(g => g.Key, g => g.First());
    }

    private void Upsert(Dictionary<string, GeneralSetting> rows, string key, string value)
    {
        if (rows.TryGetValue(key, out var row))
        {
            row.Value = value;
            return;
        }
        row = new GeneralSetting { IdentityType = IdentityType, Description = key, Value = value };
        Context.GeneralSettings.Add(row);
        rows[key] = row;
    }

    private static ClientPortalSyncSummary? ParseSummary(string value)
    {
        try
        {
            return JsonSerializer.Deserialize<ClientPortalSyncSummary>(value, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
