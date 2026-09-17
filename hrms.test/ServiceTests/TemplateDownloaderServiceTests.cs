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
    private static TemplateDownloaderService BuildService(List<Branch>? branches = null, List<PayrollGroup>? payrollGroups = null)
    {
        var repo = Substitute.For<IRepository>();
        repo.FindAll<Branch>().Returns(_ => (branches ?? []).BuildMockDbSet());
        repo.FindAll<PayrollGroup>().Returns(_ => (payrollGroups ?? []).BuildMockDbSet());

        var uow = Substitute.For<IUnitOfWorkService>();
        uow.Repository.Returns(repo);

        var environment = Substitute.For<IWebHostEnvironment>();
        // GetEmployeeTemplate reads the real wwwroot/Templates/employee_template.xlsx shipped
        // with the app -- pointing ContentRootPath at the actual hrms-api project directory
        // exercises the real file, same as production, rather than needing a fixture copy.
        environment.ContentRootPath.Returns(
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "hrms-api")));

        return new TemplateDownloaderService(environment, new BranchService(uow), new PayrollGroupService(uow));
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
}
