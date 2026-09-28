using Hrms.adms.Insfrastructure;
using Hrms.adms.Models.DTO;
using Hrms.adms.Models.Entities;
using Hrms.adms.Services;
using Hrms.adms.Services.Auditing;
using MassTransit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Onepunch.AuditEngine;
using AdmsUnitOfWork = Hrms.adms.Services.UnitOfWorkService;
using IAdmsUnitOfWork = Hrms.adms.Services.IUnitOfWorkService;

namespace hrms.test.ServiceTests;

/// <summary>
/// A device's options push used to save a freshly built device over the stored one, wiping what
/// an admin set (placement, status, name). It now only updates what the device reports. Admin
/// edits and command cancellations go through the change tracker, so the audit trail records
/// them; a device acknowledging a command does not show up. Real SQLite: the push path also runs
/// bulk ExecuteUpdate statements.
/// </summary>
public class AdmsDeviceServiceTests : IDisposable
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private readonly SqliteConnection _keepAlive;
    private readonly ServiceProvider _root;

    public AdmsDeviceServiceTests()
    {
        var connectionString = $"Data Source=file:{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _keepAlive = new SqliteConnection(connectionString);
        _keepAlive.Open();

        var tenantProvider = Substitute.For<ITenantProvider>();
        tenantProvider.TenantId.Returns(CompanyId);
        var configuration = new ConfigurationBuilder().Build();

        var services = new ServiceCollection();
        services.AddScoped(_ => tenantProvider);
        services.AddScoped(_ => Substitute.For<IPublishEndpoint>());
        services.AddSingleton<IConfiguration>(configuration);
        services.AddDbContext<AdmsContext>(o => o.UseSqlite(connectionString));
        services.AddScoped<IAdmsUnitOfWork, AdmsUnitOfWork>();
        services.AddScoped<DeviceService>();
        services.AddScoped<CommandService>();
        services.AddAdmsAuditTrail(configuration);
        _root = services.BuildServiceProvider();

        using var scope = _root.CreateScope();
        scope.ServiceProvider.GetRequiredService<AdmsContext>().Database.EnsureCreated();
    }

    public void Dispose()
    {
        _root.Dispose();
        _keepAlive.Dispose();
    }

    private async Task InScopeAsync(Func<IServiceProvider, Task> action)
    {
        using var scope = _root.CreateScope();
        await action(scope.ServiceProvider);
    }

    private async Task<BiometricDevice> DeviceAsync(Guid id)
    {
        using var scope = _root.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AdmsContext>().BiometricDevices.AsNoTracking().SingleAsync(x => x.Id == id);
    }

    private async Task<List<AuditEvent>> EventsAsync()
    {
        using var scope = _root.CreateScope();
        var page = await scope.ServiceProvider.GetRequiredService<IAuditQueryService>()
            .QueryAsync(new AuditQuery { OrganizationId = CompanyId.ToString(), PageSize = 50 });
        return page.Items.OrderBy(x => x.OccurredAtUtc).ToList();
    }

    // Seeded directly (not through the audited context) so each test starts with an empty trail.
    private async Task<Guid> SeedDeviceAsync(string name = "Main Gate")
    {
        var options = new DbContextOptionsBuilder<AdmsContext>().UseSqlite(_keepAlive.ConnectionString).Options;
        var tenantProvider = Substitute.For<ITenantProvider>();
        tenantProvider.TenantId.Returns(CompanyId);
        await using var seed = new AdmsContext(options, tenantProvider);
        var device = new BiometricDevice
        {
            Id = Guid.NewGuid(), TenantId = CompanyId, SN = "SN-001", DeviceName = name, Description = "Lobby",
            BranchId = Guid.NewGuid(), ClientId = Guid.NewGuid(), DepartmentId = Guid.NewGuid(), OperationAreaId = Guid.NewGuid(),
            Status = "Inactive", IpAddress = "10.0.0.1", FwVersion = "6.50",
        };
        seed.BiometricDevices.Add(device);
        await seed.SaveChangesAsync();
        return device.Id;
    }

    private Task PushOptionsAsync(Guid deviceId, string reportedName) =>
        InScopeAsync(sp => sp.GetRequiredService<DeviceService>().UpdateDeviceInfo(new ZkDeviceModel
        {
            DeviceInfo = new BiometricDevice
            {
                Id = deviceId, TenantId = CompanyId, SN = "SN-001", DeviceName = reportedName,
                IpAddress = "10.0.0.9", FwVersion = "6.60", MacAddress = "00:17:61:AA", Platform = "ZMM220",
            },
        }, CancellationToken.None));

    [Fact]
    public async Task OptionsPush_UpdatesWhatTheDeviceReports_AndKeepsWhatTheAdminSet()
    {
        var id = await SeedDeviceAsync();
        var before = await DeviceAsync(id);

        await PushOptionsAsync(id, reportedName: "ZK-Device");

        var after = await DeviceAsync(id);
        after.IpAddress.Should().Be("10.0.0.9");
        after.FwVersion.Should().Be("6.60");
        after.MacAddress.Should().Be("00:17:61:AA");
        after.Platform.Should().Be("ZMM220");
        after.DeviceName.Should().Be("Main Gate", "the admin's name wins over the one the device reports");
        after.Should().BeEquivalentTo(before, o => o
            .Including(d => d.BranchId).Including(d => d.ClientId).Including(d => d.DepartmentId)
            .Including(d => d.OperationAreaId).Including(d => d.Status).Including(d => d.Description));
        (await EventsAsync()).Should().BeEmpty("the device reporting in is not anyone's action");
    }

    [Fact]
    public async Task OptionsPush_FillsInANameTheAdminLeftBlank()
    {
        var id = await SeedDeviceAsync(name: "");

        await PushOptionsAsync(id, reportedName: "ZK-Device");

        (await DeviceAsync(id)).DeviceName.Should().Be("ZK-Device");
    }

    [Fact]
    public async Task AdminEdit_IsSaved_AndRecordedWithBeforeAndAfter()
    {
        var id = await SeedDeviceAsync();
        var newBranch = Guid.NewGuid();

        await InScopeAsync(sp => sp.GetRequiredService<DeviceService>().UpdateStatusAsync(new UpdateBiometricDevice
        {
            Id = id, SN = "SN-001", Status = "Active", Description = "Lobby", BranchId = newBranch,
            ClientId = null, AreaId = null,
        }, CompanyId, CancellationToken.None));

        var after = await DeviceAsync(id);
        after.Status.Should().Be("Active");
        after.BranchId.Should().Be(newBranch);
        var edit = (await EventsAsync()).Should().ContainSingle().Subject;
        edit.EventType.Should().Be(AuditEventTypes.Update);
        edit.EntityType.Should().Be(nameof(BiometricDevice));
        edit.Changes.Should().Contain(c => c.Property == nameof(BiometricDevice.Status) && c.OldValue == "Inactive" && c.NewValue == "Active");
        edit.Changes.Should().Contain(c => c.Property == nameof(BiometricDevice.BranchId) && c.NewValue == newBranch.ToString());
    }

    [Fact]
    public async Task CancellingACommand_IsRecorded_ButADeviceAcknowledgingOneIsNot()
    {
        var cancelled = Guid.NewGuid();
        var acknowledged = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AdmsContext>().UseSqlite(_keepAlive.ConnectionString).Options;
        var tenantProvider = Substitute.For<ITenantProvider>();
        tenantProvider.TenantId.Returns(CompanyId);
        await using (var seed = new AdmsContext(options, tenantProvider))
        {
            seed.DeviceCommands.AddRange(
                new DeviceCommand { Id = cancelled, TenantId = CompanyId, SN = "SN-001", CommandType = "REBOOT", Commands = "REBOOT" },
                new DeviceCommand { Id = acknowledged, TenantId = CompanyId, SN = "SN-001", CommandType = "ENROLL_FP", Commands = "ENROLL_FP PIN=1" });
            await seed.SaveChangesAsync();
        }

        await InScopeAsync(async sp =>
        {
            var commands = sp.GetRequiredService<CommandService>();
            await commands.CancelAsync(cancelled);
            await commands.CommitChangesAsync();
        });
        await InScopeAsync(async sp =>
        {
            var commands = sp.GetRequiredService<CommandService>();
            await commands.DeleteAsync(acknowledged);
            await commands.CommitChangesAsync();
        });

        await InScopeAsync(async sp =>
            (await sp.GetRequiredService<AdmsContext>().DeviceCommands.AnyAsync()).Should().BeFalse("both are gone"));
        var cancel = (await EventsAsync()).Should().ContainSingle().Subject;
        cancel.EventType.Should().Be(AuditEventTypes.Delete);
        cancel.EntityId.Should().Be(cancelled.ToString());
    }

    [Fact]
    public void ReportedInfo_NeverTouchesAdminManagedFields()
    {
        var branch = Guid.NewGuid();
        var device = new BiometricDevice { DeviceName = "Main Gate", BranchId = branch, Status = "Inactive", Description = "Lobby" };

        DeviceService.ApplyReportedInfo(device, new BiometricDevice { DeviceName = "ZK", BranchId = null, Status = "Active", Description = "", IpAddress = "1.2.3.4" });

        device.Should().BeEquivalentTo(new { DeviceName = "Main Gate", BranchId = (Guid?)branch, Status = "Inactive", Description = "Lobby", IpAddress = "1.2.3.4" });
    }
}
