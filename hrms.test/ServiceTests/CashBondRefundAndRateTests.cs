using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Infrastructure;
using hrms.test.TestSupport;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>
/// Last Pay pays back the cash bond collected so far (and the Cash Bond reports then show it as
/// refunded), and HR can change the per-run rate with every change kept in a history that never
/// moves the collected total.
/// </summary>
public class CashBondRefundAndRateTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly DateOnly Sep30 = new(2026, 9, 30);

    private static async Task<Guid> SeedEmployeeAsync(SqliteHrmsContext db, decimal rate = 100m)
    {
        await using var seed = db.NewContext(CompanyId);
        var group = new PayrollGroup { Id = Guid.NewGuid(), TenantId = CompanyId };
        var employee = new Employee
        {
            Id = Guid.NewGuid(), TenantId = CompanyId, PayrollGroupId = group.Id,
            FirstName = "Ana", LastName = "Reyes", EmployeeNo = "EMP-001", CashBond = rate,
        };
        seed.PayrollGroups.Add(group);
        seed.Employees.Add(employee);
        await seed.SaveChangesAsync();
        return employee.Id;
    }

    private static async Task SeedPayrollAsync(
        SqliteHrmsContext db, Guid employeeId, DateOnly periodEnd, decimal cashBond = 0m, decimal refund = 0m)
    {
        await using var seed = db.NewContext(CompanyId);
        seed.Payrolls.Add(new Payroll
        {
            Id = Guid.NewGuid(), TenantId = CompanyId, EmployeeId = employeeId,
            PayPeriodStart = periodEnd.AddDays(-14), PayPeriodEnd = periodEnd,
            CashBondDeduction = cashBond, CashBondRefund = refund,
        });
        await seed.SaveChangesAsync();
    }

    // No current company -- see CashBondAdjustmentTests.WithServiceAsync.
    private static async Task<T> WithServiceAsync<T>(SqliteHrmsContext db, Func<PayrollReportService, Task<T>> action)
    {
        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var employees = new EmployeeService(uow, Substitute.For<IMapper>(), new TypeAdapterConfig(), null!, null!, null!, null!, null!, null!);
        return await action(new PayrollReportService(uow, employees, null!));
    }

    [Theory]
    [InlineData(true, 1_500, 1_500)]
    [InlineData(true, 0, 0)]
    [InlineData(true, -200, 0)]
    [InlineData(false, 1_500, 0)]
    public void TheRefund_IsEverythingCollected_NeverNegative_AndZeroWhenLeftOff(bool include, int collected, int expected) =>
        LastPayrollService.ComputeCashBondRefund(include, collected).Should().Be(expected);

    [Fact]
    public async Task ARefund_ReducesTheCollectedTotal_AndIsShownAsRefunded()
    {
        using var db = new SqliteHrmsContext();
        var employeeId = await SeedEmployeeAsync(db);
        await SeedPayrollAsync(db, employeeId, new DateOnly(2026, 9, 15), cashBond: 100m);
        await SeedPayrollAsync(db, employeeId, new DateOnly(2026, 9, 29), cashBond: 100m);

        var before = (await WithServiceAsync(db, s => s.GetCashBondReportAsync(Sep30, CancellationToken.None))).Single();
        before.TotalCollected.Should().Be(200m);

        await SeedPayrollAsync(db, employeeId, Sep30, refund: before.TotalCollected);

        var after = (await WithServiceAsync(db, s => s.GetCashBondReportAsync(Sep30, CancellationToken.None))).Single();
        after.CollectedFromPayroll.Should().Be(200m);
        after.Refunded.Should().Be(200m);
        after.TotalCollected.Should().Be(0m, "a refunded bond must not be refundable a second time");
        after.PayrollRunsCount.Should().Be(2, "the Last Pay is not a deduction run");
    }

    [Fact]
    public async Task ARefund_AppearsAsANegativeEntryInTheLedger()
    {
        using var db = new SqliteHrmsContext();
        var employeeId = await SeedEmployeeAsync(db);
        await SeedPayrollAsync(db, employeeId, new DateOnly(2026, 9, 15), cashBond: 100m);
        await SeedPayrollAsync(db, employeeId, Sep30, refund: 100m);

        var entries = await WithServiceAsync(db, s => s.GetCashBondEntriesAsync(employeeId, Sep30, CancellationToken.None));

        entries.Select(x => (x.EntryType, x.Amount, x.Balance)).Should().Equal(
            ("Payroll", 100m, 100m),
            ("Refund", -100m, 0m));
    }

    [Fact]
    public async Task MyCashBond_NetsTheRefundOffTheTotal()
    {
        using var db = new SqliteHrmsContext();
        var employeeId = await SeedEmployeeAsync(db);
        await SeedPayrollAsync(db, employeeId, new DateOnly(2026, 9, 15), cashBond: 100m);
        await SeedPayrollAsync(db, employeeId, Sep30, refund: 100m);

        var mine = await WithServiceAsync(db, s => s.GetMyCashBondAsync(employeeId, CancellationToken.None));

        mine.TotalCollected.Should().Be(0m);
        mine.Refunded.Should().Be(100m);
        mine.Runs.Select(x => x.Amount).Should().BeEquivalentTo(new[] { 100m, -100m });
    }

    [Fact]
    public async Task ChangingTheRate_UpdatesTheEmployee_AndKeepsAHistoryRow()
    {
        using var db = new SqliteHrmsContext();
        var employeeId = await SeedEmployeeAsync(db, rate: 100m);

        var change = await WithServiceAsync(db, s => s.ChangeCashBondRateAsync(employeeId, 250m, "  Raised after review ", CancellationToken.None));

        change.Should().NotBeNull();
        change!.OldRate.Should().Be(100m);
        change.NewRate.Should().Be(250m);
        change.Reason.Should().Be("Raised after review");

        await using var verify = db.NewContext();
        (await verify.Employees.AsNoTracking().SingleAsync(x => x.Id == employeeId)).CashBond.Should().Be(250m);
        var history = await WithServiceAsync(db, s => s.GetCashBondRateHistoryAsync(employeeId, CancellationToken.None));
        history.Should().ContainSingle().Which.NewRate.Should().Be(250m);
    }

    [Fact]
    public async Task ChangingTheRate_NeverMovesTheCollectedTotal()
    {
        using var db = new SqliteHrmsContext();
        var employeeId = await SeedEmployeeAsync(db, rate: 100m);
        await SeedPayrollAsync(db, employeeId, new DateOnly(2026, 9, 15), cashBond: 100m);

        await WithServiceAsync(db, s => s.ChangeCashBondRateAsync(employeeId, 500m, "Raise", CancellationToken.None));

        var row = (await WithServiceAsync(db, s => s.GetCashBondReportAsync(Sep30, CancellationToken.None))).Single();
        row.TotalCollected.Should().Be(100m);
        row.Adjustments.Should().Be(0m);
        row.CashBondRate.Should().Be(500m);
    }

    [Fact]
    public async Task AnUnchangedRate_WritesNoHistory()
    {
        using var db = new SqliteHrmsContext();
        var employeeId = await SeedEmployeeAsync(db, rate: 100m);

        var change = await WithServiceAsync(db, s => s.ChangeCashBondRateAsync(employeeId, 100m, "Same", CancellationToken.None));

        change.Should().BeNull();
        (await WithServiceAsync(db, s => s.GetCashBondRateHistoryAsync(employeeId, CancellationToken.None))).Should().BeEmpty();
    }

    [Fact]
    public async Task ANegativeRate_IsRejected()
    {
        using var db = new SqliteHrmsContext();
        var employeeId = await SeedEmployeeAsync(db);

        var act = () => WithServiceAsync(db, s => s.ChangeCashBondRateAsync(employeeId, -1m, "Oops", CancellationToken.None));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void TheSharedRule_LogsOnlyARealChange()
    {
        var id = Guid.NewGuid();

        CashBondRateChange.Between(id, 100m, 100m, "x").Should().BeNull();
        var change = CashBondRateChange.Between(id, 100m, 0m, null);
        change.Should().NotBeNull();
        change!.EmployeeId.Should().Be(id);
        change.Reason.Should().BeEmpty();
    }
}
