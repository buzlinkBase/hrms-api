using Hrms.Core.Messaging;
using Hrms.Core.Services.Invitations;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using hrms.test.TestSupport;
using Mapster;
using MapsterMapper;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Onepunch.Common.Lib.DTO;

namespace hrms.test.ServiceTests;

/// <summary>
/// Batch invite matching: each employee invite is tied to the employee its email belongs to, a
/// client login can't reuse an employee's email (or vice versa), and nobody already linked to an
/// account is invited again. Accepting a client invite records the login under its client.
/// </summary>
public class InviteMatchServiceTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();

    private sealed record Seeded(Guid Unlinked, Guid Linked, Guid ClientId);

    private static async Task<Seeded> SeedAsync(SqliteHrmsContext db)
    {
        await using var seed = db.NewContext(CompanyId);
        var group = new PayrollGroup { Id = Guid.NewGuid(), TenantId = CompanyId };
        var unlinked = new Employee { Id = Guid.NewGuid(), TenantId = CompanyId, PayrollGroupId = group.Id, FirstName = "Ana", LastName = "Reyes", Email = "Ana@Acme.com " };
        var linked = new Employee { Id = Guid.NewGuid(), TenantId = CompanyId, PayrollGroupId = group.Id, FirstName = "Ben", LastName = "Cruz", Email = "ben@acme.com", UserId = Guid.NewGuid() };
        var client = new Client { Id = Guid.NewGuid(), TenantId = CompanyId, Code = "000001", Name = "Metro Retail" };
        seed.PayrollGroups.Add(group);
        seed.Employees.AddRange(unlinked, linked);
        seed.Clients.Add(client);
        seed.ClientUserAccounts.Add(new ClientUserAccount { Id = Guid.NewGuid(), TenantId = CompanyId, ClientId = client.Id, UserId = Guid.NewGuid(), Email = "buyer@metro.com" });
        await seed.SaveChangesAsync();
        return new Seeded(unlinked.Id, linked.Id, client.Id);
    }

    [Fact]
    public async Task EmployeeBatch_MatchesEachEmailToItsEmployee()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db);
        await using var context = db.NewContext(CompanyId);
        var sut = new InviteMatchService(new UnitOfWorkService(context));

        var result = await sut.MatchEmployeesAsync(["ANA@acme.com", "ben@acme.com", "nobody@acme.com", "buyer@metro.com"], default);

        var ana = result.Single(r => r.Email == "ana@acme.com");
        ana.Status.Should().Be(InviteMatchStatus.Matched);
        ana.EmployeeId.Should().Be(s.Unlinked);
        ana.Name.Should().Be("Ana Reyes");
        result.Single(r => r.Email == "ben@acme.com").Status.Should().Be(InviteMatchStatus.AlreadyLinked);
        result.Single(r => r.Email == "nobody@acme.com").Status.Should().Be(InviteMatchStatus.NoEmployee);
        result.Single(r => r.Email == "buyer@metro.com").Status.Should().Be(InviteMatchStatus.ClientLogin);
    }

    [Fact]
    public async Task ClientBatch_RefusesEmployeeEmails_AndExistingLogins()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db);
        await using var context = db.NewContext(CompanyId);
        var sut = new InviteMatchService(new UnitOfWorkService(context));

        var result = await sut.MatchClientLoginsAsync(s.ClientId, ["new@metro.com", "ana@acme.com", "buyer@metro.com"], default);

        result.Single(r => r.Email == "new@metro.com").Status.Should().Be(InviteMatchStatus.Ok);
        var employee = result.Single(r => r.Email == "ana@acme.com");
        employee.Status.Should().Be(InviteMatchStatus.EmployeeEmail);
        employee.Name.Should().Be("Ana Reyes");
        result.Single(r => r.Email == "buyer@metro.com").Status.Should().Be(InviteMatchStatus.AlreadyLinked);
    }

    [Fact]
    public async Task AcceptingAClientInvite_RecordsTheLoginUnderItsClient_NotAnEmployee()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db);
        var userId = Guid.NewGuid();

        await using (var context = db.NewContext(CompanyId))
        {
            var uow = new UnitOfWorkService(context);
            var worker = new UserOnboardedWorker(
                new EmployeeService(uow, Substitute.For<IMapper>(), new TypeAdapterConfig(), null!, null!, null!, null!, null!, null!),
                new InviteMatchService(uow));
            var consume = Substitute.For<ConsumeContext<UserOnboarded>>();
            // Same email as an employee: a client invite must still not link that employee.
            consume.Message.Returns(new UserOnboarded { UserId = userId, TenantId = CompanyId, Email = "Ana@acme.com", ClientId = s.ClientId });
            await worker.Consume(consume);
            await worker.Consume(consume); // replayed message: idempotent
        }

        // No company filter: the SQLite test context doesn't stamp TenantId on new rows the way
        // the real interceptor does.
        await using var verify = db.NewContext();
        var login = await verify.ClientUserAccounts.SingleAsync(x => x.UserId == userId);
        login.ClientId.Should().Be(s.ClientId);
        login.Email.Should().Be("ana@acme.com");
        (await verify.Employees.SingleAsync(x => x.Id == s.Unlinked)).UserId.Should().BeNull();
    }
}
