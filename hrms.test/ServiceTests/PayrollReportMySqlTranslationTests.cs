using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Infrastructure;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>
/// The payroll reports now group and sum in SQL. SQLite (PayrollReportSnapshotTests) proves the
/// numbers; this proves Pomelo can translate every rewritten query for MySQL, which production
/// runs on. Each report runs against a MySQL-configured context pointing at a closed port: EF
/// translates the LINQ before it opens a connection, so a translation problem surfaces as EF's
/// "could not be translated" error, while a translatable query fails only with the expected
/// connection error.
/// </summary>
public class PayrollReportMySqlTranslationTests
{
    private const string Unreachable = "Server=127.0.0.1;Port=1;Database=x;User=x;Password=x;Connect Timeout=1";

    private static PayrollReportService BuildService()
    {
        var options = new DbContextOptionsBuilder<HrmsContext>()
            .UseMySql(Unreachable, new MySqlServerVersion(new Version(8, 4, 8)), x => x.UseNetTopologySuite())
            .Options;
        var context = new HrmsContext(options, null!, new FixedTenant(Guid.NewGuid()), null!);
        var uow = Substitute.For<IUnitOfWorkService>();
        uow.Context.Returns(context);
        uow.Repository.Returns(new Repository(context));

        // Private Mapster config -- see PayrollReportSnapshotTests.BuildService.
        var employeeService = new EmployeeService(
            uow, Substitute.For<IMapper>(), new TypeAdapterConfig(),
            new DepartmentService(uow), new PayrollGroupService(uow), new BranchService(uow),
            new CostCenterService(uow), new PositionService(uow), new SectionService(uow));
        return new PayrollReportService(uow, employeeService, new PayrollOpeningBalanceService(uow));
    }

    public static IEnumerable<object[]> Reports() =>
    [
        ["bank"], ["cost-client"], ["cost-department"], ["cost-branch"], ["ytd"], ["13th"], ["tax"],
        ["1601c"], ["alphalist"], ["2316"], ["deduction-ledger"], ["reimbursement"],
    ];

    [Theory]
    [MemberData(nameof(Reports))]
    public async Task Report_TranslatesForMySql(string report)
    {
        var service = BuildService();
        Func<Task> act = report switch
        {
            "bank" => () => service.GetBankDisbursementAsync(new(2026, 1, 1), new(2026, 1, 31), default),
            "cost-client" => () => service.GetCostSummaryAsync(new(2026, 1, 1), new(2026, 1, 31), "client", default),
            "cost-department" => () => service.GetCostSummaryAsync(new(2026, 1, 1), new(2026, 1, 31), "department", default),
            "cost-branch" => () => service.GetCostSummaryAsync(new(2026, 1, 1), new(2026, 1, 31), "branch", default),
            "ytd" => () => service.GetYtdSummaryAsync(2026, null, default),
            "13th" => () => service.GetThirteenthMonthAsync(2026, default, asOfDate: new DateOnly(2026, 6, 30)),
            "tax" => () => service.GetAnnualTaxAnnualizationInputsAsync(2026, default),
            "1601c" => () => service.GetMonthlyRemittanceReturnAsync(new(2026, 1, 1), new(2026, 1, 31), false, default),
            "alphalist" => () => service.GetAlphalistAsync(2026, default),
            "2316" => () => service.Get2316DataAsync(Guid.NewGuid(), 2026, default),
            "deduction-ledger" => () => service.GetDeductionLedgerAsync(new(2026, 1, 31), default),
            "reimbursement" => () => service.GetReimbursementListAsync(new(2026, 1, 1), new(2026, 1, 31), default),
            _ => throw new ArgumentOutOfRangeException(nameof(report)),
        };

        var thrown = await Record.ExceptionAsync(act);

        thrown.Should().NotBeNull("there is no database to reach");
        // Pomelo wraps the connection failure in an InvalidOperationException ("transient
        // failure"), so check for EF's translation failure specifically, not the exception type.
        AllMessages(thrown!).Should().NotContain("could not be translated",
            "every rewritten report query must translate to MySQL SQL");
        // Reaching the connection step proves the query was translated first. The wording depends
        // on whether the driver's pool has already seen the host fail.
        AllMessages(thrown!).Should().MatchRegex("(?i)transient failure|unable to connect|connect",
            "the only expected failure is the unreachable database");
    }

    private static string AllMessages(Exception e) =>
        e.InnerException == null ? e.Message : e.Message + " | " + AllMessages(e.InnerException);

    private sealed class FixedTenant(Guid tenantId) : ITenantProvider
    {
        public Guid TenantId { get; } = tenantId;
        public void SetTenantId(Guid newTenantId) { }
    }
}
