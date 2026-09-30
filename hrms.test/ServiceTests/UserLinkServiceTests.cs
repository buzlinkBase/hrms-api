using Hrms.Core.Services.Invitations;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using hrms.test.TestSupport;

namespace hrms.test.ServiceTests;

/// <summary>
/// The Users page's split into employee and client users: accounts are linked by user id,
/// invites not yet accepted by email, and a client login wins over an employee sharing its email.
/// </summary>
public class UserLinkServiceTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();

    private sealed record Seeded(Guid EmployeeUser, Guid LinkedEmployee, Guid PendingEmployee, Guid ClientUser, Guid ClientId);

    private static async Task<Seeded> SeedAsync(SqliteHrmsContext db)
    {
        await using var seed = db.NewContext(CompanyId);
        var group = new PayrollGroup { Id = Guid.NewGuid(), TenantId = CompanyId };
        var department = new Department { Id = Guid.NewGuid(), TenantId = CompanyId, Code = "OPS", Name = "Operations" };
        var employeeUser = Guid.NewGuid();
        var linked = new Employee { Id = Guid.NewGuid(), TenantId = CompanyId, PayrollGroupId = group.Id, DepartmentId = department.Id, EmployeeNo = "E-001", FirstName = "Ben", LastName = "Cruz", Email = "ben@acme.com", UserId = employeeUser };
        var pending = new Employee { Id = Guid.NewGuid(), TenantId = CompanyId, PayrollGroupId = group.Id, EmployeeNo = "E-002", FirstName = "Ana", LastName = "Reyes", Email = "Ana@Acme.com " };
        // Shares its email with a client login: the client link must win.
        var shared = new Employee { Id = Guid.NewGuid(), TenantId = CompanyId, PayrollGroupId = group.Id, EmployeeNo = "E-003", FirstName = "Cy", LastName = "Tan", Email = "buyer@metro.com" };
        var client = new Client { Id = Guid.NewGuid(), TenantId = CompanyId, Code = "000001", Name = "Metro Retail" };
        var clientUser = Guid.NewGuid();
        seed.PayrollGroups.Add(group);
        seed.Departments.Add(department);
        seed.Employees.AddRange(linked, pending, shared);
        seed.Clients.Add(client);
        seed.ClientUserAccounts.Add(new ClientUserAccount { Id = Guid.NewGuid(), TenantId = CompanyId, ClientId = client.Id, UserId = clientUser, Email = "buyer@metro.com" });
        await seed.SaveChangesAsync();
        return new Seeded(employeeUser, linked.Id, pending.Id, clientUser, client.Id);
    }

    private static async Task<List<UserLinkResult>> LinksAsync(SqliteHrmsContext db, List<Guid> userIds, List<string> emails)
    {
        await using var context = db.NewContext(CompanyId);
        var sut = new UserLinkService(new UnitOfWorkService(context));
        return await sut.GetLinksAsync(new UserLinkRequest { UserIds = userIds, Emails = emails }, default);
    }

    [Fact]
    public async Task AcceptedAccounts_AreLinkedByUserId()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db);

        var result = await LinksAsync(db, [s.EmployeeUser, s.ClientUser], []);

        var employee = result.Single(r => r.UserId == s.EmployeeUser);
        employee.Type.Should().Be(UserLinkType.Employee);
        employee.EmployeeId.Should().Be(s.LinkedEmployee);
        employee.EmployeeNo.Should().Be("E-001");
        employee.DepartmentName.Should().Be("Operations");
        employee.Name.Should().Be("Ben Cruz");

        var client = result.Single(r => r.UserId == s.ClientUser);
        client.Type.Should().Be(UserLinkType.Client);
        client.ClientId.Should().Be(s.ClientId);
        client.ClientName.Should().Be("Metro Retail");
    }

    [Fact]
    public async Task PendingInvites_AreLinkedByEmail_CaseInsensitively_WithoutAUserId()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db);

        var result = await LinksAsync(db, [], ["  ANA@acme.com"]);

        var ana = result.Should().ContainSingle().Subject;
        ana.UserId.Should().BeNull();
        ana.Email.Should().Be("ana@acme.com");
        ana.Type.Should().Be(UserLinkType.Employee);
        ana.EmployeeId.Should().Be(s.PendingEmployee);
    }

    [Fact]
    public async Task ClientLogin_WinsOverAnEmployeeSharingItsEmail()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db);

        var result = await LinksAsync(db, [], ["buyer@metro.com"]);

        var link = result.Should().ContainSingle().Subject;
        link.Type.Should().Be(UserLinkType.Client);
        link.ClientId.Should().Be(s.ClientId);
    }

    [Fact]
    public async Task UnknownMembers_AreNotReturned()
    {
        using var db = new SqliteHrmsContext();
        await SeedAsync(db);

        var result = await LinksAsync(db, [Guid.NewGuid()], ["admin@acme.com"]);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task EmptyRequest_ReturnsNothing()
    {
        using var db = new SqliteHrmsContext();

        var result = await LinksAsync(db, [], []);

        result.Should().BeEmpty();
    }
}
