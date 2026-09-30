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
/// (unlike Department, which defaults to "--"), so a blank cell leaves
/// EmployeeImportModel.ProjectSiteName null. PersistAsync used to look that up with
/// Dictionary.TryGetValue(item.ProjectSiteName!, ...) unconditionally, which throws
/// ArgumentNullException on a null key -- meaning ANY row without a Project Site value aborted
/// the entire commit (surfacing to the user as the import hanging/failing to save).
/// </summary>
public class EmployeeImportServiceCommitTests
{
    private static EmployeeImportService BuildService(IUnitOfWorkService uow)
    {
        _ = TestMapping.GlobalSettings; // registers MappingProfile once per run (see TestMapping)

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

    private static MemoryStream BuildWorkbookWithClients(params (string BioId, string FirstName, string? Client)[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sheet1");
        sheet.Cell(2, 1).Value = EmployeeTemplateHeaders.BioId;
        sheet.Cell(2, 2).Value = EmployeeTemplateHeaders.FirstName;
        sheet.Cell(2, 3).Value = EmployeeTemplateHeaders.LastName;
        sheet.Cell(2, 4).Value = EmployeeTemplateHeaders.ClientName;

        for (var i = 0; i < rows.Length; i++)
        {
            var row = i + 3;
            sheet.Cell(row, 1).Value = rows[i].BioId;
            sheet.Cell(row, 2).Value = rows[i].FirstName;
            sheet.Cell(row, 3).Value = "Tester";
            if (rows[i].Client != null) sheet.Cell(row, 4).Value = rows[i].Client;
        }

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    // Client is optional: a blank Client Name used to become a placeholder client named "--"
    // that every such employee was attached to.
    [Fact]
    public async Task Upload_BlankClient_LeavesClientEmpty_AndCreatesNoPlaceholderClient()
    {
        using var db = new SqliteHrmsContext();
        await using var context = db.NewContext();
        var sut = BuildService(new UnitOfWorkService(context));

        using var file = BuildWorkbookWithClients(("2001", "Noel", null));
        await sut.Upload(file, CancellationToken.None);

        await using var verify = db.NewContext();
        (await verify.Employees.SingleAsync(x => x.FirstName == "Noel")).ClientId.Should().BeNull();
        (await verify.Clients.AnyAsync()).Should().BeFalse("no client was named in the file");
    }

    [Fact]
    public async Task Upload_NamedClients_AreCreatedOnce_IgnoringCaseAndSpacing()
    {
        using var db = new SqliteHrmsContext();
        await using var context = db.NewContext();
        var sut = BuildService(new UnitOfWorkService(context));

        using var file = BuildWorkbookWithClients(
            ("2002", "Rosa", "Acme Corp"),
            ("2003", "Lito", " acme corp "),
            ("2004", "Tess", null));
        await sut.Upload(file, CancellationToken.None);

        await using var verify = db.NewContext();
        var client = await verify.Clients.SingleAsync();
        client.Name.Should().Be("Acme Corp");
        (await verify.Employees.SingleAsync(x => x.FirstName == "Rosa")).ClientId.Should().Be(client.Id);
        (await verify.Employees.SingleAsync(x => x.FirstName == "Lito")).ClientId.Should().Be(client.Id);
        (await verify.Employees.SingleAsync(x => x.FirstName == "Tess")).ClientId.Should().BeNull();
    }

    private static MemoryStream BuildWorkbookWithShiftTimes(params (string BioId, string FirstName, object? AmIn, object? PmOut)[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sheet1");
        sheet.Cell(2, 1).Value = EmployeeTemplateHeaders.BioId;
        sheet.Cell(2, 2).Value = EmployeeTemplateHeaders.FirstName;
        sheet.Cell(2, 3).Value = EmployeeTemplateHeaders.LastName;
        sheet.Cell(2, 4).Value = EmployeeTemplateHeaders.ShiftName;
        sheet.Cell(2, 5).Value = EmployeeTemplateHeaders.AmIn;
        sheet.Cell(2, 6).Value = EmployeeTemplateHeaders.PmOut;

        for (var i = 0; i < rows.Length; i++)
        {
            var row = i + 3;
            sheet.Cell(row, 1).Value = rows[i].BioId;
            sheet.Cell(row, 2).Value = rows[i].FirstName;
            sheet.Cell(row, 3).Value = "Tester";
            sheet.Cell(row, 4).Value = "Day Shift";
            if (rows[i].AmIn != null) sheet.Cell(row, 5).Value = XLCellValue.FromObject(rows[i].AmIn);
            if (rows[i].PmOut != null) sheet.Cell(row, 6).Value = XLCellValue.FromObject(rows[i].PmOut);
        }

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    // TimeShiftId is nullable: a row with no time, or a time that isn't one, used to be handed
    // a conjured "Day Shift-8:00:00-17:00:00" shift instead of being left without a shift.
    [Fact]
    public async Task Upload_BlankOrInvalidShiftTimes_LeaveTimeShiftNull_AndCreateNoShift()
    {
        using var db = new SqliteHrmsContext();
        await using var context = db.NewContext();
        var sut = BuildService(new UnitOfWorkService(context));

        using var file = BuildWorkbookWithShiftTimes(
            ("3001", "Blank", null, null),
            ("3002", "Garbage", "not a time", "17:00:00"),
            ("3003", "HalfDone", 8.0 / 24, null));
        await sut.Upload(file, CancellationToken.None);

        await using var verify = db.NewContext();
        (await verify.Employees.CountAsync()).Should().Be(3);
        (await verify.Employees.AllAsync(x => x.TimeShiftId == null)).Should().BeTrue();
        (await verify.TimeShifts.AnyAsync()).Should().BeFalse("no row carried a usable AMIN + PM OUT pair");
    }

    [Fact]
    public async Task Upload_ValidShiftTimes_AssignTheImportedShift()
    {
        using var db = new SqliteHrmsContext();
        await using var context = db.NewContext();
        var sut = BuildService(new UnitOfWorkService(context));

        using var file = BuildWorkbookWithShiftTimes(
            ("3004", "Numeric", 7.0 / 24, 16.0 / 24),
            ("3005", "Typed", "7:00 AM", "4:00 PM"),
            ("3006", "NoShift", null, null));
        await sut.Upload(file, CancellationToken.None);

        await using var verify = db.NewContext();
        var numeric = await verify.Employees.SingleAsync(x => x.FirstName == "Numeric");
        var typed = await verify.Employees.SingleAsync(x => x.FirstName == "Typed");
        numeric.TimeShiftId.Should().NotBeNull();
        typed.TimeShiftId.Should().NotBeNull();
        var shift = await verify.TimeShifts.SingleAsync(x => x.Id == numeric.TimeShiftId);
        shift.StartTime.Should().Be(new TimeSpan(7, 0, 0));
        shift.EndTime.Should().Be(new TimeSpan(16, 0, 0));
        (await verify.Employees.SingleAsync(x => x.FirstName == "NoShift")).TimeShiftId.Should().BeNull();
    }

    private static MemoryStream BuildWorkbookWithBranchAndSite(params (string FirstName, string LastName, string Branch, string Site)[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sheet1");
        sheet.Cell(2, 1).Value = EmployeeTemplateHeaders.FirstName;
        sheet.Cell(2, 2).Value = EmployeeTemplateHeaders.LastName;
        sheet.Cell(2, 3).Value = EmployeeTemplateHeaders.BranchCode;
        sheet.Cell(2, 4).Value = EmployeeTemplateHeaders.ProjectSite;

        for (var i = 0; i < rows.Length; i++)
        {
            var row = i + 3;
            sheet.Cell(row, 1).Value = rows[i].FirstName;
            sheet.Cell(row, 2).Value = rows[i].LastName;
            sheet.Cell(row, 3).Value = rows[i].Branch;
            sheet.Cell(row, 4).Value = rows[i].Site;
        }

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    // Every Branch -> Project Site cascade in the app lists only sites whose BranchId matches,
    // so an imported site saved without a Branch looked untagged on every employee using it.
    [Fact]
    public async Task Upload_NewProjectSite_TakesTheBranchOfItsRows()
    {
        using var db = new SqliteHrmsContext();
        var branchId = Guid.NewGuid();
        await using (var seed = db.NewContext())
        {
            seed.Branches.Add(new Branch { Id = branchId, Code = "Main", Name = "Main" });
            await seed.SaveChangesAsync();
        }

        await using (var context = db.NewContext())
        {
            var sut = BuildService(new UnitOfWorkService(context));
            using var file = BuildWorkbookWithBranchAndSite(("Hazel", "Ando", "Main", " Royal Gem "));
            await sut.Upload(file, CancellationToken.None);
        }

        await using var verify = db.NewContext();
        var site = await verify.Areas.SingleAsync();
        site.Name.Should().Be("Royal Gem");
        site.BranchId.Should().Be(branchId);
        var employee = await verify.Employees.SingleAsync();
        employee.AreaId.Should().Be(site.Id);
        employee.BranchId.Should().Be(branchId);
    }

    [Fact]
    public async Task Upload_ExistingProjectSiteWithoutBranch_IsGivenTheImportedBranch()
    {
        using var db = new SqliteHrmsContext();
        var branchId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        await using (var seed = db.NewContext())
        {
            seed.Branches.Add(new Branch { Id = branchId, Code = "Main", Name = "Main" });
            seed.Areas.Add(new CostCenters { Id = siteId, Code = "0001", Name = "Royal Gem" });
            await seed.SaveChangesAsync();
        }

        await using (var context = db.NewContext())
        {
            var sut = BuildService(new UnitOfWorkService(context));
            using var file = BuildWorkbookWithBranchAndSite(("Hazel", "Ando", "Main", "royal gem"));
            await sut.Upload(file, CancellationToken.None);
        }

        await using var verify = db.NewContext();
        var site = await verify.Areas.SingleAsync();
        site.Id.Should().Be(siteId, "the existing site is reused, not duplicated");
        site.BranchId.Should().Be(branchId);
        (await verify.Employees.SingleAsync()).AreaId.Should().Be(siteId);
    }

    // Headers on row 1 (no Instructions banner), stray spaces everywhere, and the real UI path:
    // preview -> JSON round-trip through the browser -> commit.
    [Fact]
    public async Task PreviewThenCommit_TrimsEveryTextCell_AndTagsTheProjectSite()
    {
        using var db = new SqliteHrmsContext();
        await using (var seed = db.NewContext())
        {
            seed.Branches.Add(new Branch { Id = Guid.NewGuid(), Code = "Main", Name = "Main" });
            await seed.SaveChangesAsync();
        }

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sheet1");
        sheet.Cell(1, 1).Value = EmployeeTemplateHeaders.LastName;
        sheet.Cell(1, 2).Value = EmployeeTemplateHeaders.FirstName;
        sheet.Cell(1, 3).Value = EmployeeTemplateHeaders.BranchCode;
        sheet.Cell(1, 4).Value = EmployeeTemplateHeaders.ClientName;
        sheet.Cell(1, 5).Value = EmployeeTemplateHeaders.ProjectSite;
        sheet.Cell(1, 6).Value = EmployeeTemplateHeaders.Email;
        sheet.Cell(1, 7).Value = EmployeeTemplateHeaders.MiddleName;
        sheet.Cell(2, 1).Value = "Alba ";
        sheet.Cell(2, 2).Value = " Dexter";
        sheet.Cell(2, 3).Value = " Main ";
        sheet.Cell(2, 4).Value = "Metro Retails  Store Group Inc. ";
        sheet.Cell(2, 5).Value = " Royal Gem";
        sheet.Cell(2, 6).Value = " dexter@example.com ";
        sheet.Cell(2, 7).Value = "   ";
        var file = new MemoryStream();
        workbook.SaveAs(file);
        file.Position = 0;

        List<EmployeeImportPreviewRow> fromBrowser;
        await using (var context = db.NewContext())
        {
            var preview = await BuildService(new UnitOfWorkService(context)).PreviewAsync(file, CancellationToken.None);
            var web = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
            fromBrowser = System.Text.Json.JsonSerializer.Deserialize<List<EmployeeImportPreviewRow>>(
                System.Text.Json.JsonSerializer.Serialize(preview, web), web)!;
        }

        fromBrowser.Should().ContainSingle();
        fromBrowser[0].LastName.Should().Be("Alba");
        fromBrowser[0].FirstName.Should().Be("Dexter");
        fromBrowser[0].ProjectSiteName.Should().Be("Royal Gem");
        fromBrowser[0].MiddleName.Should().BeNull("a whitespace-only cell is blank");

        await using (var context = db.NewContext())
        {
            await BuildService(new UnitOfWorkService(context)).CommitPreviewAsync(fromBrowser, CancellationToken.None);
        }

        await using var verify = db.NewContext();
        var employee = await verify.Employees.SingleAsync();
        employee.LastName.Should().Be("Alba");
        employee.FirstName.Should().Be("Dexter");
        employee.Email.Should().Be("dexter@example.com");
        (await verify.Clients.SingleAsync()).Name.Should().Be("Metro Retails  Store Group Inc.");
        var site = await verify.Areas.SingleAsync();
        site.Name.Should().Be("Royal Gem");
        site.BranchId.Should().Be(employee.BranchId);
        employee.AreaId.Should().Be(site.Id);
    }

    // ── Master data is matched by name, never duplicated ───────────────────

    private static MemoryStream BuildWorkbookWithMasters(params (string FirstName, string? Client, string? Department)[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sheet1");
        sheet.Cell(1, 1).Value = EmployeeTemplateHeaders.FirstName;
        sheet.Cell(1, 2).Value = EmployeeTemplateHeaders.LastName;
        sheet.Cell(1, 3).Value = EmployeeTemplateHeaders.ClientName;
        sheet.Cell(1, 4).Value = EmployeeTemplateHeaders.DepartmentName;
        for (var i = 0; i < rows.Length; i++)
        {
            sheet.Cell(i + 2, 1).Value = rows[i].FirstName;
            sheet.Cell(i + 2, 2).Value = "Tester";
            if (rows[i].Client != null) sheet.Cell(i + 2, 3).Value = rows[i].Client;
            if (rows[i].Department != null) sheet.Cell(i + 2, 4).Value = rows[i].Department;
        }
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public async Task Upload_ReusesExistingMasters_ByNameIgnoringCaseAndSpacing()
    {
        using var db = new SqliteHrmsContext();
        var clientId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        await using (var seed = db.NewContext())
        {
            // Saved with stray spaces / other case, and a payroll group whose cutoff rows differ
            // from what the file would create -- all used to come back as duplicates.
            seed.Clients.Add(new Client { Id = clientId, Code = "000007", Name = "Acme Corp " });
            seed.Departments.Add(new Department { Id = departmentId, Code = "000004", Name = "Human Resources" });
            seed.PayrollGroups.Add(new PayrollGroup { Id = groupId, Code = "000002", Name = "-", PayrollFrequency = PayrollFrequency.MONTHLY });
            await seed.SaveChangesAsync();
        }

        await using (var context = db.NewContext())
        {
            using var file = BuildWorkbookWithMasters(("Rosa", "acme corp", "HUMAN RESOURCES"), ("Lito", "ACME CORP", " human resources "));
            await BuildService(new UnitOfWorkService(context)).Upload(file, CancellationToken.None);
        }

        await using var verify = db.NewContext();
        (await verify.Clients.CountAsync()).Should().Be(1);
        (await verify.Departments.CountAsync()).Should().Be(1);
        (await verify.PayrollGroups.CountAsync()).Should().Be(1);
        (await verify.Employees.ToListAsync()).Should().AllSatisfy(e =>
        {
            e.ClientId.Should().Be(clientId);
            e.DepartmentId.Should().Be(departmentId);
            e.PayrollGroupId.Should().Be(groupId);
        });
    }

    [Fact]
    public async Task Upload_NewMasters_AreCreatedOncePerName_WithCodesAfterTheExistingOnes()
    {
        using var db = new SqliteHrmsContext();
        await using (var seed = db.NewContext())
        {
            seed.Clients.Add(new Client { Id = Guid.NewGuid(), Code = "000007", Name = "Old Client" });
            seed.Departments.Add(new Department { Id = Guid.NewGuid(), Code = "000004", Name = "Old Department" });
            await seed.SaveChangesAsync();
        }

        await using (var context = db.NewContext())
        {
            using var file = BuildWorkbookWithMasters(("Rosa", "New Client", "Sales"), ("Lito", "new client", "SALES"));
            await BuildService(new UnitOfWorkService(context)).Upload(file, CancellationToken.None);
        }

        await using var verify = db.NewContext();
        (await verify.Clients.SingleAsync(x => x.Name == "New Client")).Code.Should().Be("000008");
        (await verify.Departments.SingleAsync(x => x.Name == "Sales")).Code.Should().Be("000005");
        (await verify.Clients.CountAsync()).Should().Be(2);
        (await verify.Departments.CountAsync()).Should().Be(2);
    }

    // Every imported employee needs a first and a last name: a nameless row refuses the whole
    // upload instead of saving an employee with a blank name.
    [Fact]
    public async Task Upload_RowMissingAName_IsRefused_AndNothingIsSaved()
    {
        using var db = new SqliteHrmsContext();
        await using (var context = db.NewContext())
        {
            var sut = BuildService(new UnitOfWorkService(context));
            using var file = BuildWorkbook(("4001", "Complete", "Row", null), ("4002", "NoLastName", "  ", null));

            var act = () => sut.Upload(file, CancellationToken.None);

            (await act.Should().ThrowAsync<ValidationException>())
                .Which.Message.Should().Contain("Row 2: Last name is required.");
        }

        await using var verify = db.NewContext();
        (await verify.Employees.AnyAsync()).Should().BeFalse();
    }

    // The client's Errors list isn't trusted for names: a stale or tampered preview payload with a
    // nameless row (and no errors on it) still imports nothing.
    [Fact]
    public async Task CommitPreview_RowMissingAName_IsRefused_EvenWithoutErrorsOnIt()
    {
        using var db = new SqliteHrmsContext();
        await using (var context = db.NewContext())
        {
            var sut = BuildService(new UnitOfWorkService(context));
            var rows = new List<EmployeeImportPreviewRow>
            {
                new() { RowNumber = 1, FirstName = "Complete", LastName = "Row", Errors = [] },
                new() { RowNumber = 2, FirstName = null, LastName = "NoFirstName", Errors = [] },
            };

            var act = () => sut.CommitPreviewAsync(rows, CancellationToken.None);

            (await act.Should().ThrowAsync<ValidationException>())
                .Which.Message.Should().Contain("Row 2: First name is required.");
        }

        await using var verify = db.NewContext();
        (await verify.Employees.AnyAsync()).Should().BeFalse();
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
