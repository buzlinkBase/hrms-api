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
/// Company-level Employee ID numbering. Off (the default) leaves the employee number as free text,
/// exactly as before; Auto hands out PREFIX-0001 style numbers for new employees, keeps typed
/// numbers, refuses duplicates and never lets a save wipe a number or reset the counter.
/// </summary>
public class EmployeeNumberingTests
{
    private static Company CompanyRow(string mode, string prefix = "EMP", int padding = 4, bool year = false, int next = 1) => new()
    {
        Id = Guid.NewGuid(), Code = "MAIN", Description = "Acme",
        EmployeeIdMode = mode, EmployeeIdPrefix = prefix, EmployeeIdPadding = padding,
        EmployeeIdIncludeYear = year, NextEmployeeNo = next,
    };

    private static async Task<Guid> SeedAsync(SqliteHrmsContext db, Company company, params Employee[] existing)
    {
        await using var seed = db.NewContext();
        var group = new PayrollGroup { Id = Guid.NewGuid(), Name = "Main" };
        seed.PayrollGroups.Add(group);
        seed.Companies.Add(company);
        foreach (var employee in existing) employee.PayrollGroupId = group.Id;
        seed.Employees.AddRange(existing);
        await seed.SaveChangesAsync();
        return group.Id;
    }

    private static Employee NewEmployee(Guid groupId, string first, string number = "") => new()
    {
        PayrollGroupId = groupId, JobLevel = "", FirstName = first, LastName = "Test", EmployeeNo = number,
    };

    private static EmployeeService ServiceFor(HrmsContext context)
    {
        _ = TestMapping.GlobalSettings;
        var uow = new UnitOfWorkService(context);
        return new EmployeeService(
            uow, Substitute.For<IMapper>(), TypeAdapterConfig.GlobalSettings,
            new DepartmentService(uow), new PayrollGroupService(uow), new BranchService(uow),
            new CostCenterService(uow), new PositionService(uow), new SectionService(uow));
    }

    // One context + service per operation: BuzlinkRepository's unit of work commits once per instance.
    private static async Task AddAsync(SqliteHrmsContext db, Guid group, string first, string number = "")
    {
        await using var context = db.NewContext();
        await ServiceFor(context).AddAsync(NewEmployee(group, first, number), CancellationToken.None);
    }

    private static async Task<List<string>> NumbersAsync(SqliteHrmsContext db)
    {
        await using var verify = db.NewContext();
        return await verify.Employees.OrderBy(e => e.FirstName).Select(e => e.EmployeeNo).ToListAsync();
    }

    // ── Off: nothing changes ────────────────────────────────────────────────

    [Fact]
    public async Task Off_LeavesTheNumberAsTyped_BlankStaysBlank_DuplicatesAllowed()
    {
        using var db = new SqliteHrmsContext();
        var group = await SeedAsync(db, CompanyRow(EmployeeIdSettings.Off));

        await AddAsync(db, group, "A", "X-1");
        await AddAsync(db, group, "B", "X-1");
        await AddAsync(db, group, "C");

        (await NumbersAsync(db)).Should().Equal("X-1", "X-1", "");
        await using var verify = db.NewContext();
        (await verify.Companies.SingleAsync()).NextEmployeeNo.Should().Be(1);
    }

    // ── Auto ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Auto_NumbersNewEmployeesInOrder_AndAdvancesTheCounter()
    {
        using var db = new SqliteHrmsContext();
        var group = await SeedAsync(db, CompanyRow(EmployeeIdSettings.Auto));

        await AddAsync(db, group, "A");
        await AddAsync(db, group, "B");

        (await NumbersAsync(db)).Should().Equal("EMP-0001", "EMP-0002");
        await using var verify = db.NewContext();
        (await verify.Companies.SingleAsync()).NextEmployeeNo.Should().Be(3);
    }

    [Fact]
    public async Task Auto_KeepsATypedNumber_AndRefusesADuplicate()
    {
        using var db = new SqliteHrmsContext();
        var group = await SeedAsync(db, CompanyRow(EmployeeIdSettings.Auto));

        await AddAsync(db, group, "A", "HR-77");
        var act = () => AddAsync(db, group, "B", "hr-77");

        (await act.Should().ThrowAsync<Exception>()).Which.Message.Should().Contain("Employee number");
        (await NumbersAsync(db)).Should().Equal("HR-77");
    }

    [Fact]
    public async Task Auto_SkipsNumbersAlreadyUsedByAnExistingEmployee()
    {
        using var db = new SqliteHrmsContext();
        var existing = new Employee { Id = Guid.NewGuid(), JobLevel = "", FirstName = "Old", LastName = "Hand", EmployeeNo = "EMP-0001" };
        var group = await SeedAsync(db, CompanyRow(EmployeeIdSettings.Auto), existing);
        await using var context = db.NewContext();
        var sut = ServiceFor(context);

        await sut.AddAsync(NewEmployee(group, "New"), CancellationToken.None);

        (await NumbersAsync(db)).Should().Equal("EMP-0002", "EMP-0001");
    }

    [Fact]
    public async Task Auto_UsesThePaddingAndYear()
    {
        using var db = new SqliteHrmsContext();
        var group = await SeedAsync(db, CompanyRow(EmployeeIdSettings.Auto, "ACME", padding: 6, year: true, next: 42));
        await using var context = db.NewContext();
        var sut = ServiceFor(context);

        await sut.AddAsync(NewEmployee(group, "A"), CancellationToken.None);

        var year = PhTime.Today(DateTime.UtcNow).Year;
        (await NumbersAsync(db)).Should().Equal($"ACME-{year}-000042");
    }

    [Fact]
    public async Task Auto_ABatchOfNewEmployees_GetsOneBlockOfNumbers_AndSeededOnesAreUntouched()
    {
        using var db = new SqliteHrmsContext();
        var group = await SeedAsync(db, CompanyRow(EmployeeIdSettings.Auto));
        await using var context = db.NewContext();
        var sut = ServiceFor(context);

        await sut.AddOrUpdateRange(
        [
            NewEmployee(group, "A"), NewEmployee(group, "B"), NewEmployee(group, "C"),
            NewEmployee(group, "S", "SEED-1"),
        ]);
        await sut.CommitChangesAsync();

        (await NumbersAsync(db)).Should().Equal("EMP-0001", "EMP-0002", "EMP-0003", "SEED-1");
        await using var verify = db.NewContext();
        (await verify.Companies.SingleAsync()).NextEmployeeNo.Should().Be(4);
    }

    [Fact]
    public async Task Auto_TwoAllocators_NeverHandOutTheSameNumber()
    {
        using var db = new SqliteHrmsContext();
        await SeedAsync(db, CompanyRow(EmployeeIdSettings.Auto));
        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);

        // Two allocators over the same company row, as two requests would be.
        var a = await new EmployeeNumberAllocator(uow).ReserveAsync(2);
        var b = await new EmployeeNumberAllocator(uow).ReserveAsync(2);

        a.Concat(b).Should().OnlyHaveUniqueItems().And.Equal("EMP-0001", "EMP-0002", "EMP-0003", "EMP-0004");
    }

    [Fact]
    public async Task Auto_BlankNumberOnUpdate_DoesNotEraseTheStoredOne()
    {
        using var db = new SqliteHrmsContext();
        var existing = new Employee { Id = Guid.NewGuid(), JobLevel = "", FirstName = "Pat", LastName = "Test", EmployeeNo = "EMP-0009" };
        var group = await SeedAsync(db, CompanyRow(EmployeeIdSettings.Auto), existing);
        await using var context = db.NewContext();
        var sut = ServiceFor(context);

        await sut.UpdateAsync(new UpdateEmployee
        {
            Id = existing.Id, PayrollGroupId = group, JobLevel = "", FirstName = "Pat", LastName = "Test", EmployeeNo = "",
        }, CancellationToken.None);

        (await NumbersAsync(db)).Should().Equal("EMP-0009");
    }

    [Fact]
    public async Task Off_BlankNumberOnUpdate_StillClearsIt_AsBefore()
    {
        using var db = new SqliteHrmsContext();
        var existing = new Employee { Id = Guid.NewGuid(), JobLevel = "", FirstName = "Pat", LastName = "Test", EmployeeNo = "OLD-1" };
        var group = await SeedAsync(db, CompanyRow(EmployeeIdSettings.Off), existing);
        await using var context = db.NewContext();
        var sut = ServiceFor(context);

        await sut.UpdateAsync(new UpdateEmployee
        {
            Id = existing.Id, PayrollGroupId = group, JobLevel = "", FirstName = "Pat", LastName = "Test", EmployeeNo = "",
        }, CancellationToken.None);

        (await NumbersAsync(db)).Should().Equal("");
    }

    // ── Company settings ────────────────────────────────────────────────────

    [Fact]
    public async Task SavingCompanyInfo_NeverResetsTheCounter()
    {
        using var db = new SqliteHrmsContext();
        await SeedAsync(db, CompanyRow(EmployeeIdSettings.Auto, next: 57));
        _ = TestMapping.GlobalSettings;
        await using var context = db.NewContext();
        var service = new CompanyService(new UnitOfWorkService(context));

        await service.SaveAsync(new CreateCompany
        {
            Description = "Acme", EmployeeIdMode = EmployeeIdSettings.Auto, EmployeeIdPrefix = "STAFF", EmployeeIdPadding = 5,
        }, CancellationToken.None);

        await using var verify = db.NewContext();
        var company = await verify.Companies.SingleAsync();
        company.NextEmployeeNo.Should().Be(57);
        company.EmployeeIdPrefix.Should().Be("STAFF");
        company.EmployeeIdPadding.Should().Be(5);
    }

    [Theory]
    [InlineData("Off", "", 4, null)]
    [InlineData("", "", 0, null)]
    [InlineData("Auto", "EMP", 4, null)]
    [InlineData("Auto", " emp-1 ", 8, null)]
    [InlineData("Auto", "", 4, null)]
    [InlineData("Auto", "   ", 4, null)]
    [InlineData("Auto", "EMP_1", 4, "prefix")]
    [InlineData("Auto", "ABCDEFGHIJK", 4, "prefix")]
    [InlineData("Auto", "EMP", 0, "length")]
    [InlineData("Auto", "EMP", 9, "length")]
    [InlineData("Sometimes", "EMP", 4, "Off or Auto")]
    public void Settings_AreValidated(string mode, string prefix, int padding, string? expectedErrorPart)
    {
        var error = EmployeeIdSettings.Validate(mode, prefix, padding);

        if (expectedErrorPart == null) error.Should().BeNull();
        else error.Should().Contain(expectedErrorPart);
    }

    [Fact]
    public void Format_PadsAndOptionallyAddsTheYear()
    {
        EmployeeIdSettings.Format("EMP", 4, false, 2026, 7).Should().Be("EMP-0007");
        EmployeeIdSettings.Format("EMP", 4, true, 2026, 7).Should().Be("EMP-2026-0007");
        EmployeeIdSettings.Format(" EMP ", 2, false, 2026, 12345).Should().Be("EMP-12345");
    }

    [Fact]
    public void Format_WithoutAPrefix_IsJustTheNumber_OrYearAndNumber()
    {
        EmployeeIdSettings.Format("", 4, false, 2026, 7).Should().Be("0007");
        EmployeeIdSettings.Format(null, 4, true, 2026, 7).Should().Be("2026-0007");
        EmployeeIdSettings.Format("  ", 3, false, 2026, 12).Should().Be("012");
    }

    [Fact]
    public async Task Auto_WithoutAPrefix_NumbersPlainly()
    {
        using var db = new SqliteHrmsContext();
        var group = await SeedAsync(db, CompanyRow(EmployeeIdSettings.Auto, prefix: ""));

        await AddAsync(db, group, "A");
        await AddAsync(db, group, "B");

        (await NumbersAsync(db)).Should().Equal("0001", "0002");
    }

    // ── Registered name on the legal forms ──────────────────────────────────

    [Fact]
    public void LegalName_PrefersTheRegisteredName_ThenTheDisplayName_ThenThePlaceholder()
    {
        var both = new Company { Description = "Acme", RegisteredName = " Acme Trading Inc. " };
        var displayOnly = new Company { Description = "Acme", RegisteredName = "  " };

        Hrms.Api.Documents.Shared.ReportDocumentStyle.LegalName(both).Should().Be("Acme Trading Inc.");
        Hrms.Api.Documents.Shared.ReportDocumentStyle.LegalName(displayOnly).Should().Be("Acme");
        Hrms.Api.Documents.Shared.ReportDocumentStyle.LegalName(null).Should().Be("(Company Name Not Set)");
        // Everything that isn't a legal form keeps the display name.
        Hrms.Api.Documents.Shared.ReportDocumentStyle.CompanyName(both).Should().Be("Acme");
    }

    // ── Default rest days ───────────────────────────────────────────────────

    [Theory]
    [InlineData("", "Sunday")]
    [InlineData(null, "Sunday")]
    [InlineData("Saturday,Sunday", "Saturday,Sunday")]
    [InlineData(" friday , SUNDAY ,Friday,Nope", "Friday,Sunday")]
    public void RestDays_AreParsedInOrder_WithSundayWhenNoneAreSet(string? value, string expected)
    {
        string.Join(",", CompanyRestDays.Parse(value)).Should().Be(expected);
    }
}
