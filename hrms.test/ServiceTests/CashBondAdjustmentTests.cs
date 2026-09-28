using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Infrastructure;
using hrms.test.TestSupport;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>
/// Cash Bond Tracking's opening balance: HR can enter cash bond already deducted before this
/// system (or correct it), and that amount counts everywhere the collected total is used -- the
/// report (as of a date), the employee's My Cash Bond and Last Pay's status line (same report).
/// </summary>
public class CashBondAdjustmentTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly DateOnly Jan1 = new(2026, 1, 1);

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

    private static async Task SeedPayrollAsync(SqliteHrmsContext db, Guid employeeId, DateOnly periodEnd, decimal cashBond)
    {
        await using var seed = db.NewContext(CompanyId);
        seed.Payrolls.Add(new Payroll
        {
            Id = Guid.NewGuid(), TenantId = CompanyId, EmployeeId = employeeId,
            PayPeriodStart = periodEnd.AddDays(-14), PayPeriodEnd = periodEnd, CashBondDeduction = cashBond,
        });
        await seed.SaveChangesAsync();
    }

    // No current company: production's ApplyTenantInterceptor (not part of the test context)
    // stamps new rows with the company; without it they'd be saved company-less and hidden by the
    // company filter. An empty current company applies no filter, so both are visible here.
    private static async Task<T> WithServiceAsync<T>(SqliteHrmsContext db, Func<PayrollReportService, Task<T>> action)
    {
        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var employees = new EmployeeService(uow, Substitute.For<IMapper>(), new TypeAdapterConfig(), null!, null!, null!, null!, null!, null!);
        return await action(new PayrollReportService(uow, employees, null!));
    }

    private static Task AdjustAsync(SqliteHrmsContext db, Guid employeeId, decimal amount, bool isAddition, DateOnly entryDate) =>
        WithServiceAsync(db, async s =>
        {
            await s.AdjustCashBondAsync(employeeId, amount, isAddition, "Opening balance from previous system", entryDate, CancellationToken.None);
            return true;
        });

    private static Task<List<CashBondReportModel>> ReportAsync(SqliteHrmsContext db, DateOnly asOf) =>
        WithServiceAsync(db, s => s.GetCashBondReportAsync(asOf, CancellationToken.None));

    [Fact]
    public async Task OpeningBalance_ShowsForAnEmployeeWithNoPayrollYet()
    {
        using var db = new SqliteHrmsContext();
        var employeeId = await SeedEmployeeAsync(db);

        await AdjustAsync(db, employeeId, 2_500m, isAddition: true, Jan1);

        var row = (await ReportAsync(db, new DateOnly(2026, 9, 30))).Should().ContainSingle().Subject;
        row.EmployeeId.Should().Be(employeeId);
        row.Adjustments.Should().Be(2_500m);
        row.CollectedFromPayroll.Should().Be(0m);
        row.TotalCollected.Should().Be(2_500m);
        row.PayrollRunsCount.Should().Be(0);
    }

    [Fact]
    public async Task OpeningBalance_AddsToWhatPayrollCollected()
    {
        using var db = new SqliteHrmsContext();
        var employeeId = await SeedEmployeeAsync(db);
        await SeedPayrollAsync(db, employeeId, new DateOnly(2026, 9, 15), 100m);
        await SeedPayrollAsync(db, employeeId, new DateOnly(2026, 9, 30), 100m);

        await AdjustAsync(db, employeeId, 2_500m, isAddition: true, Jan1);

        var row = (await ReportAsync(db, new DateOnly(2026, 9, 30))).Single();
        row.CollectedFromPayroll.Should().Be(200m);
        row.Adjustments.Should().Be(2_500m);
        row.TotalCollected.Should().Be(2_700m);
        row.PayrollRunsCount.Should().Be(2);
    }

    [Fact]
    public async Task AnEntryCountsOnlyFromItsEntryDate()
    {
        using var db = new SqliteHrmsContext();
        var employeeId = await SeedEmployeeAsync(db);
        await AdjustAsync(db, employeeId, 500m, isAddition: true, new DateOnly(2026, 6, 1));

        (await ReportAsync(db, new DateOnly(2026, 5, 31))).Should().BeEmpty();
        (await ReportAsync(db, new DateOnly(2026, 6, 1))).Single().TotalCollected.Should().Be(500m);
    }

    [Fact]
    public async Task ARemoval_CorrectsTheTotal_ButNeverBelowZero()
    {
        using var db = new SqliteHrmsContext();
        var employeeId = await SeedEmployeeAsync(db);
        await AdjustAsync(db, employeeId, 1_000m, isAddition: true, Jan1);

        await AdjustAsync(db, employeeId, 300m, isAddition: false, Jan1);
        (await ReportAsync(db, Jan1)).Single().TotalCollected.Should().Be(700m);

        await AdjustAsync(db, employeeId, 5_000m, isAddition: false, Jan1);
        (await ReportAsync(db, Jan1)).Single().TotalCollected.Should().Be(0m, "a removal is clamped to what was collected");

        var nothingLeft = () => AdjustAsync(db, employeeId, 1m, isAddition: false, Jan1);
        await nothingLeft.Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public async Task ANonPositiveAmount_IsRejected(int amount)
    {
        using var db = new SqliteHrmsContext();
        var employeeId = await SeedEmployeeAsync(db);

        var act = () => AdjustAsync(db, employeeId, amount, isAddition: true, Jan1);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task MyCashBond_IncludesTheOpeningBalance()
    {
        using var db = new SqliteHrmsContext();
        var employeeId = await SeedEmployeeAsync(db);
        await SeedPayrollAsync(db, employeeId, new DateOnly(2026, 9, 15), 100m);
        await AdjustAsync(db, employeeId, 2_500m, isAddition: true, Jan1);

        var mine = await WithServiceAsync(db, s => s.GetMyCashBondAsync(employeeId, CancellationToken.None));

        mine.TotalCollected.Should().Be(2_600m);
        mine.Runs.Should().ContainSingle().Which.Amount.Should().Be(100m);
        mine.Adjustments.Should().ContainSingle().Which.Should().BeEquivalentTo(new MyCashBondAdjustmentModel
        {
            EntryDate = Jan1, Amount = 2_500m, Particulars = "Opening balance from previous system",
        });
    }
}
