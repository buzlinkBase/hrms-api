using StackExchange.Redis;
using System.Text.Json;
namespace Hrms.Api;

public class RedisCacheService : ICacheService
{
    private readonly IDatabase _db;
    private readonly ITenantProvider _tenantProvider;

    public RedisCacheService(IConnectionMultiplexer redis, ITenantProvider tenantProvider)
    {

        _db = redis.GetDatabase();
        _tenantProvider = tenantProvider;
    }

    private string BuildTenantKey(string key)
    {
        var tenant = _tenantProvider.TenantId.ToString();
        return $"{tenant}:{key}";
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        var fullKey = BuildTenantKey(key);
        var cache = await _db.StringGetAsync(fullKey);
        if (cache.IsNullOrEmpty)
            return default;

        return JsonSerializer.Deserialize<T>(cache!);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan expiry)
    {
        var fullKey = BuildTenantKey(key);
        var json = JsonSerializer.Serialize(value);

        await _db.StringSetAsync(fullKey, json, expiry);
    }

    public async Task RemoveAsync(string key)
    {
        var fullKey = BuildTenantKey(key);
        await _db.KeyDeleteAsync(fullKey);
    }
}