using System.Collections.Concurrent;

namespace Hrms.Core.Services.Storage;

/// <summary>How many bytes each company has in file storage.</summary>
public interface IStorageUsageMeter
{
    /// <summary>The company's stored bytes. A recent measurement is reused unless <paramref name="fresh"/>.</summary>
    Task<long> GetBytesAsync(Guid tenantId, bool fresh = false, CancellationToken token = default);

    /// <summary>Several companies' bytes added together -- what an account's one storage pool is measured by.</summary>
    Task<long> GetTotalBytesAsync(IReadOnlyCollection<Guid> tenantIds, bool fresh = false, CancellationToken token = default);

    /// <summary>Whole megabytes (rounded up) for each company; Tenant Service adds them up per account.</summary>
    Task<Dictionary<Guid, int>> GetMegabytesAsync(IReadOnlyCollection<Guid> tenantIds, CancellationToken token = default);

    /// <summary>A file of <paramref name="bytes"/> was just stored for the company.</summary>
    void Stored(Guid tenantId, long bytes);

    /// <summary>Files were deleted for the company; the next read measures again.</summary>
    void Removed(Guid tenantId);
}

/// <summary>
/// Measures a company's folder in <see cref="IFileStorage"/> (the files themselves are the source
/// of truth -- no table of sizes to drift). Listing a folder is slow, so a measurement is reused for
/// <see cref="Lifetime"/> and kept current in between by <see cref="Stored"/>; a deletion drops it so
/// the next read measures again. Other app instances catch up when their own copy expires, which is why
/// <c>StorageQuotaGuard</c> re-measures before it refuses an upload.
/// </summary>
public sealed class StorageUsageMeter(IFileStorage storage, TimeProvider? clock = null) : IStorageUsageMeter
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly ConcurrentDictionary<Guid, (long Bytes, DateTime MeasuredAtUtc)> _cache = new();

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    public async Task<long> GetBytesAsync(Guid tenantId, bool fresh = false, CancellationToken token = default)
    {
        if (!fresh && _cache.TryGetValue(tenantId, out var cached) && Now - cached.MeasuredAtUtc < Lifetime)
            return cached.Bytes;

        var bytes = await storage.GetPrefixSizeAsync(StorageKeys.TenantPrefix(tenantId), token);
        _cache[tenantId] = (bytes, Now);
        return bytes;
    }

    public async Task<long> GetTotalBytesAsync(IReadOnlyCollection<Guid> tenantIds, bool fresh = false, CancellationToken token = default)
    {
        long total = 0;
        foreach (var tenantId in tenantIds.Distinct())
            total += await GetBytesAsync(tenantId, fresh, token);
        return total;
    }

    public async Task<Dictionary<Guid, int>> GetMegabytesAsync(IReadOnlyCollection<Guid> tenantIds, CancellationToken token = default)
    {
        var result = new Dictionary<Guid, int>();
        foreach (var tenantId in tenantIds.Distinct())
        {
            var bytes = await GetBytesAsync(tenantId, token: token);
            result[tenantId] = (int)Math.Min(int.MaxValue, (bytes + BytesPerMb - 1) / BytesPerMb);
        }
        return result;
    }

    public void Stored(Guid tenantId, long bytes)
    {
        if (bytes <= 0) return;
        _cache.AddOrUpdate(tenantId, _ => (bytes, Now), (_, current) => (current.Bytes + bytes, current.MeasuredAtUtc));
    }

    public void Removed(Guid tenantId) => _cache.TryRemove(tenantId, out _);

    public const long BytesPerMb = 1024L * 1024L;
}
