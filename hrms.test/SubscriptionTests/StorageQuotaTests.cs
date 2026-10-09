using hrms.test.ServiceTests;
using hrms.test.TestSupport;
using Hrms.Core.Services;
using Hrms.Core.Services.Storage;
using Hrms.Core.Services.Subscriptions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Onepunch.Common.Lib.Subscriptions;

namespace hrms.test.SubscriptionTests;

/// <summary>
/// The storage plan limit (storage.max_mb = base capacity + purchased blocks): how a company's usage is
/// measured, what is reported to Tenant Service, and the refusal when a file doesn't fit.
/// </summary>
public class StorageQuotaTests : IDisposable
{
    private static readonly Guid Company = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();
    private const long Mb = 1024L * 1024L;

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"hrms-quota-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private LocalFileStorage Storage => new(_root);

    private async Task StoreAsync(string key, long bytes) =>
        await Storage.UploadAsync(new FileUpload(key, new MemoryStream(new byte[bytes]), "application/octet-stream"));

    private static EntitlementSnapshot SnapshotWith(int? storageMb, bool readOnly = false) => new()
    {
        TenantId = Company,
        Status = readOnly ? "Expired" : "Active",
        AccessMode = readOnly ? SubscriptionAccessMode.ReadOnly : SubscriptionAccessMode.Full,
        Limits = EntitlementKeys.Limits.ToDictionary(k => k, k => k == EntitlementKeys.StorageMaxMb ? storageMb ?? EntitlementKeys.Unlimited : EntitlementKeys.Unlimited),
    };

    private static StorageQuotaGuard GuardFor(EntitlementSnapshot snapshot, IStorageUsageMeter meter) =>
        new(Snapshots.Provider(snapshot), meter);

    // ── Measuring ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Storage_MeasuresOneCompanyFolder_AndNothingElse()
    {
        await StoreAsync($"tenants/{Company}/company-logo/2026/10/a.png", 3 * Mb);
        await StoreAsync($"tenants/{Company}/employee-photos/2026/10/b.png", Mb / 2);
        await StoreAsync($"tenants/{Other}/company-logo/2026/10/c.png", 7 * Mb);
        await StoreAsync($"archives/{Company}/records.zip", 9 * Mb);

        (await Storage.GetPrefixSizeAsync(StorageKeys.TenantPrefix(Company))).Should().Be(3 * Mb + Mb / 2);
        (await Storage.GetPrefixSizeAsync(StorageKeys.TenantPrefix(Guid.NewGuid()))).Should().Be(0);
    }

    [Fact]
    public async Task Storage_RefusesToMeasureAnythingButACompanyFolder()
    {
        var act = () => Storage.GetPrefixSizeAsync("tenants/");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Meter_ReportsWholeMegabytes_RoundedUp()
    {
        await StoreAsync($"tenants/{Company}/x/1.bin", 2 * Mb + 1);

        var usage = await new StorageUsageMeter(Storage).GetMegabytesAsync([Company, Other]);

        usage[Company].Should().Be(3);
        usage[Other].Should().Be(0);
    }

    [Fact]
    public async Task Meter_ReusesAMeasurement_UntilToldOtherwise()
    {
        var meter = new StorageUsageMeter(Storage);
        await StoreAsync($"tenants/{Company}/x/1.bin", Mb);
        (await meter.GetBytesAsync(Company)).Should().Be(Mb);

        await StoreAsync($"tenants/{Company}/x/2.bin", Mb); // written behind the meter's back
        (await meter.GetBytesAsync(Company)).Should().Be(Mb, "a recent measurement is reused");
        meter.Stored(Company, 100);
        (await meter.GetBytesAsync(Company)).Should().Be(Mb + 100, "uploads through the app keep it current");
        (await meter.GetBytesAsync(Company, fresh: true)).Should().Be(2 * Mb);

        await StoreAsync($"tenants/{Company}/x/3.bin", Mb);
        meter.Removed(Company);
        (await meter.GetBytesAsync(Company)).Should().Be(3 * Mb, "a deletion makes it measure again");
    }

    [Fact]
    public async Task Meter_MeasuresAgain_OnceTheMeasurementIsOld()
    {
        var clock = new FakeClock(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        var meter = new StorageUsageMeter(Storage, clock);
        await StoreAsync($"tenants/{Company}/x/1.bin", Mb);
        await meter.GetBytesAsync(Company);
        await StoreAsync($"tenants/{Company}/x/2.bin", Mb);

        clock.UtcNow += StorageUsageMeter.Lifetime + TimeSpan.FromSeconds(1);

        (await meter.GetBytesAsync(Company)).Should().Be(2 * Mb);
    }

    private sealed class FakeClock(DateTime utcNow) : TimeProvider
    {
        public DateTime UtcNow { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => new(UtcNow, TimeSpan.Zero);
    }

    // ── Reporting to Tenant Service ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Usage_IncludesStorageInMegabytes_PerCompany()
    {
        await StoreAsync($"tenants/{Company}/x/1.bin", 5 * Mb);
        using var db = new SqliteHrmsContext();
        await using var context = db.NewContext();

        var usage = await new SubscriptionUsageService(new UnitOfWorkService(context), new StorageUsageMeter(Storage))
            .GetUsageAsync([Company, Other], CancellationToken.None);

        usage.Single(u => u.TenantId == Company).Used[EntitlementKeys.StorageMaxMb].Should().Be(5);
        usage.Single(u => u.TenantId == Other).Used[EntitlementKeys.StorageMaxMb].Should().Be(0);
    }

    [Fact]
    public async Task Usage_WhenStorageCantBeMeasured_LeavesTheKeyOut_NotZero()
    {
        var meter = Substitute.For<IStorageUsageMeter>();
        meter.GetMegabytesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns<Dictionary<Guid, int>>(_ => throw new IOException("Spaces is down"));
        using var db = new SqliteHrmsContext();
        await using var context = db.NewContext();

        var usage = await new SubscriptionUsageService(new UnitOfWorkService(context), meter)
            .GetUsageAsync([Company], CancellationToken.None);

        usage.Single().Used.Should().NotContainKey(EntitlementKeys.StorageMaxMb);
        usage.Single().Used.Should().ContainKey(EntitlementKeys.EmployeesMax, "the rest is still reported");
    }

    [Fact]
    public async Task Usage_WithoutAMeter_LeavesStorageOut()
    {
        using var db = new SqliteHrmsContext();
        await using var context = db.NewContext();

        var usage = await new SubscriptionUsageService(new UnitOfWorkService(context)).GetUsageAsync([Company], CancellationToken.None);

        usage.Single().Used.Should().NotContainKey(EntitlementKeys.StorageMaxMb);
    }

    // ── The hard limit ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AFileThatFits_IsAllowed_UpToTheLastByte()
    {
        await StoreAsync($"tenants/{Company}/x/1.bin", 9 * Mb);
        var guard = GuardFor(SnapshotWith(10), new StorageUsageMeter(Storage));

        await guard.EnsureCanStoreAsync(Company, Mb, default);

        var tooBig = () => guard.EnsureCanStoreAsync(Company, Mb + 1, default);
        await tooBig.Should().ThrowAsync<PlanRestrictionException>();
    }

    [Fact]
    public async Task AFileThatDoesNotFit_IsRefused_TellingTheUserToBuyStorageBlocks()
    {
        await StoreAsync($"tenants/{Company}/x/1.bin", 9 * Mb);
        var guard = GuardFor(SnapshotWith(10), new StorageUsageMeter(Storage));

        var act = () => guard.EnsureCanStoreAsync(Company, 2 * Mb, default);

        var error = (await act.Should().ThrowAsync<PlanRestrictionException>()).Which;
        error.ProblemType.Should().Be(SubscriptionProblemTypes.PlanLimitReached);
        error.Message.Should().Contain("storage blocks").And.Contain("2 MB").And.Contain("1 MB");
        error.Extensions["limit"].Should().Be(EntitlementKeys.StorageMaxMb);
        error.Extensions["max"].Should().Be(10);
        error.Extensions["used"].Should().Be(9);
    }

    [Fact]
    public async Task APlanWithUnlimitedStorage_NeverRefuses()
    {
        var guard = GuardFor(SnapshotWith(null), new StorageUsageMeter(Storage));

        await guard.EnsureCanStoreAsync(Company, 500 * Mb, default);
    }

    [Fact]
    public async Task ARefusal_IsDoubleCheckedAgainstAFreshMeasurement()
    {
        var meter = Substitute.For<IStorageUsageMeter>();
        meter.GetBytesAsync(Company, false, Arg.Any<CancellationToken>()).Returns(9 * Mb);   // stale: files were deleted elsewhere
        meter.GetBytesAsync(Company, true, Arg.Any<CancellationToken>()).Returns(1 * Mb);
        var guard = GuardFor(SnapshotWith(10), meter);

        await guard.EnsureCanStoreAsync(Company, 5 * Mb, default);

        await meter.Received(1).GetBytesAsync(Company, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AReadOnlySubscription_CannotStoreFiles()
    {
        var guard = GuardFor(SnapshotWith(10, readOnly: true), new StorageUsageMeter(Storage));

        var act = () => guard.EnsureCanStoreAsync(Company, 1, default);

        (await act.Should().ThrowAsync<PlanRestrictionException>()).Which.ProblemType.Should().Be(SubscriptionProblemTypes.ReadOnly);
    }

    [Fact]
    public async Task AnEmptyFile_NeedsNoRoom()
    {
        var guard = GuardFor(SnapshotWith(0), new StorageUsageMeter(Storage));

        await guard.EnsureCanStoreAsync(Company, 0, default);
    }

    [Fact]
    public async Task APlanThatLacksTheKey_MeaningZero_FailsClosed()
    {
        var guard = GuardFor(SnapshotWith(0), new StorageUsageMeter(Storage));

        var act = () => guard.EnsureCanStoreAsync(Company, 1, default);

        await act.Should().ThrowAsync<PlanRestrictionException>();
    }

    // ── Where uploads ask ────────────────────────────────────────────────────────────────────

    private sealed class CompanyTenant : ITenantProvider
    {
        public Guid TenantId { get; private set; } = Company;
        public void SetTenantId(Guid tenantId) => TenantId = tenantId;
    }

    [Fact]
    public async Task ALogoThatDoesNotFit_IsRefused_AndNothingIsStored()
    {
        using var db = new SqliteHrmsContext();
        await using var context = db.NewContext();
        var storage = Storage;
        await StoreAsync($"tenants/{Company}/x/1.bin", 5 * Mb);
        var guard = GuardFor(SnapshotWith(5), new StorageUsageMeter(storage));
        var service = new OrganizationImageService(new UnitOfWorkService(context), storage, new CompanyTenant(), new ImageUrlResolver(storage), guard);

        var act = () => service.SetCompanyLogoAsync(new MemoryStream(ImageUploadsTests.Png), 12, "logo.png", default);

        await act.Should().ThrowAsync<PlanRestrictionException>().WithMessage("*storage blocks*");
        Directory.GetFiles(_root, "*", SearchOption.AllDirectories).Should().ContainSingle("only the file that was already there");
    }

    [Fact]
    public async Task ALogoThatFits_IsStored_AndCountedStraightAway()
    {
        using var db = new SqliteHrmsContext();
        await using var context = db.NewContext();
        var storage = Storage;
        var meter = new StorageUsageMeter(storage);
        var guard = GuardFor(SnapshotWith(5), meter);
        var service = new OrganizationImageService(new UnitOfWorkService(context), storage, new CompanyTenant(), new ImageUrlResolver(storage), guard);
        (await meter.GetBytesAsync(Company)).Should().Be(0);

        await service.SetCompanyLogoAsync(new MemoryStream(ImageUploadsTests.Png), 12, "logo.png", default);

        (await meter.GetBytesAsync(Company)).Should().Be(ImageUploadsTests.Png.Length);
    }
}
