using Hrms.Core;
using Hrms.Core.Services;
using Hrms.Domain;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Domain.ValueObjects;
using Hrms.Infrastructure;
using hrms.test.TestSupport;
using Mapster;
using MapsterMapper;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>
/// EmployeeService.SearchAsync (GET employees/list) -- the Setup → Employee table's server-side
/// paging, sorting and column filters. Runs against SQLite so the EmployeeListQueryBuilder
/// clauses are proven to translate to real SQL, not just evaluate in memory.
/// </summary>
public class EmployeeSearchTests
{
    private static readonly Guid ClientA = Guid.NewGuid();
    private static readonly Guid ClientB = Guid.NewGuid();
    private static readonly Guid GroupSemi = Guid.NewGuid();
    private static readonly Guid GroupWeekly = Guid.NewGuid();
    private static readonly Guid SiteNorth = Guid.NewGuid();

    private static EmployeeService BuildService(IUnitOfWorkService uow)
    {
        TypeAdapterConfig.GlobalSettings.Scan(typeof(MappingProfile).Assembly);
        return new EmployeeService(
            uow,
            Substitute.For<IMapper>(),
            TypeAdapterConfig.GlobalSettings,
            new DepartmentService(uow),
            new PayrollGroupService(uow),
            new BranchService(uow),
            new CostCenterService(uow),
            new PositionService(uow),
            new SectionService(uow));
    }

    private static Employee NewEmployee(string last, string first, Guid payrollGroupId, Guid? clientId = null,
        Guid? areaId = null, int? bioId = null, string sss = "", string tin = "") => new()
    {
        Id = Guid.NewGuid(),
        LastName = last,
        FirstName = first,
        EmployeeNo = $"E-{last}",
        PayrollGroupId = payrollGroupId,
        ClientId = clientId,
        AreaId = areaId,
        BioId = bioId,
        SSSNo = sss,
        TIN = tin,
    };

    private static async Task<SqliteHrmsContext> SeedAsync()
    {
        var db = new SqliteHrmsContext();
        await using var seed = db.NewContext();
        seed.Clients.AddRange(
            new Client { Id = ClientA, Name = "Acme Security" },
            new Client { Id = ClientB, Name = "Zenith Foods" });
        seed.PayrollGroups.AddRange(
            new PayrollGroup { Id = GroupSemi, Name = "Semi-Monthly" },
            new PayrollGroup { Id = GroupWeekly, Name = "Weekly" });
        seed.Areas.Add(new CostCenters { Id = SiteNorth, Name = "North Site" });
        seed.Employees.AddRange(
            NewEmployee("Abella", "Jerome", GroupSemi, ClientA, SiteNorth, bioId: 1001, sss: "34-1234567-8", tin: "123-456-789"),
            NewEmployee("Bautista", "Ana", GroupSemi, ClientB, bioId: 1002, sss: "34-9999999-1"),
            NewEmployee("Cruz", "Ben", GroupWeekly, ClientA, SiteNorth, bioId: 2001),
            NewEmployee("Dela Rosa", "Carla", GroupWeekly, null, bioId: 2002),
            NewEmployee("Estrada", "Dan", GroupSemi, ClientB, tin: "999-888-777"));
        await seed.SaveChangesAsync();
        return db;
    }

    private static async Task<List<string>> LastNamesAsync(SqliteHrmsContext db, EmployeeListQuery query)
    {
        await using var context = db.NewContext();
        var result = await BuildService(new UnitOfWorkService(context)).SearchAsync(query, CancellationToken.None);
        return result.Data!.Select(x => x.LastName).ToList();
    }

    [Fact]
    public async Task Filters_ByClientPayrollGroupAndProjectSite()
    {
        using var db = await SeedAsync();

        (await LastNamesAsync(db, new EmployeeListQuery { ClientIds = [ClientA] }))
            .Should().BeEquivalentTo(["Abella", "Cruz"]);
        (await LastNamesAsync(db, new EmployeeListQuery { PayrollGroupIds = [GroupWeekly] }))
            .Should().BeEquivalentTo(["Cruz", "Dela Rosa"]);
        (await LastNamesAsync(db, new EmployeeListQuery { AreaIds = [SiteNorth] }))
            .Should().BeEquivalentTo(["Abella", "Cruz"]);
    }

    [Fact]
    public async Task Filters_CombineWithAnd()
    {
        using var db = await SeedAsync();

        (await LastNamesAsync(db, new EmployeeListQuery { ClientIds = [ClientA], PayrollGroupIds = [GroupSemi] }))
            .Should().Equal("Abella");
    }

    [Fact]
    public async Task TextFilters_MatchPartialBioIdAndStatutoryNumbers()
    {
        using var db = await SeedAsync();

        (await LastNamesAsync(db, new EmployeeListQuery { BioId = "200" }))
            .Should().BeEquivalentTo(["Cruz", "Dela Rosa"]);
        (await LastNamesAsync(db, new EmployeeListQuery { SSSNo = "1234567" }))
            .Should().Equal("Abella");
        (await LastNamesAsync(db, new EmployeeListQuery { TIN = "888" }))
            .Should().Equal("Estrada");
    }

    [Fact]
    public async Task Keyword_MatchesRelatedNamesAndStatutoryNumbers()
    {
        using var db = await SeedAsync();

        (await LastNamesAsync(db, new EmployeeListQuery { Keyword = "Zenith" }))
            .Should().BeEquivalentTo(["Bautista", "Estrada"], "keyword matches the client's name");
        (await LastNamesAsync(db, new EmployeeListQuery { Keyword = "North" }))
            .Should().BeEquivalentTo(["Abella", "Cruz"], "keyword matches the project site's name");
        (await LastNamesAsync(db, new EmployeeListQuery { Keyword = "123-456" }))
            .Should().Equal("Abella");
    }

    [Fact]
    public async Task Sorts_ByWhitelistedField_AndFallsBackToNameForUnknownField()
    {
        using var db = await SeedAsync();

        (await LastNamesAsync(db, new EmployeeListQuery { SortField = "bioId", SortOrder = "descend" }))
            .Take(2).Should().Equal("Dela Rosa", "Cruz");
        (await LastNamesAsync(db, new EmployeeListQuery { SortField = "clientName", SortOrder = "descend" }))
            .Take(2).Should().BeEquivalentTo(["Bautista", "Estrada"], "Zenith Foods sorts first descending");
        (await LastNamesAsync(db, new EmployeeListQuery { SortField = "no; DROP TABLE Employees" }))
            .Should().Equal("Abella", "Bautista", "Cruz", "Dela Rosa", "Estrada");
    }

    [Fact]
    public async Task Pages_ReturnTheRightSlice_WithTheFilteredTotal()
    {
        using var db = await SeedAsync();
        await using var context = db.NewContext();
        var service = BuildService(new UnitOfWorkService(context));

        var page2 = await service.SearchAsync(new EmployeeListQuery { Page = 2, Limit = 2 }, CancellationToken.None);

        page2.Data!.Select(x => x.LastName).Should().Equal("Cruz", "Dela Rosa");
        page2.MetaData.TotalCount.Should().Be(5);
        page2.MetaData.TotalPages.Should().Be(3);
        page2.MetaData.CurrentPage.Should().Be(2);

        var filtered = await service.SearchAsync(new EmployeeListQuery { ClientIds = [ClientA], Limit = 1 }, CancellationToken.None);
        filtered.MetaData.TotalCount.Should().Be(2, "the total counts every match, not just the current page");
    }

    // Backs ApprovalActionResponse.ActorName -- the approval timeline's "Approved by <name>".
    [Fact]
    public async Task GetDisplayNamesAsync_ReturnsFirstLastForKnownIds_AndSkipsUnknown()
    {
        using var db = await SeedAsync();
        await using var context = db.NewContext();
        var service = BuildService(new UnitOfWorkService(context));
        var abella = context.Employees.Single(x => x.LastName == "Abella").Id;

        var names = await service.GetDisplayNamesAsync([abella, abella, Guid.NewGuid()], CancellationToken.None);

        names.Should().ContainSingle().Which.Should().Be(
            new KeyValuePair<Guid, string>(abella, "Jerome Abella"));
    }

    [Fact]
    public void Limit_IsCapped()
    {
        new EmployeeListQuery { Limit = 10_000 }.NormalizedLimit.Should().Be(EmployeeListQuery.MaxLimit);
        new EmployeeListQuery { Limit = 0, Page = 0 }.NormalizedPage.Should().Be(1);
    }
}
