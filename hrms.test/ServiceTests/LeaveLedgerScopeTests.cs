using Hrms.Domain.Entities.EmployeeEntities;
using hrms.test.TestSupport;
using Mapster;
using MapsterMapper;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>
/// Leave Ledger report: every filter is optional. With none set it returns everyone (as it always
/// did); each one that is set narrows the report to that client / area / payroll group /
/// department / branch, and several together all have to match.
/// </summary>
public class LeaveLedgerScopeTests
{
    private static readonly Guid ClientA = Guid.NewGuid();
    private static readonly Guid ClientB = Guid.NewGuid();
    private static readonly Guid AreaX = Guid.NewGuid();
    private static readonly Guid AreaY = Guid.NewGuid();

    // Four employees spread over two clients / areas, and two payroll groups.
    private sealed record Seeded(Guid GroupOne, Guid GroupTwo, Guid Ana, Guid Ben, Guid Cara, Guid Dan);

    private static async Task<Seeded> SeedAsync(SqliteHrmsContext db)
    {
        await using var seed = db.NewContext();
        var groupOne = new PayrollGroup { Id = Guid.NewGuid() };
        var groupTwo = new PayrollGroup { Id = Guid.NewGuid() };
        seed.PayrollGroups.AddRange(groupOne, groupTwo);
        seed.Clients.AddRange(
            new Client { Id = ClientA, Name = "Client A" },
            new Client { Id = ClientB, Name = "Client B" });
        seed.Areas.AddRange(
            new CostCenters { Id = AreaX, Name = "Area X" },
            new CostCenters { Id = AreaY, Name = "Area Y" });

        Employee Emp(string name, Guid group, Guid? client, Guid? area) => new()
        {
            Id = Guid.NewGuid(), PayrollGroupId = group, ClientId = client, AreaId = area,
            FirstName = name, LastName = name, EmployeeNo = name,
        };
        var ana = Emp("Ana", groupOne.Id, ClientA, AreaX);
        var ben = Emp("Ben", groupOne.Id, ClientB, AreaY);
        var cara = Emp("Cara", groupTwo.Id, ClientA, AreaY);
        var dan = Emp("Dan", groupTwo.Id, null, null);
        seed.Employees.AddRange(ana, ben, cara, dan);

        var leave = new Leave { Id = Guid.NewGuid(), Code = "VL", Description = "Vacation Leave" };
        seed.Leaves.Add(leave);
        foreach (var e in new[] { ana, ben, cara, dan })
        {
            seed.LeaveCredits.Add(new LeaveCredits
            {
                Id = Guid.NewGuid(), EmployeeId = e.Id, LeaveId = leave.Id, PeriodYear = 2026,
                FromDate = new DateTime(2026, 1, 1), ToDate = new DateTime(2026, 12, 31),
                Granted = 10m, Balance = 10m,
            });
        }
        await seed.SaveChangesAsync();
        return new Seeded(groupOne.Id, groupTwo.Id, ana.Id, ben.Id, cara.Id, dan.Id);
    }

    private static async Task<List<Guid>> LedgerAsync(SqliteHrmsContext db, EmployeeScopeFilter? scope)
    {
        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var employees = new EmployeeService(uow, Substitute.For<IMapper>(), new TypeAdapterConfig(), null!, null!, null!, null!, null!, null!);
        var rows = await new PayrollReportService(uow, employees, null!).GetLeaveLedgerAsync(2026, scope, CancellationToken.None);
        return rows.Select(x => x.EmployeeId).ToList();
    }

    [Fact]
    public async Task WithNoFilter_ReturnsEveryone()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db);

        (await LedgerAsync(db, null)).Should().BeEquivalentTo([s.Ana, s.Ben, s.Cara, s.Dan]);
        (await LedgerAsync(db, new EmployeeScopeFilter())).Should().BeEquivalentTo([s.Ana, s.Ben, s.Cara, s.Dan]);
    }

    [Fact]
    public async Task TheOriginalTwoArgumentOverload_StillReturnsEveryone()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db);
        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var employees = new EmployeeService(uow, Substitute.For<IMapper>(), new TypeAdapterConfig(), null!, null!, null!, null!, null!, null!);

        var rows = await new PayrollReportService(uow, employees, null!).GetLeaveLedgerAsync(2026, CancellationToken.None);

        rows.Select(x => x.EmployeeId).Should().BeEquivalentTo([s.Ana, s.Ben, s.Cara, s.Dan]);
    }

    [Fact]
    public async Task ByClient_ReturnsOnlyThatClientsEmployees()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db);

        (await LedgerAsync(db, new EmployeeScopeFilter { ClientId = ClientA })).Should().BeEquivalentTo([s.Ana, s.Cara]);
    }

    [Fact]
    public async Task ByArea_ReturnsOnlyThatAreasEmployees()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db);

        (await LedgerAsync(db, new EmployeeScopeFilter { AreaId = AreaY })).Should().BeEquivalentTo([s.Ben, s.Cara]);
    }

    [Fact]
    public async Task ByPayrollGroup_ReturnsOnlyThatGroupsEmployees()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db);

        (await LedgerAsync(db, new EmployeeScopeFilter { PayrollGroupId = s.GroupTwo })).Should().BeEquivalentTo([s.Cara, s.Dan]);
    }

    [Fact]
    public async Task SeveralFilters_AllHaveToMatch()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db);

        var scope = new EmployeeScopeFilter { ClientId = ClientA, AreaId = AreaY };

        (await LedgerAsync(db, scope)).Should().BeEquivalentTo([s.Cara]);
    }

    [Fact]
    public async Task AFilterNobodyMatches_ReturnsAnEmptyLedger()
    {
        using var db = new SqliteHrmsContext();
        await SeedAsync(db);

        (await LedgerAsync(db, new EmployeeScopeFilter { ClientId = Guid.NewGuid() })).Should().BeEmpty();
    }
}
