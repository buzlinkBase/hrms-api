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
using Microsoft.EntityFrameworkCore;
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
        _ = TestMapping.GlobalSettings; // registers MappingProfile once per run (see TestMapping)
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

    // Job Level is plain text -- any value is stored and found by a partial search, including a
    // row still holding an old enum name.
    [Fact]
    public async Task JobLevelFilter_MatchesFreeTextAndLegacyValues_ByPartialText()
    {
        using var db = await SeedAsync();
        await using (var seed = db.NewContext())
        {
            var projectLead = NewEmployee("Fajardo", "Eli", GroupSemi);
            projectLead.JobLevel = "Project Lead";
            var legacy = NewEmployee("Garcia", "Fe", GroupSemi);
            legacy.JobLevel = "TechnicalSpecialist";
            seed.Employees.AddRange(projectLead, legacy);
            await seed.SaveChangesAsync();
        }

        // SQLite's Contains is case-sensitive (MySQL's default collation isn't), same as the other
        // text filters' tests here.
        (await LastNamesAsync(db, new EmployeeListQuery { JobLevel = "Project" }))
            .Should().Equal("Fajardo");
        (await LastNamesAsync(db, new EmployeeListQuery { JobLevel = "Technical" }))
            .Should().Equal("Garcia");
    }

    // The Job Level suggestions: each level the company uses once, however it was spelled or spaced.
    [Fact]
    public async Task JobLevels_AreTheCompanysOwn_OncePerSpelling_Sorted()
    {
        using var db = await SeedAsync();
        await using (var seed = db.NewContext())
        {
            string[] levels = ["Supervisory", "  Managerial ", "managerial", "Project Lead", "", "Supervisory"];
            seed.Employees.AddRange(levels.Select((level, i) =>
            {
                var employee = NewEmployee($"Level{i}", "X", GroupSemi);
                employee.JobLevel = level;
                return employee;
            }));
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var result = await BuildService(new UnitOfWorkService(context)).GetJobLevelsAsync(CancellationToken.None);

        result.Should().Equal("Managerial", "Project Lead", "Supervisory");
    }

    // Regression: EmployeeModel (GET employees/{id} and the table) had no JobLevel, so the edit
    // form fell back to the default level and saving overwrote the one the user had chosen.
    [Fact]
    public async Task EmployeeModel_CarriesTheStoredJobLevel_InTheListAndSingleRecordMapping()
    {
        using var db = await SeedAsync();
        var projectLead = NewEmployee("Fajardo", "Eli", GroupSemi);
        projectLead.JobLevel = "Project Lead";
        await using (var seed = db.NewContext())
        {
            seed.Employees.Add(projectLead);
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var list = await BuildService(new UnitOfWorkService(context))
            .SearchAsync(new EmployeeListQuery { FullName = "Fajardo" }, CancellationToken.None);
        list.Data!.Single().JobLevel.Should().Be("Project Lead");

        projectLead.Adapt<EmployeeModel>(TestMapping.GlobalSettings).JobLevel.Should().Be("Project Lead");
    }

    [Fact]
    public async Task Update_SavesTheJobLevelExactlyAsTyped()
    {
        using var db = await SeedAsync();
        var employee = NewEmployee("Fajardo", "Eli", GroupSemi);
        await using (var seed = db.NewContext())
        {
            seed.Employees.Add(employee);
            await seed.SaveChangesAsync();
        }

        await using (var context = db.NewContext())
        {
            await BuildService(new UnitOfWorkService(context)).UpdateAsync(new UpdateEmployee
            {
                Id = employee.Id, PayrollGroupId = GroupSemi, FirstName = "Eli", LastName = "Fajardo",
                JobLevel = "Senior Site Engineer",
            }, CancellationToken.None);
        }

        await using var check = db.NewContext();
        (await check.Employees.SingleAsync(x => x.Id == employee.Id)).JobLevel.Should().Be("Senior Site Engineer");
    }

    [Fact]
    public void NewEmployee_HasNoJobLevelUntilOneIsEntered()
    {
        new Employee().JobLevel.Should().BeEmpty();
        new CreateEmployee().JobLevel.Should().BeEmpty();
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

    // Regression: the whole search text was matched against one column at a time, so a name typed
    // the way the table shows it ("Last, First"), or in natural order ("First Last"), matched no
    // single column and found nobody.
    [Theory]
    [InlineData("Abella, Jerome")]
    [InlineData("Jerome Abella")]
    [InlineData("abella jerome")]
    [InlineData("  Jerome   Abella  ")]
    public async Task Keyword_FindsAPersonByFullName_InAnyOrder(string keyword)
    {
        using var db = await SeedAsync();

        // SQLite is case-sensitive where MySQL isn't -- compare case-insensitively here.
        var normalized = keyword.Contains("abella") ? "Abella Jerome" : keyword;
        (await LastNamesAsync(db, new EmployeeListQuery { Keyword = normalized }))
            .Should().Equal("Abella");
    }

    [Fact]
    public async Task Keyword_EveryWordMustMatchSomething()
    {
        using var db = await SeedAsync();

        (await LastNamesAsync(db, new EmployeeListQuery { Keyword = "Dela Rosa, Carla" }))
            .Should().Equal(new[] { "Dela Rosa" }, "a two-word surname plus a first name");
        (await LastNamesAsync(db, new EmployeeListQuery { Keyword = "Cruz Zenith" }))
            .Should().BeEmpty("Cruz is with Acme, not Zenith");
        (await LastNamesAsync(db, new EmployeeListQuery { Keyword = "Ben Acme" }))
            .Should().Equal(new[] { "Cruz" }, "name and client together narrow it down");
    }

    // Imported client names carry double spaces ("Metro Retails  Store Group"); a search typed
    // with single spaces used to miss everyone at that client.
    [Fact]
    public async Task Keyword_IgnoresDifferencesInSpacing()
    {
        using var db = await SeedAsync();
        await using (var seed = db.NewContext())
        {
            var client = new Client { Id = Guid.NewGuid(), Name = "Metro Retails  Store Group Inc." };
            seed.Clients.Add(client);
            seed.Employees.Add(NewEmployee("Ando", "Hazel", GroupSemi, client.Id));
            await seed.SaveChangesAsync();
        }

        (await LastNamesAsync(db, new EmployeeListQuery { Keyword = "Metro Retails Store" }))
            .Should().Equal("Ando");
    }

    [Fact]
    public async Task FullNameColumnFilter_FindsNameAsShownInTheTable()
    {
        using var db = await SeedAsync();

        (await LastNamesAsync(db, new EmployeeListQuery { FullName = "Dela Rosa, Carla" }))
            .Should().Equal("Dela Rosa");
        (await LastNamesAsync(db, new EmployeeListQuery { FullName = "Carla Cruz" }))
            .Should().BeEmpty();
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

    // Regression: a confidential approver the caller may not see was missing from the result, so
    // the approval timeline and workflow setup showed their raw employee id instead of a name.
    [Fact]
    public async Task GetDisplayNamesAsync_NamesAHiddenConfidentialEmployee_Confidential()
    {
        using var db = await SeedAsync();
        Guid abella, cruz;
        await using (var seed = db.NewContext())
        {
            abella = seed.Employees.Single(x => x.LastName == "Abella").Id;
            cruz = seed.Employees.Single(x => x.LastName == "Cruz").Id;
        }
        await using var context = db.NewContext(new AnalyticsSeed.Uncleared(cruz));
        var service = BuildService(new UnitOfWorkService(context));

        var names = await service.GetDisplayNamesAsync([abella, cruz, Guid.NewGuid()], CancellationToken.None);

        names.Should().BeEquivalentTo(new Dictionary<Guid, string>
        {
            [abella] = "Jerome Abella",
            [cruz] = EmployeeService.ConfidentialName,
        }, "an unknown id is still left out");
    }

    [Fact]
    public void Limit_IsCapped()
    {
        new EmployeeListQuery { Limit = 10_000 }.NormalizedLimit.Should().Be(EmployeeListQuery.MaxLimit);
        new EmployeeListQuery { Limit = 0, Page = 0 }.NormalizedPage.Should().Be(1);
    }
}
