using Hrms.adms.Insfrastructure;
using MassTransit;
using Hrms.adms.Messages;
using Hrms.adms.Models.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Onepunch.AuditEngine.EntityFrameworkCore;
using Onepunch.Common.Lib.DTO;
using Onepunch.Common.Lib.Subscriptions;

namespace hrms.test.ServiceTests;

/// <summary>A locked company's devices, device users and punches leave the device service's database; other companies' stay.</summary>
public class AdmsCompanyDataTests : IDisposable
{
    private static readonly Guid CompanyA = Guid.NewGuid();
    private static readonly Guid CompanyB = Guid.NewGuid();

    private readonly SqliteConnection _keepAlive;
    private readonly DbContextOptions<AdmsContext> _options;

    public AdmsCompanyDataTests()
    {
        var connectionString = $"Data Source=file:{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _keepAlive = new SqliteConnection(connectionString);
        _keepAlive.Open();
        _options = new DbContextOptionsBuilder<AdmsContext>().UseSqlite(connectionString).Options;
        using var db = NewContext(Guid.Empty);
        db.Database.EnsureCreated();
    }

    public void Dispose() => _keepAlive.Dispose();

    private AdmsContext NewContext(Guid tenantId)
    {
        var tenantProvider = Substitute.For<ITenantProvider>();
        tenantProvider.TenantId.Returns(tenantId);
        return new AdmsContext(_options, tenantProvider);
    }

    private void SeedDevice(Guid tenantId, string serial)
    {
        using var db = NewContext(tenantId);
        db.BiometricDevices.Add(new BiometricDevice
        {
            Id = Guid.NewGuid(), TenantId = tenantId, SN = serial, DeviceName = $"Gate {serial}", Description = "Lobby",
            BranchId = Guid.NewGuid(), ClientId = Guid.NewGuid(), DepartmentId = Guid.NewGuid(), OperationAreaId = Guid.NewGuid(),
            Status = "Active", IpAddress = "10.0.0.1", FwVersion = "6.50",
        });
        db.SaveChanges();
    }

    private int Devices(Guid tenantId)
    {
        using var db = NewContext(Guid.Empty);
        return db.BiometricDevices.IgnoreQueryFilters().Count(d => d.TenantId == tenantId);
    }

    [Fact]
    public void EveryTableWithATenantIdIsFound()
    {
        using var db = NewContext(Guid.Empty);

        var tables = AdmsCompanyData.TenantTables(db.Model).Select(t => t.Table).ToList();

        tables.Should().Contain(db.Model.FindEntityType(typeof(BiometricDevice))!.GetTableName());
        tables.Should().Contain(db.Model.FindEntityType(typeof(Hrms.adms.Models.Entities.Attendance))!.GetTableName());
        tables.Should().Contain(db.Model.FindEntityType(typeof(BiometricTemplate))!.GetTableName());
    }

    [Fact]
    public async Task Delete_RemovesOnlyThatCompanysRows()
    {
        SeedDevice(CompanyA, "A-1");
        SeedDevice(CompanyA, "A-2");
        SeedDevice(CompanyB, "B-1");

        await using (var db = NewContext(CompanyA))
        {
            var deleted = await AdmsCompanyData.DeleteAsync(db, CompanyA, CancellationToken.None);
            deleted.Should().Be(2);
        }

        Devices(CompanyA).Should().Be(0);
        Devices(CompanyB).Should().Be(1);
    }

    [Fact]
    public async Task Delete_ForACompanyWithNothing_DoesNothing()
    {
        SeedDevice(CompanyB, "B-1");

        await using var db = NewContext(CompanyA);
        (await AdmsCompanyData.DeleteAsync(db, CompanyA, CancellationToken.None)).Should().Be(0);

        Devices(CompanyB).Should().Be(1);
    }

    // ── The worker ───────────────────────────────────────────────────────────

    private async Task<ConsumeContext<PurgeCompanyData>> ConsumeAsync(
        Guid tenantId, CompanyPurgeStage stage, bool readOnly, IEntitlementProvider? entitlements = null, Guid? headerTenant = null)
    {
        if (entitlements == null)
        {
            entitlements = Substitute.For<IEntitlementProvider>();
            entitlements.GetAsync(tenantId, Arg.Any<CancellationToken>()).Returns(new EntitlementSnapshot
            {
                TenantId = tenantId,
                Status = readOnly ? "Expired" : "Active",
                AccessMode = readOnly ? SubscriptionAccessMode.ReadOnly : SubscriptionAccessMode.Full,
            });
        }
        var tenantProvider = Substitute.For<ITenantProvider>();
        tenantProvider.TenantId.Returns(headerTenant ?? tenantId);
        await using var db = NewContext(tenantId);
        var worker = new PurgeCompanyDataWorker(db, entitlements, tenantProvider, NullLogger<PurgeCompanyDataWorker>.Instance);
        var context = Substitute.For<ConsumeContext<PurgeCompanyData>>();
        context.Message.Returns(new PurgeCompanyData { TenantId = tenantId, Stage = stage, OwnerUserId = Owner, SubscriptionId = Subscription });
        context.CancellationToken.Returns(CancellationToken.None);
        await worker.Consume(context);
        return context;
    }

    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid Subscription = Guid.NewGuid();

    /// <summary>The answers the worker published, with the headers it set on each.</summary>
    private static List<(CompanyDataPurged Message, Dictionary<string, object?> Headers)> Answers(ConsumeContext<PurgeCompanyData> context) =>
        context.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IPublishEndpoint.Publish) && c.GetArguments()[0] is CompanyDataPurged)
            .Select(c =>
            {
                var headers = new Dictionary<string, object?>();
                if (c.GetArguments().OfType<IPipe<PublishContext<CompanyDataPurged>>>().FirstOrDefault() is { } pipe)
                {
                    var publish = Substitute.For<PublishContext<CompanyDataPurged>>();
                    var sendHeaders = Substitute.For<SendHeaders>();
                    sendHeaders.When(h => h.Set(Arg.Any<string>(), Arg.Any<object?>(), Arg.Any<bool>()))
                        .Do(x => headers[x.ArgAt<string>(0)] = x.ArgAt<object?>(1));
                    sendHeaders.When(h => h.Set(Arg.Any<string>(), Arg.Any<string>()))
                        .Do(x => headers[x.ArgAt<string>(0)] = x.ArgAt<string>(1));
                    publish.Headers.Returns(sendHeaders);
                    pipe.Send(publish).GetAwaiter().GetResult();
                }
                return ((CompanyDataPurged)c.GetArguments()[0]!, headers);
            })
            .ToList();

    private void SeedAudit(Guid tenantId)
    {
        using var db = NewContext(tenantId);
        db.Set<AuditEventRecord>().Add(new AuditEventRecord
        {
            Id = Guid.NewGuid(), OccurredAtUtc = DateTime.UtcNow, EventType = "Update", OrganizationId = tenantId.ToString(),
        });
        db.SaveChanges();
    }

    private int AuditRows(Guid tenantId)
    {
        using var db = NewContext(Guid.Empty);
        var organization = tenantId.ToString();
        return db.Set<AuditEventRecord>().Count(a => a.OrganizationId == organization);
    }

    [Fact]
    public async Task ThePurge_LeavesALiveCompanyAlone()
    {
        SeedDevice(CompanyA, "A-1");

        await ConsumeAsync(CompanyA, CompanyPurgeStage.ArchiveAndPurge, readOnly: false);

        Devices(CompanyA).Should().Be(1);
    }

    [Fact]
    public async Task AnErasure_TakesALiveCompanysDevicesAndAuditTrail_AndNoOneElses()
    {
        SeedDevice(CompanyA, "A-1");
        SeedDevice(CompanyB, "B-1");
        SeedAudit(CompanyA);
        SeedAudit(CompanyB);
        var entitlements = Substitute.For<IEntitlementProvider>();

        await ConsumeAsync(CompanyA, CompanyPurgeStage.HardDelete, readOnly: false, entitlements);

        Devices(CompanyA).Should().Be(0);
        AuditRows(CompanyA).Should().Be(0);
        Devices(CompanyB).Should().Be(1);
        AuditRows(CompanyB).Should().Be(1);
        await entitlements.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await entitlements.Received(1).InvalidateAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Single() == CompanyA));
    }

    [Fact]
    public async Task AnErasure_AnswersTenantService_AsTheDeviceService()
    {
        SeedDevice(CompanyA, "A-1");

        var context = await ConsumeAsync(CompanyA, CompanyPurgeStage.HardDelete, readOnly: false);

        var (answer, headers) = Answers(context).Should().ContainSingle().Subject;
        answer.Succeeded.Should().BeTrue(answer.Error);
        answer.Stage.Should().Be(CompanyPurgeStage.HardDelete);
        answer.TenantId.Should().Be(CompanyA);
        answer.OwnerUserId.Should().Be(Owner);
        answer.SubscriptionId.Should().Be(Subscription);
        answer.RowsDeleted.Should().Be(1);
        headers[PurgeCompanyDataWorker.SourceHeader].Should().Be(PurgeCompanyDataWorker.Source);
        headers["X-Tenant-ID"].Should().Be(CompanyA.ToString());
    }

    [Fact]
    public async Task AnErasureThatFails_IsReported_NotSwallowed()
    {
        var entitlements = Substitute.For<IEntitlementProvider>();
        entitlements.InvalidateAsync(Arg.Any<IEnumerable<Guid>>()).Returns(Task.FromException(new InvalidOperationException("cache down")));

        var context = await ConsumeAsync(CompanyA, CompanyPurgeStage.HardDelete, readOnly: false, entitlements);

        var (answer, headers) = Answers(context).Should().ContainSingle().Subject;
        answer.Succeeded.Should().BeFalse();
        answer.Error.Should().Contain("cache down");
        headers[PurgeCompanyDataWorker.SourceHeader].Should().Be(PurgeCompanyDataWorker.Source);
    }

    [Fact]
    public async Task AnErasureForAnotherCompanyThanTheHeader_DeletesNothing_AndIsReportedAsFailed()
    {
        SeedDevice(CompanyA, "A-1");

        var context = await ConsumeAsync(CompanyA, CompanyPurgeStage.HardDelete, readOnly: false, headerTenant: CompanyB);

        Devices(CompanyA).Should().Be(1);
        Answers(context).Should().ContainSingle().Which.Message.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task TheYearlyPurge_StaysSilent_HrmsApiAnswersForIt()
    {
        SeedDevice(CompanyA, "A-1");

        var context = await ConsumeAsync(CompanyA, CompanyPurgeStage.ArchiveAndPurge, readOnly: true);

        Devices(CompanyA).Should().Be(0);
        Answers(context).Should().BeEmpty();
    }

    [Fact]
    public async Task DestroyingAnArchive_IsNotTheDeviceServicesBusiness()
    {
        SeedDevice(CompanyA, "A-1");

        var context = await ConsumeAsync(CompanyA, CompanyPurgeStage.DestroyArchive, readOnly: true);

        Devices(CompanyA).Should().Be(1);
        Answers(context).Should().BeEmpty();
    }
}
