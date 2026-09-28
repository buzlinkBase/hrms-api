using Hrms.adms.Insfrastructure;
using Hrms.adms.Models.Entities;
using Hrms.adms.Services.Auditing;
using Hrms.Core.Services.Auditing;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Onepunch.AuditEngine;
using Onepunch.AuditEngine.MassTransit;
using AdmsTenantPublishFilter = Hrms.adms.Extensions.TenantPublishFilter<Onepunch.AuditEngine.MassTransit.AuditEventRecorded>;

namespace hrms.test.ServiceTests;

/// <summary>
/// hrms-adms-api's audit trail, registered as the service does (AddAdmsAuditTrail): an admin's
/// device changes, queued commands and template deletions are recorded and forwarded to hrms-api;
/// what the devices themselves report or acknowledge is not.
/// </summary>
public class AdmsAuditTrailTests : IDisposable
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private readonly ServiceProvider _root;
    private readonly IPublishEndpoint _publisher = Substitute.For<IPublishEndpoint>();

    public AdmsAuditTrailTests()
    {
        var tenantProvider = Substitute.For<ITenantProvider>();
        tenantProvider.TenantId.Returns(CompanyId);
        var configuration = new ConfigurationBuilder().Build();
        var databaseName = Guid.NewGuid().ToString();

        var services = new ServiceCollection();
        services.AddScoped(_ => tenantProvider);
        services.AddScoped(_ => _publisher);
        services.AddDbContext<AdmsContext>(o => o.UseInMemoryDatabase(databaseName));
        services.AddAdmsAuditTrail(configuration);
        _root = services.BuildServiceProvider();
    }

    public void Dispose() => _root.Dispose();

    private async Task SaveAsync(Func<AdmsContext, Task> change)
    {
        using var scope = _root.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdmsContext>();
        await change(db);
        await db.SaveChangesAsync();
    }

    private async Task<List<AuditEvent>> EventsAsync()
    {
        using var scope = _root.CreateScope();
        var page = await scope.ServiceProvider.GetRequiredService<IAuditQueryService>()
            .QueryAsync(new AuditQuery { OrganizationId = CompanyId.ToString(), PageSize = 50 });
        return page.Items.OrderBy(x => x.OccurredAtUtc).ToList();
    }

    private List<AuditEventRecorded> Forwarded() =>
        _publisher.ReceivedCalls()
            .Select(c => c.GetArguments().FirstOrDefault())
            .OfType<AuditEventRecorded>()
            .ToList();

    private static BiometricDevice Device(Guid id) => new()
    {
        Id = id, TenantId = CompanyId, SN = "SN-001", DeviceName = "Lobby", Status = "Active",
    };

    [Fact]
    public async Task AdminDeviceChanges_AreRecordedAndForwarded_DeviceHeartbeatsAreNot()
    {
        var id = Guid.NewGuid();
        await SaveAsync(db => { db.BiometricDevices.Add(Device(id)); return Task.CompletedTask; });

        // The device reporting in: state, IP and firmware only.
        await SaveAsync(async db =>
        {
            var device = await db.BiometricDevices.SingleAsync(x => x.Id == id);
            device.State = "Online";
            device.IpAddress = "10.0.0.8";
            device.FwVersion = "6.60";
        });
        // An admin renaming it.
        await SaveAsync(async db => (await db.BiometricDevices.SingleAsync(x => x.Id == id)).DeviceName = "Main Gate");

        var events = await EventsAsync();
        events.Select(e => e.EventType).Should().Equal(AuditEventTypes.Create, AuditEventTypes.Update);
        events[1].Changes.Should().ContainSingle().Which.Should().Be(new AuditChange(nameof(BiometricDevice.DeviceName), "Lobby", "Main Gate"));
        events.Should().OnlyContain(e => e.Source == AdmsAuditRegistration.SourceName && e.OrganizationId == CompanyId.ToString());
        Forwarded().Select(m => m.Id).Should().BeEquivalentTo(events.Select(e => e.Id), "hrms-api keeps the combined read copy");
    }

    [Fact]
    public async Task QueuedCommands_RecordTheCreationWithoutTheirPayload_NotTheDevicesAcknowledgement()
    {
        var id = Guid.NewGuid();
        await SaveAsync(db =>
        {
            db.DeviceCommands.Add(new DeviceCommand
            {
                Id = id, TenantId = CompanyId, SN = "SN-001", CommandType = "ENROLL_FP",
                Commands = "DATA UPDATE BIODATA Pin=12\tTmp=VERY-LONG-TEMPLATE",
            });
            return Task.CompletedTask;
        });
        await SaveAsync(async db => (await db.DeviceCommands.SingleAsync(x => x.Id == id)).UpdatedAt = DateTime.UtcNow);

        var queued = (await EventsAsync()).Should().ContainSingle().Subject;
        queued.EventType.Should().Be(AuditEventTypes.Create);
        queued.EntityType.Should().Be(nameof(DeviceCommand));
        queued.Changes.Should().Contain(c => c.Property == nameof(DeviceCommand.CommandType) && c.NewValue == "ENROLL_FP");
        queued.Changes.Should().NotContain(c => c.Property == nameof(DeviceCommand.Commands), "command text can carry whole biometric templates");
    }

    [Fact]
    public async Task Templates_UploadedByDevicesAreNotRecorded_ButDeletingOneIs()
    {
        var id = Guid.NewGuid();
        await SaveAsync(db =>
        {
            db.BiometricTemplates.Add(new BiometricTemplate { Id = id, TenantId = CompanyId, SN = "SN-001", BioId = 12, TemplateData = "TEMPLATE" });
            return Task.CompletedTask;
        });
        (await EventsAsync()).Should().BeEmpty();

        await SaveAsync(async db => db.BiometricTemplates.Remove(await db.BiometricTemplates.SingleAsync(x => x.Id == id)));

        var deleted = (await EventsAsync()).Should().ContainSingle().Subject;
        deleted.EventType.Should().Be(AuditEventTypes.Delete);
        deleted.EntityId.Should().Be(id.ToString());
    }

    [Fact]
    public void AuditScreen_LabelsTheDeviceRecordTypes()
    {
        AuditLogPresenter.ModuleLabel(nameof(BiometricDevice)).Should().Be("Biometric Device");
        AuditLogPresenter.ModuleLabel(nameof(DeviceCommand)).Should().Be("Device Command");
        AuditLogPresenter.ModuleLabel(nameof(BiometricTemplate)).Should().Be("Biometric Template");
    }

    [Fact]
    public async Task PublishFilter_KeepsTheCompanyAForwardedEventAlreadyCarries()
    {
        var tenantProvider = Substitute.For<ITenantProvider>();
        tenantProvider.TenantId.Returns(Guid.Empty); // e.g. publishing from a background consumer
        var company = Guid.NewGuid().ToString();
        var headers = Substitute.For<SendHeaders>();
        headers.TryGetHeader("X-Tenant-ID", out Arg.Any<object?>())
            .Returns(call => { call[1] = company; return true; });
        var context = Substitute.For<PublishContext<AuditEventRecorded>>();
        context.Headers.Returns(headers);

        await new AdmsTenantPublishFilter(tenantProvider).Send(context, Substitute.For<IPipe<PublishContext<AuditEventRecorded>>>());

        headers.DidNotReceive().Set(Arg.Any<string>(), Arg.Any<string>());
    }
}
