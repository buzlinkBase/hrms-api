using Hrms.Core.Messaging;
using Hrms.Core.Services.Approvals;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Infrastructure;
using hrms.test.TestSupport;
using Mapster;
using MapsterMapper;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Onepunch.Common.Lib;
using Onepunch.Common.Lib.DTO;

namespace hrms.test.ServiceTests;

/// <summary>
/// Accounts were never linked to their employee (UserOnboardedWorker's guard skipped every
/// not-yet-linked employee), so pushes, department/position approver lookups and anything keyed
/// on Employee.UserId missed everyone matched by email. Invitations now link, and an account
/// matched by email is linked the first time it's resolved. Also: revoking an approved pass slip
/// now tells the applicant.
/// </summary>
public class AccountLinkAndRevokeTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();

    private static async Task<Employee> SeedEmployeeAsync(SqliteHrmsContext db, string email, Guid? userId = null)
    {
        await using var seed = db.NewContext(CompanyId);
        var group = new PayrollGroup { Id = Guid.NewGuid(), TenantId = CompanyId };
        var employee = new Employee
        {
            Id = Guid.NewGuid(), TenantId = CompanyId, PayrollGroupId = group.Id,
            FirstName = "Ana", LastName = "Reyes", Email = email, UserId = userId,
        };
        seed.PayrollGroups.Add(group);
        seed.Employees.Add(employee);
        await seed.SaveChangesAsync();
        return employee;
    }

    private static async Task<Guid?> UserIdOfAsync(SqliteHrmsContext db, Guid employeeId)
    {
        await using var verify = db.NewContext(CompanyId);
        return (await verify.Employees.AsNoTracking().SingleAsync(x => x.Id == employeeId)).UserId;
    }

    private static EmployeeService EmployeeServiceFor(IUnitOfWorkService uow, EmployeeAccountLinkService? links = null) =>
        new(uow, Substitute.For<IMapper>(), new TypeAdapterConfig(), null!, null!, null!, null!, null!, null!, links);

    // ---- invitation acceptance (UserOnboardedWorker) ----

    private static async Task OnboardAsync(SqliteHrmsContext db, Guid userId, string email, Guid? employeeId = null)
    {
        await using var context = db.NewContext(CompanyId);
        var uow = new UnitOfWorkService(context);
        var worker = new UserOnboardedWorker(EmployeeServiceFor(uow), new Hrms.Core.Services.Invitations.InviteMatchService(uow));
        var consume = Substitute.For<ConsumeContext<UserOnboarded>>();
        consume.Message.Returns(new UserOnboarded { UserId = userId, TenantId = CompanyId, Email = email, EmployeeId = employeeId });
        await worker.Consume(consume);
    }

    [Fact]
    public async Task AcceptingAnInvitation_LinksTheEmployeeToTheAccount()
    {
        using var db = new SqliteHrmsContext();
        var employee = await SeedEmployeeAsync(db, "ana@acme.com");
        var userId = Guid.NewGuid();

        await OnboardAsync(db, userId, "ana@acme.com", employee.Id);

        (await UserIdOfAsync(db, employee.Id)).Should().Be(userId);
    }

    [Fact]
    public async Task AcceptingAnInvitation_ByEmailOnly_AlsoLinks()
    {
        using var db = new SqliteHrmsContext();
        var employee = await SeedEmployeeAsync(db, "ana@acme.com");
        var userId = Guid.NewGuid();

        await OnboardAsync(db, userId, "ana@acme.com");

        (await UserIdOfAsync(db, employee.Id)).Should().Be(userId);
    }

    [Fact]
    public async Task AnEmployeeLinkedToAnotherAccount_IsLeftAlone()
    {
        using var db = new SqliteHrmsContext();
        var original = Guid.NewGuid();
        var employee = await SeedEmployeeAsync(db, "ana@acme.com", original);

        await OnboardAsync(db, Guid.NewGuid(), "ana@acme.com", employee.Id);

        (await UserIdOfAsync(db, employee.Id)).Should().Be(original);
    }

    // ---- healing accounts matched by email (ResolveEmployeeIdAsync) ----

    private static ServiceProvider BuildHost(SqliteHrmsContext db)
    {
        var tenantProvider = Substitute.For<ITenantProvider>();
        tenantProvider.TenantId.Returns(CompanyId);
        var services = new ServiceCollection();
        services.AddScoped(_ => tenantProvider);
        services.AddScoped<TenantConnectionStringInfo>();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddDbContext<HrmsContext>(o => o.UseSqlite(db.ConnectionString, x => x.UseNetTopologySuite()));
        services.AddScoped<IUnitOfWorkService, UnitOfWorkService>();
        services.AddSingleton<EmployeeAccountLinkQueue>();
        services.AddScoped<EmployeeAccountLinkService>();
        services.AddSingleton<EmployeeAccountLinkWorker>();
        return services.BuildServiceProvider();
    }

    // Resolves inside a request-like scope whose unit of work is never committed, then lets the
    // worker drain the queue the way the hosted service does.
    private static async Task<Guid?> ResolveAsync(ServiceProvider host, Guid userId, string email)
    {
        Guid? resolved;
        using (var scope = host.CreateScope())
        {
            var service = EmployeeServiceFor(
                scope.ServiceProvider.GetRequiredService<IUnitOfWorkService>(),
                scope.ServiceProvider.GetRequiredService<EmployeeAccountLinkService>());
            resolved = await service.ResolveEmployeeIdAsync(userId, email, CancellationToken.None);
        }

        var queue = host.GetRequiredService<EmployeeAccountLinkQueue>();
        var worker = host.GetRequiredService<EmployeeAccountLinkWorker>();
        while (queue.Reader.TryRead(out var link))
            await worker.LinkAsync(link, CancellationToken.None);
        return resolved;
    }

    [Fact]
    public async Task AnAccountMatchedByEmail_IsLinkedTheFirstTimeItIsResolved()
    {
        using var db = new SqliteHrmsContext();
        var employee = await SeedEmployeeAsync(db, "ana@acme.com");
        using var host = BuildHost(db);
        var userId = Guid.NewGuid();

        (await ResolveAsync(host, userId, "ana@acme.com")).Should().Be(employee.Id);

        (await UserIdOfAsync(db, employee.Id)).Should().Be(userId,
            "the link is committed on its own, even though the resolving request never commits");
    }

    [Fact]
    public async Task ResolvingAnAlreadyLinkedEmployee_QueuesNothing()
    {
        using var db = new SqliteHrmsContext();
        var userId = Guid.NewGuid();
        await SeedEmployeeAsync(db, "ana@acme.com", userId);
        using var host = BuildHost(db);

        using var scope = host.CreateScope();
        await EmployeeServiceFor(
                scope.ServiceProvider.GetRequiredService<IUnitOfWorkService>(),
                scope.ServiceProvider.GetRequiredService<EmployeeAccountLinkService>())
            .ResolveEmployeeIdAsync(userId, "ana@acme.com", CancellationToken.None);

        host.GetRequiredService<EmployeeAccountLinkQueue>().Reader.TryRead(out _).Should().BeFalse();
    }

    [Fact]
    public async Task TheAccountsOwnLinkedEmployee_WinsOverAnEmailMatch()
    {
        using var db = new SqliteHrmsContext();
        var userId = Guid.NewGuid();
        var linked = await SeedEmployeeAsync(db, "ana.reyes@acme.com", userId);
        var emailOnly = await SeedEmployeeAsync(db, "ana@acme.com");
        using var host = BuildHost(db);

        (await ResolveAsync(host, userId, "ana@acme.com")).Should().Be(linked.Id);

        (await UserIdOfAsync(db, emailOnly.Id)).Should().BeNull("an account is never linked to a second employee");
    }

    // ---- revoking an approved pass slip ----

    [Fact]
    public async Task RevokingAPassSlip_TellsTheApplicant()
    {
        using var db = new SqliteHrmsContext();
        var applicant = await SeedEmployeeAsync(db, "ana@acme.com");
        var slipId = Guid.NewGuid();
        await using (var seed = db.NewContext(CompanyId))
        {
            seed.PassSlipApplications.Add(new PassSlipApplication
            {
                Id = slipId, TenantId = CompanyId, EmployeeId = applicant.Id,
                ApplicationDate = new DateOnly(2026, 9, 3), ApprovalStatus = ApprovalStatus.Approved,
            });
            await seed.SaveChangesAsync();
        }

        var publisher = Substitute.For<IPublishEndpoint>();
        await using (var context = db.NewContext(CompanyId))
        {
            var uow = new UnitOfWorkService(context);
            await new PassSlipApplicationService(uow, null!, new ApprovalEngineService(uow, publisher))
                .RevokeAsync(slipId, CancellationToken.None);
        }

        await using (var verify = db.NewContext(CompanyId))
            (await verify.PassSlipApplications.SingleAsync(x => x.Id == slipId)).ApprovalStatus.Should().Be(ApprovalStatus.Cancelled);
        await publisher.Received(1).Publish(
            Arg.Is<ApprovalNotificationRequested>(m =>
                m.StatusLabel == ApprovalEngineService.CancelledStatusLabel &&
                m.RecipientEmail == "ana@acme.com" &&
                m.ApplicationId == slipId &&
                m.DeliverPush &&
                m.DeliverEmail),
            Arg.Any<CancellationToken>());
    }
}
