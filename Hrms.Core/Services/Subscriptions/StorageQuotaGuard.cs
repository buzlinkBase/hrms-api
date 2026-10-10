using Hrms.Core.Services.Storage;
using Onepunch.Common.Lib.Subscriptions;

namespace Hrms.Core.Services.Subscriptions;

/// <summary>
/// Refuses storing a file the account has no room for (throws <see cref="PlanRestrictionException"/>).
/// Every code path that puts a file into <see cref="IFileStorage"/> asks this first, then reports what
/// it stored. <c>storage.max_mb</c> is the plan's base capacity plus any storage blocks bought, and it is
/// one pool for the whole account: every company the owner has counts against it together.
/// </summary>
public interface IStorageQuotaGuard
{
    Task EnsureCanStoreAsync(Guid tenantId, long bytes, CancellationToken token);

    /// <summary>The file was stored; keeps the running tally current.</summary>
    void Stored(Guid tenantId, long bytes);

    /// <summary>Files were deleted; the tally measures again.</summary>
    void Removed(Guid tenantId);
}

public sealed class StorageQuotaGuard(IEntitlementProvider entitlements, IStorageUsageMeter meter) : IStorageQuotaGuard
{
    public async Task EnsureCanStoreAsync(Guid tenantId, long bytes, CancellationToken token)
    {
        if (bytes <= 0) return;

        var snapshot = await entitlements.GetAsync(tenantId, token);
        if (snapshot.IsReadOnly) throw PlanRestrictionException.ReadOnly();
        if (snapshot.LimitOf(EntitlementKeys.StorageMaxMb) is not { } limitMb) return;

        // Every company of the account shares the limit; this one is counted even if the snapshot predates it.
        IReadOnlyCollection<Guid> pool = snapshot.AccountTenantIds.Contains(tenantId)
            ? snapshot.AccountTenantIds
            : [.. snapshot.AccountTenantIds, tenantId];

        var limitBytes = limitMb * StorageUsageMeter.BytesPerMb;
        var used = await meter.GetTotalBytesAsync(pool, token: token);
        if (used + bytes <= limitBytes) return;

        // The tally can be stale (another instance stored or deleted files): measure before refusing.
        used = await meter.GetTotalBytesAsync(pool, fresh: true, token: token);
        if (used + bytes <= limitBytes) return;

        throw PlanRestrictionException.LimitReached(
            EntitlementKeys.StorageMaxMb, limitMb, ToMb(used), Message(limitBytes, used, bytes));
    }

    public void Stored(Guid tenantId, long bytes) => meter.Stored(tenantId, bytes);

    public void Removed(Guid tenantId) => meter.Removed(tenantId);

    private static int ToMb(long bytes) => (int)Math.Min(int.MaxValue, (bytes + StorageUsageMeter.BytesPerMb - 1) / StorageUsageMeter.BytesPerMb);

    private static string Message(long limitBytes, long usedBytes, long adding) =>
        $"This file needs {Size(adding)}, but your account has only {Size(Math.Max(0, limitBytes - usedBytes))} of storage left across all your companies " +
        $"({Size(usedBytes)} of {Size(limitBytes)} used). Buy additional storage blocks in Billing, or delete files you no longer need, to continue.";

    private static string Size(long bytes) => bytes switch
    {
        >= 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024 * 1024):0.##} GB",
        >= 1024L * 1024 => $"{bytes / (1024.0 * 1024):0.##} MB",
        >= 1024 => $"{bytes / 1024.0:0.##} KB",
        _ => $"{bytes} bytes",
    };
}
