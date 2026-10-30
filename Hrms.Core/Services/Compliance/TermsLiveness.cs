using System.Net;
using Onepunch.Common.Lib.Cache;

namespace Hrms.Core.Services.Compliance;

// Whether the Terms of Service are in effect -- the Platform Console's "Terms of Service live"
// switch. While they aren't, settings that differ from the DOLE defaults save without the
// acknowledgment (ComplianceAcknowledgmentService), since the Terms they point to aren't shown.
public interface ITermsLiveness
{
    Task<bool> IsLiveAsync(CancellationToken token = default);
}

// Asks Tenant Service's public legal/terms/current, which answers 404 while the Terms aren't live
// (or nothing is published). One platform-wide answer, cached for five minutes -- so flipping the
// switch reaches hrms-api within that time. If Tenant Service can't be reached it counts as not
// live, briefly: like the entitlements, a dependency being down shouldn't block every save.
public sealed class TenantTermsLiveness(IHttpClientFactory httpClients, ICacheService cache) : ITermsLiveness
{
    public const string HttpClientName = "tenant-legal";
    private const string CacheKey = "legal:terms-live";
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan FailOpenFor = TimeSpan.FromSeconds(30);

    public async Task<bool> IsLiveAsync(CancellationToken token = default)
    {
        var cached = await TryCacheGetAsync();
        if (cached != null) return cached == "true";

        bool live;
        var expiry = CacheFor;
        try
        {
            using var response = await httpClients.CreateClient(HttpClientName).GetAsync("api/v1/legal/terms/current", token);
            if (response.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.NotFound))
                throw new HttpRequestException($"Tenant Service answered {(int)response.StatusCode}.");
            live = response.StatusCode == HttpStatusCode.OK;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Log.Logger.Warning(ex, "Couldn't ask Tenant Service whether the Terms are live; treating them as not live for now");
            live = false;
            expiry = FailOpenFor;
        }

        await TryCacheSetAsync(live ? "true" : "false", expiry);
        return live;
    }

    private async Task<string?> TryCacheGetAsync()
    {
        try { return await cache.GetAsync<string>(CacheKey); }
        catch (Exception ex)
        {
            Log.Logger.Warning(ex, "Terms liveness cache read failed");
            return null;
        }
    }

    private async Task TryCacheSetAsync(string value, TimeSpan expiry)
    {
        try { await cache.SetAsync(CacheKey, value, expiry); }
        catch (Exception ex) { Log.Logger.Warning(ex, "Terms liveness cache write failed"); }
    }
}
