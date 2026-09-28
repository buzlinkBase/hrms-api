using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Infrastructure;
using hrms.test.TestSupport;
using Mapster;
using MapsterMapper;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>
/// Ledger screens: the main list is one summary row per employee as of a date, and "View ledger"
/// shows that one employee's signed entries (oldest first) with the balance after each.
/// </summary>
public class LedgerDetailsTests
{
    private static readonly DateOnly Jan15 = new(2026, 1, 15);
    private static readonly DateOnly Jan31 = new(2026, 1, 31);
    private static readonly DateOnly Feb15 = new(2026, 2, 15);
    private static readonly DateOnly Feb28 = new(2026, 2, 28);

    private static async Task<Guid> SeedEmployeeAsync(SqliteHrmsContext db, string lastName)
    {
        await using var seed = db.NewContext();
        var group = new PayrollGroup { Id = Guid.NewGuid() };
        var employee = new Employee
        {
            Id = Guid.NewGuid(), PayrollGroupId = group.Id, FirstName = "Ana", LastName = lastName, EmployeeNo = lastName,
        };
        seed.PayrollGroups.Add(group);
        seed.Employees.Add(employee);
        await seed.SaveChangesAsync();
        return employee.Id;
    }

    private static async Task WithUowAsync(SqliteHrmsContext db, Func<IUnitOfWorkService, Task> action)
    {
        await using var context = db.NewContext();
        await action(new UnitOfWorkService(context));
    }

    private static async Task<T> WithReportsAsync<T>(SqliteHrmsContext db, Func<PayrollReportService, Task<T>> action)
    {
        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var employees = new EmployeeService(uow, Substitute.For<IMapper>(), new TypeAdapterConfig(), null!, null!, null!, null!, null!, null!);
        return await action(new PayrollReportService(uow, employees, null!));
    }

    private static Task AdjustRetirementAsync(SqliteHrmsContext db, Guid employeeId, decimal amount, bool isAddition, DateOnly date) =>
        WithUowAsync(db, uow => new PayrollService(uow, Substitute.For<IMapper>())
            .AdjustRetirementBalanceAsync(employeeId, amount, isAddition, "HR entry", date, CancellationToken.None));

    [Fact]
    public async Task CashBondEntries_MergePayrollAndManualEntries_WithRunningTotal_UpToAsOf()
    {
        using var db = new SqliteHrmsContext();
        var employeeId = await SeedEmployeeAsync(db, "Reyes");
        var otherId = await SeedEmployeeAsync(db, "Cruz");
        await using (var seed = db.NewContext())
        {
            seed.Payrolls.AddRange(
                new Payroll { Id = Guid.NewGuid(), EmployeeId = employeeId, PayPeriodStart = Jan15.AddDays(-14), PayPeriodEnd = Jan15, CashBondDeduction = 100m },
                new Payroll { Id = Guid.NewGuid(), EmployeeId = employeeId, PayPeriodStart = Feb15.AddDays(-14), PayPeriodEnd = Feb15, CashBondDeduction = 100m },
                new Payroll { Id = Guid.NewGuid(), EmployeeId = employeeId, PayPeriodStart = Feb28.AddDays(-12), PayPeriodEnd = Feb28, CashBondDeduction = 100m },
                new Payroll { Id = Guid.NewGuid(), EmployeeId = otherId, PayPeriodStart = Jan15.AddDays(-14), PayPeriodEnd = Jan15, CashBondDeduction = 999m });
            await seed.SaveChangesAsync();
        }
        await WithReportsAsync(db, async s =>
        {
            await s.AdjustCashBondAsync(employeeId, 2_000m, isAddition: true, "Opening balance", new DateOnly(2026, 1, 1), CancellationToken.None);
            return true;
        });
        await WithReportsAsync(db, async s =>
        {
            await s.AdjustCashBondAsync(employeeId, 50m, isAddition: false, "Refund", Jan31, CancellationToken.None);
            return true;
        });

        var entries = await WithReportsAsync(db, s => s.GetCashBondEntriesAsync(employeeId, Feb15, CancellationToken.None));

        entries.Select(x => (x.EntryDate, x.EntryType, x.Amount, x.Balance)).Should().Equal(
            (new DateOnly(2026, 1, 1), "Adjustment", 2_000m, 2_000m),
            (Jan15, "Payroll", 100m, 2_100m),
            (Jan31, "Adjustment", -50m, 2_050m),
            (Feb15, "Payroll", 100m, 2_150m));
        entries[0].Remarks.Should().Be("Opening balance");
        entries[1].PayrollId.Should().NotBeNull();

        // The drawer's last balance matches the main list's total for the same date.
        var row = (await WithReportsAsync(db, s => s.GetCashBondReportAsync(Feb15, CancellationToken.None)))
            .Single(x => x.EmployeeId == employeeId);
        row.TotalCollected.Should().Be(entries[^1].Balance);
    }

    [Fact]
    public async Task RetirementSummary_IsOneRowPerEmployee_UpToAsOf()
    {
        using var db = new SqliteHrmsContext();
        var reyes = await SeedEmployeeAsync(db, "Reyes");
        var cruz = await SeedEmployeeAsync(db, "Cruz");
        await AdjustRetirementAsync(db, reyes, 1_000m, isAddition: true, Jan15);
        await AdjustRetirementAsync(db, reyes, 300m, isAddition: false, Jan31);
        await AdjustRetirementAsync(db, reyes, 500m, isAddition: true, Feb28);
        await AdjustRetirementAsync(db, cruz, 200m, isAddition: true, Jan15);

        var summary = await WithReportsAsync(db, s => s.GetRetirementSummaryAsync(Feb15, CancellationToken.None));

        summary.Select(x => x.EmployeeId).Should().Equal([cruz, reyes], "sorted by name");
        var row = summary.Single(x => x.EmployeeId == reyes);
        row.TotalAdded.Should().Be(1_000m);
        row.TotalDeducted.Should().Be(300m);
        row.Balance.Should().Be(700m);
        row.EntryCount.Should().Be(2);
        row.LastEntryDate.Should().Be(Jan31);
    }

    [Fact]
    public async Task RetirementEntries_AreOnlyThatEmployees_OldestFirst_UpToAsOf()
    {
        using var db = new SqliteHrmsContext();
        var reyes = await SeedEmployeeAsync(db, "Reyes");
        var cruz = await SeedEmployeeAsync(db, "Cruz");
        await AdjustRetirementAsync(db, reyes, 1_000m, isAddition: true, Jan15);
        await AdjustRetirementAsync(db, cruz, 200m, isAddition: true, Jan15);
        await AdjustRetirementAsync(db, reyes, 300m, isAddition: false, Jan31);
        await AdjustRetirementAsync(db, reyes, 500m, isAddition: true, Feb28);

        var entries = await WithReportsAsync(db, s => s.GetRetirementEntriesAsync(reyes, Feb15, CancellationToken.None));

        entries.Select(x => (x.EntryDate, x.Amount, x.Balance)).Should().Equal(
            (Jan15, 1_000m, 1_000m),
            (Jan31, -300m, 700m));
    }

    [Fact]
    public async Task UniformAllowanceSummaryAndEntries_AgreeOnTheBalance()
    {
        using var db = new SqliteHrmsContext();
        var employeeId = await SeedEmployeeAsync(db, "Reyes");
        await WithUowAsync(db, uow => new UniformAllowanceFundService(uow)
            .AccrueAsync(employeeId, 500m, Jan31, "January accrual", CancellationToken.None));
        await WithUowAsync(db, uow => new UniformAllowanceFundService(uow)
            .ReleaseBatchAsync([(employeeId, 200m)], Feb15, "Uniform issuance", CancellationToken.None));

        var row = (await WithReportsAsync(db, s => s.GetUniformAllowanceSummaryAsync(Feb28, CancellationToken.None))).Single();
        var entries = await WithReportsAsync(db, s => s.GetUniformAllowanceEntriesAsync(employeeId, Feb28, CancellationToken.None));

        row.TotalAdded.Should().Be(500m);
        row.TotalDeducted.Should().Be(200m);
        row.Balance.Should().Be(300m);
        entries.Select(x => (x.EntryType, x.Amount, x.Balance)).Should().Equal(
            ("Accrual", 500m, 500m),
            ("Release", -200m, 300m));
    }

    [Fact]
    public async Task LeaveEntries_AreForOneEmployeeLeaveTypeAndYear()
    {
        using var db = new SqliteHrmsContext();
        var employeeId = await SeedEmployeeAsync(db, "Reyes");
        var vl = new Leave { Id = Guid.NewGuid(), Code = "VL", Description = "Vacation Leave" };
        var sl = new Leave { Id = Guid.NewGuid(), Code = "SL", Description = "Sick Leave" };
        await using (var seed = db.NewContext())
        {
            seed.Leaves.AddRange(vl, sl);
            LeaveCredits Credits(Guid leaveId, int year) => new()
            {
                Id = Guid.NewGuid(), EmployeeId = employeeId, LeaveId = leaveId, PeriodYear = year,
                FromDate = new DateTime(year, 1, 1), ToDate = new DateTime(year, 12, 31),
            };
            var vl2026 = Credits(vl.Id, 2026);
            var vl2025 = Credits(vl.Id, 2025);
            var sl2026 = Credits(sl.Id, 2026);
            seed.LeaveCredits.AddRange(vl2026, vl2025, sl2026);
            LeaveLedger Entry(LeaveCredits c, LedgerEntryType type, DateOnly date, decimal amount, decimal balance) => new()
            {
                Id = Guid.NewGuid(), EmployeeId = employeeId, LeaveId = c.LeaveId, LeaveCreditsId = c.Id,
                EntryType = type, EntryDate = date, Amount = amount, Balance = balance, Particulars = type.ToString(),
            };
            seed.LeaveLedgers.AddRange(
                Entry(vl2026, LedgerEntryType.Grant, new DateOnly(2026, 1, 1), 10m, 10m),
                Entry(vl2026, LedgerEntryType.Deduction, Feb15, -2m, 8m),
                Entry(vl2025, LedgerEntryType.Grant, new DateOnly(2025, 1, 1), 5m, 5m),
                Entry(sl2026, LedgerEntryType.Grant, new DateOnly(2026, 1, 1), 7m, 7m));
            await seed.SaveChangesAsync();
        }

        var entries = await WithReportsAsync(db, s => s.GetLeaveEntriesAsync(employeeId, vl.Id, 2026, CancellationToken.None));

        entries.Select(x => (x.EntryType, x.Amount, x.Balance)).Should().Equal(
            ("Grant", 10m, 10m),
            ("Deduction", -2m, 8m));
    }
}
