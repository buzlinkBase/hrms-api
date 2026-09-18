using ClosedXML.Excel;
using Hrms.Domain.Entities.EmployeeEntities;
using Mapster;
using MapsterMapper;
using MockQueryable.NSubstitute;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>
/// EmployeeImportService.PreviewAsync -- the read-only counterpart to Upload (mirrors
/// TaxAnnualizationService's Preview/Generate split): parses and defaults the file exactly the
/// same way, flags the same per-row BioId conflicts Upload's ValidateImportData would throw on,
/// but must never write anything, so a user can see what an import would do before committing.
/// </summary>
public class EmployeeImportServicePreviewTests
{
    // Ganss.Excel's HeaderRowNumber/MinRowNumber are 0-indexed (default 0) -- production's
    // HeaderRowNumber = 1, MinRowNumber = 2 (EmployeeImportService.ParseAndDefaultAsync) means
    // headers on PHYSICAL row 2 and data starting PHYSICAL row 3, so the fixture has to match
    // that, not a naive "row 1 = headers" reading of the numbers.
    private static MemoryStream BuildWorkbook(params (string BioId, string FirstName, string LastName)[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sheet1");
        sheet.Cell(2, 1).Value = "BioId";
        sheet.Cell(2, 2).Value = "FirstName";
        sheet.Cell(2, 3).Value = "MiddleName";
        sheet.Cell(2, 4).Value = "LastName";
        sheet.Cell(2, 5).Value = "Suffix";

        for (var i = 0; i < rows.Length; i++)
        {
            var r = rows[i];
            var row = i + 3;
            sheet.Cell(row, 1).Value = r.BioId;
            sheet.Cell(row, 2).Value = r.FirstName;
            sheet.Cell(row, 4).Value = r.LastName;
        }

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    private static EmployeeImportService BuildService(IRepository repo, IUnitOfWorkService uow, params BasicEmployeeInfo[] dbEmployees)
    {
        repo.FindAll<Employee>().Returns(_ => dbEmployees
            .Select(e => new Employee
            {
                Id = e.Id,
                BioId = e.BioId,
                FirstName = e.FirstName,
                MiddleName = e.MiddleName,
                LastName = e.LastName,
                Suffix = e.Suffix,
                Skills = [],
                Educations = [],
                Dependents = [],
                EmployeeRecords = [],
                Employments = [],
                Assets = [],
                RestDays = [],
            })
            .ToList()
            .BuildMockDbSet());

        uow.Repository.Returns(repo);

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
            new BranchService(uow));
    }

    [Fact]
    public async Task PreviewAsync_ReturnsOneRowPerDataRow_WithParsedFieldValues()
    {
        var repo = Substitute.For<IRepository>();
        var sut = BuildService(repo, Substitute.For<IUnitOfWorkService>());
        using var file = BuildWorkbook(("1001", "Juan", "DelaCruz"), ("1003", "Maria", "Santos"));

        var result = await sut.PreviewAsync(file, CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].RowNumber.Should().Be(1);
        result[0].BioId.Should().Be("1001");
        result[0].FirstName.Should().Be("Juan");
        result[0].LastName.Should().Be("DelaCruz");
        result[1].RowNumber.Should().Be(2);
        result.SelectMany(r => r.Errors).Should().BeEmpty();
    }

    [Fact]
    public async Task PreviewAsync_FlagsDuplicateBioIdsWithinTheFile_OnBothRows()
    {
        var repo = Substitute.For<IRepository>();
        var sut = BuildService(repo, Substitute.For<IUnitOfWorkService>());
        using var file = BuildWorkbook(("1001", "Juan", "DelaCruz"), ("1001", "Pedro", "Reyes"));

        var result = await sut.PreviewAsync(file, CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Errors.Should().ContainSingle(e => e.Contains("more than one row"));
        result[1].Errors.Should().ContainSingle(e => e.Contains("more than one row"));
    }

    [Fact]
    public async Task PreviewAsync_FlagsBioIdAlreadyRegisteredToADifferentName()
    {
        var existing = new BasicEmployeeInfo { Id = Guid.NewGuid(), BioId = 2002, FirstName = "Existing", MiddleName = "", LastName = "Person", Suffix = "" };
        var repo = Substitute.For<IRepository>();
        var sut = BuildService(repo, Substitute.For<IUnitOfWorkService>(), existing);
        using var file = BuildWorkbook(("2002", "Different", "Name"));

        var result = await sut.PreviewAsync(file, CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Errors.Should().ContainSingle(e => e.Contains("already registered"));
    }

    /// <summary>
    /// Regression guard for a production 500: a phone number typed into the "Date Birth" column
    /// (a numeric cell, so ExcelMapper tries to convert the raw double straight to DateTime and
    /// throws) used to abort Fetch for the WHOLE file. ParseAndDefaultAsync now cancels that
    /// exception via ExcelMapper.ErrorParsingCell instead, and PreviewAsync surfaces it as a
    /// normal per-row error on just that row -- every other row still comes through clean.
    /// </summary>
    [Fact]
    public async Task PreviewAsync_DoesNotThrow_WhenACellHasAnUnconvertibleValue()
    {
        var repo = Substitute.For<IRepository>();
        var sut = BuildService(repo, Substitute.For<IUnitOfWorkService>());

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sheet1");
        sheet.Cell(2, 1).Value = "BioId";
        sheet.Cell(2, 2).Value = "FirstName";
        sheet.Cell(2, 3).Value = "MiddleName";
        sheet.Cell(2, 4).Value = "LastName";
        sheet.Cell(2, 5).Value = "Suffix";
        sheet.Cell(2, 6).Value = "Date Birth";

        sheet.Cell(3, 1).Value = "1001";
        sheet.Cell(3, 2).Value = "Juan";
        sheet.Cell(3, 4).Value = "DelaCruz";
        sheet.Cell(3, 6).Value = 9279334762; // a phone number, not a date -- numeric cell

        sheet.Cell(4, 1).Value = "1002";
        sheet.Cell(4, 2).Value = "Maria";
        sheet.Cell(4, 4).Value = "Santos";

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var act = () => sut.PreviewAsync(stream, CancellationToken.None);

        var result = await act.Should().NotThrowAsync();
        result.Subject.Should().HaveCount(2);
        result.Subject[0].Errors.Should().ContainSingle(e => e.Contains("not a valid date"));
        result.Subject[0].DateOfBirth.Should().BeNull();
        result.Subject[1].Errors.Should().BeEmpty();
    }

    /// <summary>
    /// Regression guard: AMIn/AmOut/PMIn/PMOut are read as plain strings, which used to mean
    /// NPOI rendered the raw numeric time value through its own number-format lookup -- for a
    /// handful of Excel's built-in time formats NPOI has never implemented, that lookup fails
    /// and produces a literal "reserved-0x.." placeholder instead of the actual time. ConvertTimeCell
    /// now bypasses that lookup entirely for these 4 columns and computes the time straight from
    /// the cell's numeric (fraction-of-a-day) value, so this must come through correctly
    /// regardless of whatever number format happens to be on the cell.
    /// </summary>
    [Fact]
    public async Task PreviewAsync_ReadsTimeColumns_FromRawNumericCellValue()
    {
        var repo = Substitute.For<IRepository>();
        var sut = BuildService(repo, Substitute.For<IUnitOfWorkService>());

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sheet1");
        sheet.Cell(2, 1).Value = "BioId";
        sheet.Cell(2, 2).Value = "FirstName";
        sheet.Cell(2, 3).Value = "MiddleName";
        sheet.Cell(2, 4).Value = "LastName";
        sheet.Cell(2, 5).Value = "Suffix";
        sheet.Cell(2, 6).Value = "AMIN";
        sheet.Cell(2, 7).Value = "PM OUT";

        sheet.Cell(3, 1).Value = "1001";
        sheet.Cell(3, 2).Value = "Juan";
        sheet.Cell(3, 4).Value = "DelaCruz";
        sheet.Cell(3, 6).Value = 7.0 / 24; // 07:00:00, no time-formatted number style applied
        sheet.Cell(3, 7).Value = 15.0 / 24; // 15:00:00

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var result = await sut.PreviewAsync(stream, CancellationToken.None);

        result.Should().ContainSingle();
        result[0].AMIn.Should().Be("07:00:00");
        result[0].PMOut.Should().Be("15:00:00");
    }

    /// <summary>
    /// Regression guard: HireDate is DateOnly (unlike DateOfBirth, which is DateTime), and
    /// ExcelMapper has no built-in conversion to DateOnly at all -- every row with a real date in
    /// that column used to throw and get wiped, whether the cell held a genuine Excel date serial
    /// (numeric) or a typed date string. ConvertCellValue now handles both directly.
    /// </summary>
    [Fact]
    public async Task PreviewAsync_ReadsHireDate_FromNumericAndTextCells()
    {
        var repo = Substitute.For<IRepository>();
        var sut = BuildService(repo, Substitute.For<IUnitOfWorkService>());

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sheet1");
        sheet.Cell(2, 1).Value = "BioId";
        sheet.Cell(2, 2).Value = "FirstName";
        sheet.Cell(2, 3).Value = "MiddleName";
        sheet.Cell(2, 4).Value = "LastName";
        sheet.Cell(2, 5).Value = "Suffix";
        sheet.Cell(2, 6).Value = "Hire Date";

        sheet.Cell(3, 1).Value = "1001";
        sheet.Cell(3, 2).Value = "Juan";
        sheet.Cell(3, 4).Value = "DelaCruz";
        sheet.Cell(3, 6).Value = new DateTime(2025, 4, 16); // genuine Excel date serial

        sheet.Cell(4, 1).Value = "1002";
        sheet.Cell(4, 2).Value = "Maria";
        sheet.Cell(4, 4).Value = "Santos";
        sheet.Cell(4, 6).Value = "04/16/2025"; // typed as text, not a real Excel date

        sheet.Cell(5, 1).Value = "1003";
        sheet.Cell(5, 2).Value = "Pedro";
        sheet.Cell(5, 4).Value = "Reyes";
        sheet.Cell(5, 6).Value = "Februay 28, 2025"; // genuine typo -- must still be flagged

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var result = await sut.PreviewAsync(stream, CancellationToken.None);

        result.Should().HaveCount(3);
        result[0].HireDate.Should().Be(new DateOnly(2025, 4, 16));
        result[0].Errors.Should().BeEmpty();
        result[1].HireDate.Should().Be(new DateOnly(2025, 4, 16));
        result[1].Errors.Should().BeEmpty();
        result[2].Errors.Should().ContainSingle(e => e.Contains("not a valid DateOnly"));
    }

    [Fact]
    public async Task PreviewAsync_NeverWritesToTheDatabase()
    {
        var existing = new BasicEmployeeInfo { Id = Guid.NewGuid(), BioId = 2002, FirstName = "Existing", MiddleName = "", LastName = "Person", Suffix = "" };
        var repo = Substitute.For<IRepository>();
        var uow = Substitute.For<IUnitOfWorkService>();
        var sut = BuildService(repo, uow, existing);
        using var file = BuildWorkbook(("1001", "Juan", "DelaCruz"), ("2002", "Different", "Name"));

        await sut.PreviewAsync(file, CancellationToken.None);

        await repo.DidNotReceive().AddAsync(Arg.Any<Employee>(), Arg.Any<CancellationToken>());
        await repo.DidNotReceive().AddRangeAsync(Arg.Any<IEnumerable<Employee>>(), Arg.Any<CancellationToken>());
        await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await uow.DidNotReceive().CommitChangesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // BuildPayrollGroupName is private (ExtractPayrollGroups/GetPayrollGroupKey both delegate to
    // it internally) -- PreviewAsync's own output doesn't expose the formatted group name (it
    // only carries the raw, as-entered PayrollGroup string), and exercising the full
    // Upload/PersistAsync write path to observe it would need real EF test infrastructure this
    // suite doesn't set up for EmployeeImportService yet. Reflection keeps this test targeted at
    // exactly the new naming rule without taking on that larger setup.
    private static string BuildPayrollGroupName(string? baseName, params (int Day, bool IsEndOfMonth)[] cutoffs)
    {
        var method = typeof(EmployeeImportService).GetMethod(
            "BuildPayrollGroupName",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (string)method.Invoke(null, [baseName, cutoffs])!;
    }

    [Theory]
    [InlineData("Semi-Monthly", 10, false, 25, false, "Semi-Monthly-10-25")]
    [InlineData("Executive", 15, false, 0, false, "Executive-15")]
    [InlineData("", 5, false, 20, false, "--5-20")]
    public void BuildPayrollGroupName_JoinsBaseNameAndActiveCutoffsWithHyphens(
        string baseName, int cutoff1, bool eom1, int cutoff2, bool eom2, string expected)
    {
        var result = BuildPayrollGroupName(baseName, (cutoff1, eom1), (cutoff2, eom2));

        result.Should().Be(expected);
    }

    [Fact]
    public void BuildPayrollGroupName_MarksEndOfMonthCutoffsDistinctlyFromFixedDayCutoffs()
    {
        var fixedDay = BuildPayrollGroupName("Semi-Monthly", (10, false), (25, false));
        var endOfMonth = BuildPayrollGroupName("Semi-Monthly", (10, false), (25, true));

        endOfMonth.Should().NotBe(fixedDay);
        endOfMonth.Should().Be("Semi-Monthly-10-25EOM");
    }

    [Fact]
    public async Task CommitPreviewAsync_WhenEveryRowStillHasErrors_DoesNothing()
    {
        // The frontend disables its own Confirm while any row has errors, so the only way this
        // is reached with error rows still present is a stale/tampered payload -- must not
        // attempt to persist anything in that case.
        var repo = Substitute.For<IRepository>();
        var uow = Substitute.For<IUnitOfWorkService>();
        var sut = BuildService(repo, uow);
        var rows = new List<EmployeeImportPreviewRow>
        {
            new() { RowNumber = 1, BioId = "1001", FirstName = "Juan", LastName = "DelaCruz", Errors = ["Some conflict"] },
        };

        await sut.CommitPreviewAsync(rows, CancellationToken.None);

        await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await uow.DidNotReceive().CommitChangesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
