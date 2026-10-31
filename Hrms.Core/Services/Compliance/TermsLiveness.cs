using System.Net;
using Onepunch.Common.Lib.Cache;

namespace Hrms.Core.Services.Compliance;

// Whether the Terms of Service are in effect -- the Platform Console's "Terms of Service live"
// switch. While they aren't, settings that differ from the DOLE defaults save without the
// acknowledgment (ComplianceAcknowledgmentService), since the Terms they point to aren't shown.
public interface ITermsLiveness
{
    Task<bool> IsLiveAsync(CancellationToken token = default);

    // Why the acknowledgments are or aren't being asked for -- shown on Company Settings >
    // Acknowledged Deviations so a silent "not live" is visible.
    async Task<TermsLivenessState> GetStateAsync(CancellationToken token = default) =>
        await IsLiveAsync(token) ? TermsLivenessState.Live : TermsLivenessState.NotLive;
}

public enum TermsLivenessState
{
    Live,
    // The Terms aren't in effect (the Platform Console switch is off, or nothing is published).
    NotLive,
    // Tenant Service couldn't be reached, so the Terms count as not live for now.
    Unreachable,
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

    public async Task<bool> IsLiveAsync(CancellationToken token = default) =>
        await GetStateAsync(token) == TermsLivenessState.Live;

    public async Task<TermsLivenessState> GetStateAsync(CancellationToken token = default)
    {
        if (Enum.TryParse<TermsLivenessState>(await TryCacheGetAsync(), out var cached)) return cached;

        TermsLivenessState state;
        var expiry = CacheFor;
        var client = httpClients.CreateClient(HttpClientName);
        try
        {
            using var response = await client.GetAsync("api/v1/legal/terms/current", token);
            if (response.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.NotFound))
                throw new HttpRequestException($"Tenant Service answered {(int)response.StatusCode}.");
            state = response.StatusCode == HttpStatusCode.OK ? TermsLivenessState.Live : TermsLivenessState.NotLive;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // An error, not a warning: until it's fixed every DOLE deviation saves without being
            // asked for or recorded. Usually ApiServices:TenantService points somewhere unreachable.
            Log.Logger.Error(ex, "Couldn't ask Tenant Service at {BaseAddress} whether the Terms are live; "
                + "DOLE deviations are saved without acknowledgment until it answers", client.BaseAddress);
            state = TermsLivenessState.Unreachable;
            expiry = FailOpenFor;
        }

        await TryCacheSetAsync(state.ToString(), expiry);
        return state;
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
