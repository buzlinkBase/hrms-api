using System.IO.Compression;
using hrms.test.TestSupport;
using Hrms.Core.Services.DataPurge;
using Hrms.Core.Services.Storage;
using Hrms.Domain.Entities.Announcements;
using Hrms.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Onepunch.Common.Lib.DTO;
using Onepunch.Common.Lib.Subscriptions;

namespace hrms.test.ServiceTests;

/// <summary>
/// A locked company's data is archived, verified and removed from the live database a year on --
/// only that company's, only when it is read-only, and never before the archive is safely stored.
/// </summary>
public class CompanyDataPurgeTests : IDisposable
{
    private static readonly Guid CompanyA = Guid.NewGuid();
    private static readonly Guid CompanyB = Guid.NewGuid();
    private static readonly DateTime LastDepartment = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime LastHoliday = new(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly SqliteHrmsContext _sqlite = new();
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"purge-test-{Guid.NewGuid():N}");
    private readonly LocalFileStorage _storage;

    public CompanyDataPurgeTests()
    {
        _storage = new LocalFileStorage(_root);
    }

    public void Dispose()
    {
        _sqlite.Dispose();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    // ── Setup ────────────────────────────────────────────────────────────────

    private sealed class FakeEntitlements(bool readOnly) : IEntitlementProvider
    {
        public Task<EntitlementSnapshot> GetAsync(Guid tenantId, CancellationToken token = default) =>
            Task.FromResult(new EntitlementSnapshot
            {
                TenantId = tenantId,
                Status = readOnly ? "Expired" : "Active",
                AccessMode = readOnly ? SubscriptionAccessMode.ReadOnly : SubscriptionAccessMode.Full,
            });

        public Task InvalidateAsync(IEnumerable<Guid> tenantIds) => Task.CompletedTask;
    }

    /// <summary>Hands back different bytes than were stored, as a failing disk or network would.</summary>
    private sealed class CorruptingStorage(IFileStorage inner) : IFileStorage
    {
        public Task<StoredFile> UploadAsync(FileUpload upload, CancellationToken token = default) => inner.UploadAsync(upload, token);
        public Task<Stream?> OpenReadAsync(string key, CancellationToken token = default) =>
            Task.FromResult<Stream?>(new MemoryStream([1, 2, 3]));
        public Task<bool> ExistsAsync(string key, CancellationToken token = default) => inner.ExistsAsync(key, token);
        public Task DeleteAsync(string key, CancellationToken token = default) => inner.DeleteAsync(key, token);
        public Task<int> DeleteByPrefixAsync(string prefix, CancellationToken token = default) => inner.DeleteByPrefixAsync(prefix, token);
        public Task<Uri?> GetDownloadUrlAsync(string key, string? downloadFileName = null, CancellationToken token = default) =>
            inner.GetDownloadUrlAsync(key, downloadFileName, token);
    }

    private CompanyDataPurgeService ServiceFor(HrmsContext db, bool readOnly = true, IFileStorage? storage = null)
    {
        storage ??= _storage;
        ICompanyPurgeStep[] steps =
        [
            new RequireLockedStep(new FakeEntitlements(readOnly)),
            new ArchiveRecordsStep(storage, new CompanyArchiveWriter()),
            new DeleteRecordsStep(),
            new DeleteFilesStep(storage),
        ];
        return new CompanyDataPurgeService(db, storage, steps, NullLogger<CompanyDataPurgeService>.Instance);
    }

    private static PurgeCompanyData Request(Guid tenantId, CompanyPurgeStage stage = CompanyPurgeStage.ArchiveAndPurge) =>
        new() { TenantId = tenantId, OwnerUserId = Guid.NewGuid(), SubscriptionId = Guid.NewGuid(), Stage = stage };

    /// <summary>A company row (kept), a department and a holiday (archived), a notification and an announcement (deleted outright).</summary>
    private void Seed(Guid tenantId, string tag)
    {
        using var db = _sqlite.NewContext();
        db.Set<Company>().Add(new Company { Id = Guid.NewGuid(), TenantId = tenantId, Code = tag, Description = $"{tag} Corp", CreatedAt = new DateTime(2024, 1, 1) });
        db.Set<Department>().Add(new Department { Id = Guid.NewGuid(), TenantId = tenantId, Code = $"D-{tag}", Name = $"Dept {tag}", CreatedAt = LastDepartment });
        db.Set<Holiday>().Add(new Holiday { Id = Guid.NewGuid(), TenantId = tenantId, Description = $"Holiday {tag}", HolDate = new DateOnly(2025, 6, 12), CreatedAt = LastHoliday });
        db.Set<UserNotification>().Add(new UserNotification { Id = Guid.NewGuid(), TenantId = tenantId, ApplicantName = $"Notice {tag}", CreatedAt = new DateTime(2026, 9, 1) });
        db.Set<Announcement>().Add(new Announcement { Id = Guid.NewGuid(), TenantId = tenantId, Title = $"News {tag}", CreatedAt = new DateTime(2026, 9, 2) });
        db.SaveChanges();
    }

    private (int Companies, int Departments, int Holidays, int Notifications, int Announcements) Counts(Guid tenantId)
    {
        using var db = _sqlite.NewContext();
        return (
            db.Set<Company>().IgnoreQueryFilters().Count(x => x.TenantId == tenantId),
            db.Set<Department>().IgnoreQueryFilters().Count(x => x.TenantId == tenantId),
            db.Set<Holiday>().IgnoreQueryFilters().Count(x => x.TenantId == tenantId),
            db.Set<UserNotification>().IgnoreQueryFilters().Count(x => x.TenantId == tenantId),
            db.Set<Announcement>().IgnoreQueryFilters().Count(x => x.TenantId == tenantId));
    }

    private async Task<CompanyDataPurged> PurgeAsync(Guid tenantId, bool readOnly = true)
    {
        await using var db = _sqlite.NewContext();
        return await ServiceFor(db, readOnly).RunAsync(Request(tenantId), CancellationToken.None);
    }

    private async Task<Dictionary<string, string>> ReadArchiveAsync(string key)
    {
        await using var stored = await _storage.OpenReadAsync(key);
        stored.Should().NotBeNull();
        using var zip = new ZipArchive(stored!);
        return zip.Entries.ToDictionary(e => e.FullName, e =>
        {
            using var reader = new StreamReader(e.Open());
            return reader.ReadToEnd();
        });
    }

    // ── The catalog ──────────────────────────────────────────────────────────

    [Fact]
    public void Catalog_SortsTablesIntoArchivedDeletedAndKept()
    {
        using var db = _sqlite.NewContext();

        var catalog = CompanyDataCatalog.Build(db.Model, db.GetService<ISqlGenerationHelper>());

        TableOf<Company>(db, catalog).Kind.Should().Be(CompanyTableKind.Kept);
        TableOf<UserNotification>(db, catalog).Kind.Should().Be(CompanyTableKind.Operational);
        TableOf<Announcement>(db, catalog).Kind.Should().Be(CompanyTableKind.Operational);
        TableOf<Department>(db, catalog).Kind.Should().Be(CompanyTableKind.Compliance);
        TableOf<Holiday>(db, catalog).Kind.Should().Be(CompanyTableKind.Compliance);
    }

    [Fact]
    public void Catalog_NeverTouchesAuditEventsOrMessagingTables()
    {
        using var db = _sqlite.NewContext();

        var catalog = CompanyDataCatalog.Build(db.Model, db.GetService<ISqlGenerationHelper>());

        catalog.Tables.Select(t => t.Name).Should().NotContain(["AuditEvents", "OutboxMessage", "OutboxState", "InboxState"]);
    }

    [Fact]
    public void Catalog_DeletesChildrenBeforeTheirParents()
    {
        using var db = _sqlite.NewContext();
        var catalog = CompanyDataCatalog.Build(db.Model, db.GetService<ISqlGenerationHelper>());
        var order = catalog.ToDelete.Select(t => t.Name).ToList();

        // Every foreign key between two deleted tables points from a table deleted earlier.
        foreach (var entity in db.Model.GetEntityTypes().Where(e => e.GetTableName() != null))
        {
            foreach (var fk in entity.GetForeignKeys())
            {
                var child = entity.GetTableName()!;
                var parent = fk.PrincipalEntityType.GetTableName();
                if (parent == null || parent == child) continue;
                var childAt = order.IndexOf(child);
                var parentAt = order.IndexOf(parent);
                if (childAt < 0 || parentAt < 0) continue;
                childAt.Should().BeLessThan(parentAt, $"{child} refers to {parent}");
            }
        }
    }

    private static CompanyTable TableOf<T>(HrmsContext db, CompanyDataCatalog catalog)
    {
        var name = db.Model.FindEntityType(typeof(T))!.GetTableName();
        return catalog.Tables.Single(t => t.Name == name);
    }

    // ── The purge ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Purge_ArchivesTheRecords_ThenDeletesThemFromTheLiveDatabase()
    {
        Seed(CompanyA, "A");

        var result = await PurgeAsync(CompanyA);

        result.Succeeded.Should().BeTrue(result.Error);
        result.Stage.Should().Be(CompanyPurgeStage.ArchiveAndPurge);
        result.ArchiveKey.Should().StartWith(StorageKeys.ArchivePrefix(CompanyA));
        result.ArchiveSha256.Should().NotBeNullOrEmpty();
        result.RowsArchived.Should().BeGreaterThanOrEqualTo(3, "the company, a department and a holiday");
        result.RowsDeleted.Should().BeGreaterThanOrEqualTo(4);

        var live = Counts(CompanyA);
        live.Departments.Should().Be(0);
        live.Holidays.Should().Be(0);
        live.Notifications.Should().Be(0);
        live.Announcements.Should().Be(0);

        var archive = await ReadArchiveAsync(result.ArchiveKey!);
        archive.Keys.Should().Contain("manifest.json");
        archive.Single(e => e.Key.Contains("Department")).Value.Should().Contain("Dept A");
        archive.Single(e => e.Key.Contains("Holiday")).Value.Should().Contain("Holiday A");
    }

    [Fact]
    public async Task Purge_KeepsTheCompanysOwnRow_SoItStillOpens()
    {
        Seed(CompanyA, "A");

        await PurgeAsync(CompanyA);

        Counts(CompanyA).Companies.Should().Be(1);
    }

    [Fact]
    public async Task Purge_OnlyEverTouchesTheCompanyItWasAskedAbout()
    {
        Seed(CompanyA, "A");
        Seed(CompanyB, "B");

        await PurgeAsync(CompanyA);

        Counts(CompanyB).Should().Be((1, 1, 1, 1, 1));
    }

    [Fact]
    public async Task Purge_DoesNotArchiveNotificationsOrAnnouncements()
    {
        Seed(CompanyA, "A");

        var result = await PurgeAsync(CompanyA);

        var archive = await ReadArchiveAsync(result.ArchiveKey!);
        archive.Keys.Should().NotContain(k => k.Contains("Notification") || k.Contains("Announcement"));
    }

    [Fact]
    public async Task Purge_ReportsTheLatestEntry_ThatStartsTheFiveYearClock()
    {
        Seed(CompanyA, "A");

        var result = await PurgeAsync(CompanyA);

        result.LastEntryUtc.Should().Be(LastHoliday, "the notification and announcement are newer but aren't records to keep");
    }

    [Fact]
    public async Task Purge_AlsoRemovesRowsThatWereSoftDeleted()
    {
        Seed(CompanyA, "A");
        await using (var db = _sqlite.NewContext())
        {
            var department = await db.Set<Department>().IgnoreQueryFilters().SingleAsync(d => d.TenantId == CompanyA);
            department.DeletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        var result = await PurgeAsync(CompanyA);

        result.Succeeded.Should().BeTrue(result.Error);
        Counts(CompanyA).Departments.Should().Be(0);
    }

    [Fact]
    public async Task Purge_RefusesACompanyThatIsNotReadOnly_AndDeletesNothing()
    {
        Seed(CompanyA, "A");

        var result = await PurgeAsync(CompanyA, readOnly: false);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("isn't read-only");
        Counts(CompanyA).Should().Be((1, 1, 1, 1, 1));
        Directory.Exists(Path.Combine(_root, "archives")).Should().BeFalse("no archive was written");
    }

    [Fact]
    public async Task Purge_DeletesNothing_WhenTheArchiveCantBeReadBackIntact()
    {
        Seed(CompanyA, "A");

        CompanyDataPurged result;
        await using (var db = _sqlite.NewContext())
            result = await ServiceFor(db, storage: new CorruptingStorage(_storage)).RunAsync(Request(CompanyA), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("nothing was deleted");
        Counts(CompanyA).Should().Be((1, 1, 1, 1, 1));
        Directory.Exists(Path.Combine(_root, "archives", CompanyA.ToString("D")))
            .Should().BeTrue("the folder is made on upload");
        Directory.GetFiles(Path.Combine(_root, "archives", CompanyA.ToString("D"))).Should().BeEmpty("the bad archive is removed");
    }

    [Fact]
    public async Task Purge_Twice_IsHarmless_AndTheSecondRunChangesNothing()
    {
        Seed(CompanyA, "A");
        var first = await PurgeAsync(CompanyA);

        var second = await PurgeAsync(CompanyA);

        second.Succeeded.Should().BeTrue(second.Error);
        second.ArchiveKey.Should().BeNull("there was nothing left to archive");
        second.RowsArchived.Should().Be(0);
        (await _storage.ExistsAsync(first.ArchiveKey!)).Should().BeTrue("the first archive is untouched");
        Counts(CompanyA).Companies.Should().Be(1);
    }

    [Fact]
    public async Task Purge_DeletesTheCompanysUploadedFiles_ButNotAnotherCompanysOrTheArchive()
    {
        Seed(CompanyA, "A");
        var logoA = StorageKeys.ForNewFile(CompanyA, "company-logo", "logo.png");
        var logoB = StorageKeys.ForNewFile(CompanyB, "company-logo", "logo.png");
        foreach (var key in new[] { logoA, logoB })
            await _storage.UploadAsync(new FileUpload(key, new MemoryStream([9, 9]), "image/png"));

        var result = await PurgeAsync(CompanyA);

        result.FilesDeleted.Should().Be(1);
        (await _storage.ExistsAsync(logoA)).Should().BeFalse();
        (await _storage.ExistsAsync(logoB)).Should().BeTrue();
        (await _storage.ExistsAsync(result.ArchiveKey!)).Should().BeTrue();
    }

    [Fact]
    public async Task DestroyArchive_RemovesTheArchive_AndNothingElse()
    {
        Seed(CompanyA, "A");
        Seed(CompanyB, "B");
        var a = await PurgeAsync(CompanyA);
        var b = await PurgeAsync(CompanyB);

        CompanyDataPurged destroyed;
        await using (var db = _sqlite.NewContext())
            destroyed = await ServiceFor(db).RunAsync(Request(CompanyA, CompanyPurgeStage.DestroyArchive), CancellationToken.None);

        destroyed.Succeeded.Should().BeTrue();
        destroyed.Stage.Should().Be(CompanyPurgeStage.DestroyArchive);
        destroyed.FilesDeleted.Should().Be(1);
        (await _storage.ExistsAsync(a.ArchiveKey!)).Should().BeFalse();
        (await _storage.ExistsAsync(b.ArchiveKey!)).Should().BeTrue();
    }

    [Fact]
    public async Task APurgeWithoutACompany_IsRefused()
    {
        await using var db = _sqlite.NewContext();

        var result = await ServiceFor(db).RunAsync(Request(Guid.Empty), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
    }

    // ── Storage ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("tenants/9f4b2c1e-0d3a-4b8e-9c1f-2a7d5e6f8b30/", true)]
    [InlineData("archives/9f4b2c1e-0d3a-4b8e-9c1f-2a7d5e6f8b30/", true)]
    [InlineData("tenants/", false)]
    [InlineData("tenants/9f4b2c1e-0d3a-4b8e-9c1f-2a7d5e6f8b30", false)]
    [InlineData("tenants/not-a-guid/", false)]
    [InlineData("other/9f4b2c1e-0d3a-4b8e-9c1f-2a7d5e6f8b30/", false)]
    [InlineData("tenants/9f4b2c1e-0d3a-4b8e-9c1f-2a7d5e6f8b30/employee-photos/", false)]
    [InlineData("tenants/00000000-0000-0000-0000-000000000000/", false)]
    [InlineData("", false)]
    [InlineData("/", false)]
    public void OnlyAWholeCompanyFolder_CanBeDeletedByPrefix(string prefix, bool allowed)
    {
        StorageKeys.IsCompanyFolder(prefix).Should().Be(allowed);
    }

    [Fact]
    public async Task DeleteByPrefix_RefusesAnythingButACompanyFolder()
    {
        var act = () => _storage.DeleteByPrefixAsync("tenants/");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DeleteByPrefix_OfAnEmptyFolder_DoesNothing()
    {
        (await _storage.DeleteByPrefixAsync(StorageKeys.TenantPrefix(Guid.NewGuid()))).Should().Be(0);
    }
}
