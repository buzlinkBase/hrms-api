using hrms.test.TestSupport;
using Hrms.adms.Insfrastructure;
using Hrms.adms.Messages;
using Hrms.Core.Services.Announcements.Lifecycle;
using Hrms.Core.Services.DataPurge;
using Hrms.Core.Services.Storage;
using Hrms.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Onepunch.Common.Lib.DTO;
using Onepunch.Common.Lib.Subscriptions;

namespace hrms.test.ServiceTests;

/// <summary>
/// The safety net for the yearly purge and the console's permanent deletion: EVERY table of the HR and
/// device databases is checked, read from the EF model, so a table added later is covered or the build
/// goes red. If one of these fails after you added an entity, give it a <c>Guid TenantId</c> (or a
/// single-column foreign key to a table that has one) -- don't add it to an exception list unless it
/// really holds no company data.
/// </summary>
public class CompanyDataCoverageTests : IDisposable
{
    private static readonly Guid CompanyA = Guid.NewGuid();
    private static readonly Guid CompanyB = Guid.NewGuid();

    private readonly SqliteHrmsContext _sqlite = new();
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"coverage-test-{Guid.NewGuid():N}");

    public void Dispose()
    {
        _sqlite.Dispose();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    // ── The model ────────────────────────────────────────────────────────────

    [Fact]
    public void EveryHrTable_IsTiedToACompany_ExceptMessagingAndTheAuditTrail()
    {
        using var db = _sqlite.NewContext();

        var catalog = CompanyDataCatalog.Build(db.Model, db.GetService<ISqlGenerationHelper>());

        catalog.Unscoped.Should().BeEquivalentTo(UniversalRowSeeder.InfrastructureTables,
            "a table the purge can't tie to a company would survive the account's deletion");
        var tables = db.Model.GetRelationalModel().Tables.Select(t => t.Name)
            .Where(t => !UniversalRowSeeder.InfrastructureTables.Contains(t));
        catalog.Tables.Select(t => t.Name).Should().BeEquivalentTo(tables);
    }

    [Fact]
    public void EveryTenantIdColumn_IsANonNullableGuid_SoThePurgeRecognisesIt()
    {
        using var hr = _sqlite.NewContext();
        using var adms = NewAdmsContext(out var keepAlive);
        using var _ = keepAlive;

        var wrong = hr.Model.GetEntityTypes().Concat(adms.Model.GetEntityTypes())
            .Select(t => (Type: t, Property: t.FindProperty("TenantId")))
            .Where(x => x.Property != null && x.Property.ClrType != typeof(Guid))
            .Select(x => $"{x.Type.ClrType.Name}.TenantId is {x.Property!.ClrType.Name}")
            .ToList();

        wrong.Should().BeEmpty("a TenantId that isn't a Guid is silently skipped by the purge");
    }

    [Fact]
    public void EveryDeviceTable_HasATenantId_ExceptMessagingAndTheAuditTrail()
    {
        using var db = NewAdmsContext(out var keepAlive);
        using var _ = keepAlive;

        var scoped = AdmsCompanyData.TenantTables(db.Model).Select(t => t.Table).ToHashSet();
        var missing = db.Model.GetRelationalModel().Tables.Select(t => t.Name)
            .Where(t => !scoped.Contains(t) && !UniversalRowSeeder.InfrastructureTables.Contains(t))
            .ToList();

        missing.Should().BeEmpty("the device service only deletes tables with a TenantId column");
    }

    // ── Every table, really deleted ──────────────────────────────────────────

    private CompanyDataPurgeService ServiceFor(HrmsContext db, IFileStorage storage)
    {
        var entitlements = Substitute.For<IEntitlementProvider>();
        entitlements.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(call => new EntitlementSnapshot
        {
            TenantId = call.Arg<Guid>(), Status = "Expired", AccessMode = SubscriptionAccessMode.ReadOnly,
        });
        ICompanyPurgeStep[] steps =
        [
            new RequireLockedStep(entitlements),
            new CancelScheduledJobsStep(Substitute.For<IAnnouncementJobScheduler>()),
            new ArchiveRecordsStep(storage, new CompanyArchiveWriter()),
            new DeleteRecordsStep(),
            new DeleteAuditTrailStep(),
            new DeleteFilesStep(storage),
            new ForgetCompanyCachesStep(entitlements, Substitute.For<Onepunch.Common.Lib.Cache.ICacheService>(), Substitute.For<IStorageUsageMeter>()),
        ];
        return new CompanyDataPurgeService(db, storage, steps, NullLogger<CompanyDataPurgeService>.Instance);
    }

    private async Task<(IReadOnlyList<string> Tables, CompanyDataPurged Result)> SeedAndRunAsync(CompanyPurgeStage stage)
    {
        IReadOnlyList<string> tables;
        using (var db = _sqlite.NewContext()) tables = UniversalRowSeeder.Seed(db, [CompanyA, CompanyB]);
        tables.Should().HaveCountGreaterThan(100, "every HR table is seeded");
        using (var db = _sqlite.NewContext())
            tables.Where(t => UniversalRowSeeder.Count(db, t) != 2).Should().BeEmpty("each table starts with one row per company");

        await using var run = _sqlite.NewContext();
        var result = await ServiceFor(run, new LocalFileStorage(_root)).RunAsync(
            new PurgeCompanyData { TenantId = CompanyA, OwnerUserId = Guid.NewGuid(), SubscriptionId = Guid.NewGuid(), Stage = stage },
            CancellationToken.None);
        return (tables, result);
    }

    [Fact]
    public async Task Erase_LeavesNoRowOfTheCompany_InAnyTable_AndEveryRowOfAnother()
    {
        var (tables, result) = await SeedAndRunAsync(CompanyPurgeStage.HardDelete);

        result.Succeeded.Should().BeTrue(result.Error);
        using var db = _sqlite.NewContext();
        var left = tables.Where(t => UniversalRowSeeder.Count(db, t) != 1).ToList();
        left.Should().BeEmpty("each table had one row per company, and only company B's may remain");
        var lostOther = tables.Where(t => UniversalRowSeeder.CountFor(db, t, CompanyB) is 0).ToList();
        lostOther.Should().BeEmpty("another company's rows are never touched");
    }

    [Fact]
    public async Task YearlyPurge_ArchivesAndDeletesEveryTable_KeepingOnlyTheCompanyRow()
    {
        var (tables, result) = await SeedAndRunAsync(CompanyPurgeStage.ArchiveAndPurge);

        result.Succeeded.Should().BeTrue(result.Error);
        result.ArchiveKey.Should().NotBeNull();
        using var db = _sqlite.NewContext();
        var companyTable = db.Model.FindEntityType(typeof(Company))!.GetTableName()!;
        var wrong = tables
            .Select(t => (Table: t, Rows: UniversalRowSeeder.Count(db, t), Expected: t == companyTable ? 2 : 1))
            .Where(x => x.Rows != x.Expected)
            .Select(x => $"{x.Table}: {x.Rows} rows, expected {x.Expected}")
            .ToList();
        wrong.Should().BeEmpty();
    }

    [Fact]
    public async Task DeviceErasure_LeavesNoRowOfTheCompany_InAnyTable()
    {
        using var keepAlive = OpenAdmsDatabase(out var options);
        IReadOnlyList<string> tables;
        using (var db = NewAdmsContext(options, CompanyA)) tables = UniversalRowSeeder.Seed(db, [CompanyA, CompanyB]);
        tables.Should().HaveCountGreaterThanOrEqualTo(13, "every device table is seeded");

        await using (var db = NewAdmsContext(options, CompanyA))
            await AdmsCompanyData.DeleteAsync(db, CompanyA, CancellationToken.None);

        using var check = NewAdmsContext(options, Guid.Empty);
        tables.Where(t => UniversalRowSeeder.Count(check, t) != 1).Should().BeEmpty();
        tables.Where(t => UniversalRowSeeder.CountFor(check, t, CompanyB) is 0).Should().BeEmpty();
    }

    // ── adms setup ───────────────────────────────────────────────────────────

    private static SqliteConnection OpenAdmsDatabase(out DbContextOptions<AdmsContext> options)
    {
        var connectionString = $"Data Source=file:{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        var keepAlive = new SqliteConnection(connectionString);
        keepAlive.Open();
        options = new DbContextOptionsBuilder<AdmsContext>().UseSqlite(connectionString).Options;
        using var db = NewAdmsContext(options, Guid.Empty);
        db.Database.EnsureCreated();
        return keepAlive;
    }

    private static AdmsContext NewAdmsContext(out SqliteConnection keepAlive)
    {
        keepAlive = OpenAdmsDatabase(out var options);
        return NewAdmsContext(options, Guid.Empty);
    }

    private static AdmsContext NewAdmsContext(DbContextOptions<AdmsContext> options, Guid tenantId)
    {
        var tenantProvider = Substitute.For<ITenantProvider>();
        tenantProvider.TenantId.Returns(tenantId);
        return new AdmsContext(options, tenantProvider);
    }
}
