using Hrms.Api.Controllers;
using Hrms.Core.Messaging;
using Hrms.Core.Services.Auditing;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Infrastructure;
using hrms.test.TestSupport;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Onepunch.AuditEngine;
using Onepunch.AuditEngine.MassTransit;
using Onepunch.Common.Lib;

namespace hrms.test.ServiceTests;

/// <summary>
/// hrms-api's audit trail end to end on a real (SQLite) HrmsContext, registered exactly as the
/// API does (AddHrmsAuditTrail): automatic capture with masking, the read copy of other
/// services' events, and the Security > Audit endpoint's company scoping and contract.
/// </summary>
public class AuditTrailTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();

    private static ServiceProvider BuildHost(SqliteHrmsContext db, Guid companyId)
    {
        var tenantProvider = Substitute.For<ITenantProvider>();
        tenantProvider.TenantId.Returns(companyId);
        var configuration = new ConfigurationBuilder().Build();

        var services = new ServiceCollection();
        services.AddScoped(_ => tenantProvider);
        services.AddScoped(_ => new TenantConnectionStringInfo());
        services.AddSingleton<IConfiguration>(configuration);
        services.AddDbContext<HrmsContext>(o => o.UseSqlite(db.ConnectionString, x => x.UseNetTopologySuite()));
        services.AddScoped<IUnitOfWorkService, UnitOfWorkService>();
        services.AddHrmsAuditTrail(configuration);
        return services.BuildServiceProvider();
    }

    private static async Task<Guid> SeedEmployeeAsync(SqliteHrmsContext db)
    {
        await using var seed = db.NewContext(CompanyId);
        var payrollGroup = new PayrollGroup { Id = Guid.NewGuid(), TenantId = CompanyId };
        var employee = new Employee
        {
            Id = Guid.NewGuid(), TenantId = CompanyId, PayrollGroupId = payrollGroup.Id,
            FirstName = "Ana", LastName = "Reyes", TIN = "123-456-789",
        };
        seed.PayrollGroups.Add(payrollGroup);
        seed.Employees.Add(employee);
        await seed.SaveChangesAsync();
        return employee.Id;
    }

    private static async Task<List<AuditEvent>> EventsAsync(ServiceProvider host)
    {
        using var scope = host.CreateScope();
        var page = await scope.ServiceProvider.GetRequiredService<IAuditQueryService>()
            .QueryAsync(new AuditQuery { OrganizationId = CompanyId.ToString(), PageSize = 50 });
        return page.Items.ToList();
    }

    [Fact]
    public async Task EditingAnEmployee_IsRecordedInTheSameCommit_WithGovernmentIdsMasked()
    {
        using var db = new SqliteHrmsContext();
        var employeeId = await SeedEmployeeAsync(db);
        using var host = BuildHost(db, CompanyId);

        using (var scope = host.CreateScope())
        {
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkService>();
            var employee = await uow.Context.Employees.SingleAsync(x => x.Id == employeeId);
            employee.LastName = "Santos";
            employee.TIN = "987-654-321";
            await uow.CommitChangesAsync("", CancellationToken.None);
        }

        var update = (await EventsAsync(host)).Should().ContainSingle().Subject;
        update.EventType.Should().Be(AuditEventTypes.Update);
        update.EntityType.Should().Be(nameof(Employee));
        update.EntityId.Should().Be(employeeId.ToString());
        update.Source.Should().Be(HrmsAuditRegistration.SourceName);
        update.Changes.Should().BeEquivalentTo(
        [
            new AuditChange(nameof(Employee.LastName), "Reyes", "Santos"),
            new AuditChange(nameof(Employee.TIN), "***", "***"),
        ]);
    }

    [Fact]
    public async Task AnUncommittedChange_LeavesNoAuditRecord()
    {
        using var db = new SqliteHrmsContext();
        var employeeId = await SeedEmployeeAsync(db);
        using var host = BuildHost(db, CompanyId);

        using (var scope = host.CreateScope())
        {
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkService>();
            var employee = await uow.Context.Employees.SingleAsync(x => x.Id == employeeId);
            employee.LastName = "Santos";
            await uow.Context.SaveChangesAsync(); // saved inside the unit's transaction, never committed
        }

        (await EventsAsync(host)).Should().BeEmpty("the audit row rolls back with the change it describes");
    }

    // ---- read copy of AuthApi / TenantApi events ----

    private static AuditEventRecorded SignIn(string source = "auth-api") => new()
    {
        Id = Guid.NewGuid(),
        OccurredAtUtc = new DateTime(2026, 9, 28, 1, 0, 0, DateTimeKind.Utc),
        EventType = AuditEventTypes.Login,
        EntityType = "User",
        OrganizationId = CompanyId.ToString(),
        ActorId = "user-1",
        ActorDisplayName = "ana@acme.com",
        Source = source,
        Hash = "HASH-FROM-AUTH",
    };

    private static async Task ConsumeAsync(ServiceProvider host, AuditEventRecorded message)
    {
        using var scope = host.CreateScope();
        var worker = new AuditReplicaWorker(
            scope.ServiceProvider.GetRequiredService<IAuditReplicaWriter>(),
            scope.ServiceProvider.GetRequiredService<IUnitOfWorkService>());
        var context = Substitute.For<ConsumeContext<AuditEventRecorded>>();
        context.Message.Returns(message);
        await worker.Consume(context);
    }

    [Fact]
    public async Task ReplicaWorker_StoresOtherServicesEventsOnce_AsReceived()
    {
        using var db = new SqliteHrmsContext();
        using var host = BuildHost(db, CompanyId);
        var signIn = SignIn();

        await ConsumeAsync(host, signIn);
        await ConsumeAsync(host, signIn); // redelivery

        var stored = (await EventsAsync(host)).Should().ContainSingle().Subject;
        stored.Id.Should().Be(signIn.Id);
        stored.Hash.Should().Be("HASH-FROM-AUTH", "the sender's chain is kept intact");
        stored.Source.Should().Be("auth-api");
    }

    [Fact]
    public async Task ReplicaWorker_IgnoresHrmsApisOwnEvents()
    {
        using var db = new SqliteHrmsContext();
        using var host = BuildHost(db, CompanyId);

        await ConsumeAsync(host, SignIn(source: HrmsAuditRegistration.SourceName));

        (await EventsAsync(host)).Should().BeEmpty();
    }

    // ---- Security > Audit endpoint ----

    [Fact]
    public async Task Endpoint_ListsOnlyTheCallersCompany_InTheFrontendsShape()
    {
        using var db = new SqliteHrmsContext();
        using var host = BuildHost(db, CompanyId);
        await ConsumeAsync(host, SignIn());
        var otherCompany = SignIn() with { OrganizationId = Guid.NewGuid().ToString() };
        await ConsumeAsync(host, otherCompany);

        using var scope = host.CreateScope();
        var controller = new AuditLogsController(
            scope.ServiceProvider.GetRequiredService<IAuditQueryService>(),
            scope.ServiceProvider.GetRequiredService<ITenantProvider>());

        var result = await controller.Get(new AuditLogQuery { Action = "login", Status = "SUCCESS" }, CancellationToken.None);

        var page = ((OkObjectResult)result).Value.Should().BeOfType<AuditLogPage>().Subject;
        page.Total.Should().Be(1);
        var row = page.Items.Single();
        row.Action.Should().Be("LOGIN");
        row.Status.Should().Be("SUCCESS");
        row.Module.Should().Be("Account");
        row.UserName.Should().Be("ana@acme.com");
        row.Timestamp.Should().Be("2026-09-28T01:00:00.0000000Z");

        (await controller.Get(Guid.Parse(otherCompany.Id.ToString()), CancellationToken.None))
            .Should().BeOfType<NotFoundResult>("another company's event is invisible, even by id");
    }
}

/// <summary>The Security > Audit query contract, mapped onto the engine's filters.</summary>
public class AuditLogPresenterTests
{
    [Fact]
    public void Query_MapsTheScreensFilters_AndIsAlwaysScopedToTheCompany()
    {
        var from = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.FromHours(8));

        var query = AuditLogPresenter.ToEngineQuery(new AuditLogQuery
        {
            Action = "APPROVE",
            Status = "FAILED",
            Module = "Leave",
            UserId = " user-1 ",
            From = from,
            Search = "  ",
            Page = 3,
            PageSize = 50,
        }, "company-1");

        query.OrganizationId.Should().Be("company-1");
        query.EventType.Should().Be(AuditEventTypes.Approve);
        query.Outcome.Should().Be(AuditOutcome.Failure);
        query.EntityType.Should().Be("Leave");
        query.ActorId.Should().Be("user-1");
        query.FromUtc.Should().Be(new DateTime(2026, 8, 31, 16, 0, 0), "local dates arrive as instants and are compared in UTC");
        query.Search.Should().BeNull();
        (query.Page, query.PageSize).Should().Be((3, 50));
    }

    [Fact]
    public void UnknownActionOrStatus_FiltersNothing()
    {
        var query = AuditLogPresenter.ToEngineQuery(new AuditLogQuery { Action = "DANCE", Status = "MAYBE" }, "c");

        query.EventType.Should().BeNull();
        query.Outcome.Should().BeNull();
    }

    [Fact]
    public void Response_ShowsLabels_RequestContext_AndChanges()
    {
        var row = AuditLogPresenter.ToResponse(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = new DateTime(2026, 9, 28, 1, 2, 3),
            EventType = AuditEventTypes.Reject,
            EntityType = "OfficialBusiness",
            EntityId = "ob-1",
            Actor = AuditActor.User("u-1", "admin@acme.com"),
            Outcome = AuditOutcome.Failure,
            Metadata = new Dictionary<string, string?> { ["IpAddress"] = "203.0.113.7", ["UserAgent"] = "Mozilla/5.0" },
            Changes = [new AuditChange("Status", "ForApproval", "Declined")],
        });

        row.Action.Should().Be("REJECT");
        row.Module.Should().Be("Official Business");
        row.Status.Should().Be("FAILED");
        row.IpAddress.Should().Be("203.0.113.7");
        row.UserAgent.Should().Be("Mozilla/5.0");
        row.Timestamp.Should().Be("2026-09-28T01:02:03.0000000Z");
        row.Changes.Should().ContainSingle().Which.Should().Be(new AuditLogChange("Status", "ForApproval", "Declined"));
    }

    [Fact]
    public void UnlistedEntityTypes_StillGetAReadableLabel()
    {
        AuditLogPresenter.ModuleLabel("SalaryAdjustment").Should().Be("Salary Adjustment");
        AuditLogPresenter.ModuleLabel(null).Should().Be("System");
    }
}
