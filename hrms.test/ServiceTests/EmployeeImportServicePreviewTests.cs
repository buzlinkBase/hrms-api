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
}
