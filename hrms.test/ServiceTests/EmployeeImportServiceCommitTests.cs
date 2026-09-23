using ClosedXML.Excel;
using Hrms.Core.Services;
using Hrms.Domain.Entities;
using hrms.test.TestSupport;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>
/// EmployeeImportService.Upload -- the real commit path (unlike EmployeeImportServicePreviewTests,
/// which only exercises the read-only PreviewAsync against mocked repositories). Needs a REAL
/// relational provider (SQLite) since PersistAsync actually queries/writes CostCenters,
/// Department, PayrollGroup, etc.
///
/// Regression guard: "Project Site" is an OPTIONAL import column with no SetDefaults fallback
/// (unlike Department/Client, which always default to "--"), so a blank cell leaves
/// EmployeeImportModel.ProjectSiteName null. PersistAsync used to look that up with
/// Dictionary.TryGetValue(item.ProjectSiteName!, ...) unconditionally, which throws
/// ArgumentNullException on a null key -- meaning ANY row without a Project Site value aborted
/// the entire commit (surfacing to the user as the import hanging/failing to save).
/// </summary>
public class EmployeeImportServiceCommitTests
{
    private static EmployeeImportService BuildService(IUnitOfWorkService uow)
    {
        TypeAdapterConfig.GlobalSettings.Scan(typeof(MappingProfile).Assembly);

        var employeeService = new EmployeeService(
            uow,
            Substitute.For<IMapper>(),
            TypeAdapterConfig.GlobalSettings,
            new DepartmentService(uow),
            new PayrollGroupService(uow),
            new BranchService(uow),
            new CostCenterService(uow),
            new PositionService(uow),
            new SectionService(uow));

        return new EmployeeImportService(
            uow,
            employeeService,
            new TimeShiftService(uow, TypeAdapterConfig.GlobalSettings, Substitute.For<IMapper>()),
            new DepartmentService(uow),
            new PayrollGroupService(uow),
            new ClientService(uow),
            new BranchService(uow),
            new CostCenterService(uow));
    }

    private static MemoryStream BuildWorkbook(params (string BioId, string FirstName, string LastName, string? ProjectSite)[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sheet1");
        sheet.Cell(2, 1).Value = "BioId";
        sheet.Cell(2, 2).Value = "FirstName";
        sheet.Cell(2, 3).Value = "MiddleName";
        sheet.Cell(2, 4).Value = "LastName";
        sheet.Cell(2, 5).Value = "Suffix";
        sheet.Cell(2, 6).Value = "Project Site";

        for (var i = 0; i < rows.Length; i++)
        {
            var r = rows[i];
            var row = i + 3;
            sheet.Cell(row, 1).Value = r.BioId;
            sheet.Cell(row, 2).Value = r.FirstName;
            sheet.Cell(row, 4).Value = r.LastName;
            if (!string.IsNullOrEmpty(r.ProjectSite)) sheet.Cell(row, 6).Value = r.ProjectSite;
        }

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public async Task Upload_DoesNotThrow_WhenProjectSiteIsBlank()
    {
        using var db = new SqliteHrmsContext();
        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var sut = BuildService(uow);

        using var file = BuildWorkbook(("1001", "Juan", "DelaCruz", null));

        var act = () => sut.Upload(file, CancellationToken.None);

        await act.Should().NotThrowAsync();

        await using var verify = db.NewContext();
        var employee = await verify.Employees.SingleAsync(x => x.FirstName == "Juan");
        employee.AreaId.Should().BeNull("no Project Site was supplied for this row");
    }

    [Fact]
    public async Task Upload_ResolvesProjectSite_ToANewlyCreatedCostCenter()
    {
        using var db = new SqliteHrmsContext();
        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var sut = BuildService(uow);

        using var file = BuildWorkbook(("1002", "Maria", "Santos", "Manila HQ"));

        await sut.Upload(file, CancellationToken.None);

        await using var verify = db.NewContext();
        var employee = await verify.Employees.SingleAsync(x => x.FirstName == "Maria");
        employee.AreaId.Should().NotBeNull();
        var area = await verify.Areas.FindAsync(employee.AreaId!.Value);
        area.Should().NotBeNull();
        area!.Name.Should().Be("Manila HQ");
    }

    /// <summary>
    /// Regression guard for a production 500 ("Duplicate entry ... for key
    /// IX_EmployeeSettings_EmployeeId"): PersistAsync's existing-employee (update-in-place)
    /// branch used to attach a fresh `new EmployeeSetting` with a default Id to the matched
    /// employee. EF's UpdateRange graph-walk treats that as a brand-new entity to INSERT since
    /// its key is unset, colliding with the EmployeeSetting row the employee already has
    /// (one-to-one, unique-indexed on EmployeeId) -- so re-importing ANY already-existing
    /// employee (the exact "update instead of stop" path this whole import redesign exists for)
    /// threw on the second upload of the same person.
    /// </summary>
    [Fact]
    public async Task Upload_ReimportingAnExistingEmployee_UpdatesInPlace_WithoutDuplicatingSettings()
    {
        using var db = new SqliteHrmsContext();
        var bioId = "1005";

        // First import: creates the employee (and its one EmployeeSetting row) fresh.
        await using (var context = db.NewContext())
        {
            var uow = new UnitOfWorkService(context);
            var sut = BuildService(uow);
            using var file = BuildWorkbook((bioId, "Liza", "Gomez", null));
            await sut.Upload(file, CancellationToken.None);
        }

        // Second import of the SAME person (same name, same BioId) -- must update in place, not
        // throw, and must not create a second EmployeeSetting row for the same employee.
        await using (var context = db.NewContext())
        {
            var uow = new UnitOfWorkService(context);
            var sut = BuildService(uow);
            using var file = BuildWorkbook((bioId, "Liza", "Gomez", "Davao Site"));

            var act = () => sut.Upload(file, CancellationToken.None);

            await act.Should().NotThrowAsync();
        }

        await using var verify = db.NewContext();
        (await verify.Employees.CountAsync()).Should().Be(1,
            "the second import should update the same employee, not create a duplicate");
        var employee = await verify.Employees.SingleAsync();
        employee.AreaId.Should().NotBeNull("the second file supplied a Project Site");
        (await verify.EmployeeSettings.CountAsync(x => x.EmployeeId == employee.Id)).Should().Be(1,
            "re-importing must not create a second EmployeeSetting row for the same employee");
    }

    [Fact]
    public async Task Upload_MixOfBlankAndPopulatedProjectSite_PersistsBothRows()
    {
        using var db = new SqliteHrmsContext();
        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var sut = BuildService(uow);

        using var file = BuildWorkbook(
            ("1003", "Pedro", "Reyes", null),
            ("1004", "Ana", "Cruz", "Cebu Branch"));

        await sut.Upload(file, CancellationToken.None);

        await using var verify = db.NewContext();
        (await verify.Employees.CountAsync()).Should().Be(2);
        (await verify.Employees.SingleAsync(x => x.FirstName == "Pedro")).AreaId.Should().BeNull();
        (await verify.Employees.SingleAsync(x => x.FirstName == "Ana")).AreaId.Should().NotBeNull();
    }
}
