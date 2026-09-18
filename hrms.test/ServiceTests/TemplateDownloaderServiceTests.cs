using ClosedXML.Excel;
using Microsoft.AspNetCore.Hosting;
using MockQueryable.NSubstitute;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>
/// TemplateDownloaderService.GetEmployeeTemplate — regression guard for a bug where several
/// ClosedXML cell references were missing their row number (e.g. "N" instead of "N3"),
/// throwing "'N' is not A1 address or workbook named range" the moment the endpoint was hit,
/// since ClosedXML's Cell(string) requires a full A1-style address, not a bare column letter.
/// </summary>
public class TemplateDownloaderServiceTests
{
    private static TemplateDownloaderService BuildService(
        List<Branch>? branches = null, List<PayrollGroup>? payrollGroups = null, List<Department>? departments = null)
    {
        var repo = Substitute.For<IRepository>();
        repo.FindAll<Branch>().Returns(_ => (branches ?? []).BuildMockDbSet());
        repo.FindAll<PayrollGroup>().Returns(_ => (payrollGroups ?? []).BuildMockDbSet());
        repo.FindAll<Department>().Returns(_ => (departments ?? []).BuildMockDbSet());

        var uow = Substitute.For<IUnitOfWorkService>();
        uow.Repository.Returns(repo);

        var environment = Substitute.For<IWebHostEnvironment>();
        // GetEmployeeTemplate reads the real wwwroot/Templates/employee_template.xlsx shipped
        // with the app -- pointing ContentRootPath at the actual hrms-api project directory
        // exercises the real file, same as production, rather than needing a fixture copy.
        environment.ContentRootPath.Returns(
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "hrms-api")));

        return new TemplateDownloaderService(environment, new BranchService(uow), new DepartmentService(uow), new PayrollGroupService(uow));
    }

    [Fact]
    public async Task GetEmployeeTemplate_DoesNotThrow_AndProducesAReadableWorkbook()
    {
        var sut = BuildService(
            branches: [new Branch { Code = "MAIN" }],
            payrollGroups: [new PayrollGroup { Name = "Semi-Monthly" }]);

        var act = () => sut.GetEmployeeTemplate(CancellationToken.None);

        var stream = await act.Should().NotThrowAsync();
        stream.Subject.Length.Should().BeGreaterThan(0);

        // Round-trips the produced stream back through ClosedXML -- if any cell reference were
        // still malformed, either the write above or this read would throw.
        stream.Subject.Position = 0;
        using var workbook = new XLWorkbook(stream.Subject);
        workbook.Worksheet(1).Cell("N3").GetString().Should().Be("False");
    }

    [Fact]
    public async Task GetEmployeeTemplate_RestDayColumns_HaveDayNameDropdownWithBlankOption()
    {
        var sut = BuildService(
            branches: [new Branch { Code = "MAIN" }],
            payrollGroups: [new PayrollGroup { Name = "Semi-Monthly" }]);

        var stream = await sut.GetEmployeeTemplate(CancellationToken.None);
        stream.Position = 0;
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheet(1);

        // Sample row still shows an example day, but the underlying dropdown source (the hidden
        // "RestDays" helper sheet) includes a leading blank entry so real rows for employees
        // without a rest day can clear the cell instead of being forced to pick a day.
        worksheet.Cell("H3").GetString().Should().Be("Saturday");
        worksheet.Cell("I3").GetString().Should().Be("Sunday");

        var restDaySheet = workbook.Worksheet("RestDays");
        restDaySheet.Cell(1, 1).GetString().Should().BeEmpty();
        Enumerable.Range(2, 7)
            .Select(row => restDaySheet.Cell(row, 1).GetString())
            .Should().BeEquivalentTo(Enum.GetNames(typeof(DayName)));
    }

    [Fact]
    public async Task GetEmployeeTemplateWithErrors_WritesOnlyErrorRows_WithMessagesAndNoStaleSampleData()
    {
        var sut = BuildService(
            branches: [new Branch { Code = "MAIN" }],
            payrollGroups: [new PayrollGroup { Name = "Semi-Monthly" }]);

        var rows = new List<EmployeeImportPreviewRow>
        {
            new() { RowNumber = 1, BioId = "1001", FirstName = "Juan", LastName = "DelaCruz", Errors = [] },
            new() { RowNumber = 2, BioId = "1002", FirstName = "Maria", LastName = "Santos", Errors = ["BioId 1002 is used by more than one row in this file."] },
        };

        var stream = await sut.GetEmployeeTemplateWithErrors(rows, CancellationToken.None);
        stream.Position = 0;
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheet(1);

        // Real headers live on row 2 (row 1 is the "Instructions" banner) -- same header text
        // MapFields reads on upload -- plus the new trailing "Import Errors" column.
        var headerByText = Enumerable.Range(1, worksheet.Row(2).LastCellUsed()!.Address.ColumnNumber)
            .ToDictionary(col => worksheet.Cell(2, col).GetString(), col => col);
        headerByText.Should().ContainKey("Import Errors");

        // Only the one row with Errors is written, starting at row 3 (the template's own "real
        // data starts here" row) -- the clean row is skipped entirely.
        worksheet.Cell(3, headerByText["BioId"]).GetString().Should().Be("1002");
        worksheet.Cell(3, headerByText["FirstName"]).GetString().Should().Be("Maria");
        worksheet.Cell(3, headerByText["Import Errors"]).GetString().Should().Contain("more than one row");

        // Row 4 (part of the blank template's own cutoff-example reference table) must be
        // cleared, not left behind as a phantom row a re-upload would misread as a real employee.
        worksheet.Cell(4, headerByText["BioId"]).GetString().Should().BeEmpty();
    }

    /// <summary>
    /// Regression guard: fieldWriters' header text must match the real template's row-2 headers
    /// exactly (they previously drifted -- "Cut-Off1"/"EOM1" instead of "Cut-Off 1"/"EOM 1", and
    /// Civil Status/Blood Type/Address 1/Address 2/SalaryType/Monthly Rate had no writer at all --
    /// so TryGetValue silently no-op'd and those columns never made it into the re-download).
    /// </summary>
    [Fact]
    public async Task GetEmployeeTemplateWithErrors_WritesEveryTemplateColumn()
    {
        var sut = BuildService(
            branches: [new Branch { Code = "MAIN" }],
            payrollGroups: [new PayrollGroup { Name = "Semi-Monthly" }]);

        var rows = new List<EmployeeImportPreviewRow>
        {
            new()
            {
                RowNumber = 1,
                BioId = "1001",
                FirstName = "Juan",
                LastName = "DelaCruz",
                Cutoff1 = 15,
                Cutoff4 = 30,
                EOM4 = true,
                DateOfBirth = new DateTime(1990, 5, 20),
                CivilStatus = "Single",
                BloodType = "O+",
                Address1 = "123 Main St",
                Address2 = "Brgy. Sample",
                SalaryType = "Fixed",
                MonthlyRate = 25000,
                Errors = ["Missing department"],
            },
        };

        var stream = await sut.GetEmployeeTemplateWithErrors(rows, CancellationToken.None);
        stream.Position = 0;
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheet(1);
        var headerByText = Enumerable.Range(1, worksheet.Row(2).LastCellUsed()!.Address.ColumnNumber)
            .ToDictionary(col => worksheet.Cell(2, col).GetString(), col => col);

        worksheet.Cell(3, headerByText["Cut-Off 1"]).GetValue<int>().Should().Be(15);
        worksheet.Cell(3, headerByText["Cut-Off 4"]).GetValue<int>().Should().Be(30);
        worksheet.Cell(3, headerByText["EOM 4"]).GetValue<bool>().Should().BeTrue();
        worksheet.Cell(3, headerByText["Date Birth"]).GetDateTime().Should().Be(new DateTime(1990, 5, 20));
        worksheet.Cell(3, headerByText["Civil Status"]).GetString().Should().Be("Single");
        worksheet.Cell(3, headerByText["Blood Type"]).GetString().Should().Be("O+");
        worksheet.Cell(3, headerByText["Address 1"]).GetString().Should().Be("123 Main St");
        worksheet.Cell(3, headerByText["Address 2"]).GetString().Should().Be("Brgy. Sample");
        worksheet.Cell(3, headerByText["SalaryType"]).GetString().Should().Be("Fixed");
        worksheet.Cell(3, headerByText["Monthly Rate"]).GetValue<decimal>().Should().Be(25000);
    }
}
