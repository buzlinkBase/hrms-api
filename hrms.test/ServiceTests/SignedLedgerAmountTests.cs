using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Infrastructure;
using hrms.test.TestSupport;
using Mapster;
using MapsterMapper;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>
/// Retirement and Uniform Allowance ledgers store each movement as one signed Amount: + adds to
/// the balance, − deducts from it. The running Balance must always equal the sum of Amounts, and
/// the ledger reports hand the signed Amount through unchanged.
/// </summary>
public class SignedLedgerAmountTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly DateOnly Jan31 = new(2026, 1, 31);
    private static readonly DateOnly Feb28 = new(2026, 2, 28);
    private static readonly DateOnly Mar31 = new(2026, 3, 31);

    private static async Task<Guid> SeedEmployeeAsync(SqliteHrmsContext db)
    {
        await using var seed = db.NewContext(CompanyId);
        var group = new PayrollGroup { Id = Guid.NewGuid(), TenantId = CompanyId };
        var employee = new Employee
        {
            Id = Guid.NewGuid(), TenantId = CompanyId, PayrollGroupId = group.Id,
            FirstName = "Ana", LastName = "Reyes", EmployeeNo = "EMP-001",
        };
        seed.PayrollGroups.Add(group);
        seed.Employees.Add(employee);
        await seed.SaveChangesAsync();
        return employee.Id;
    }

    // No current company -- see CashBondAdjustmentTests.WithServiceAsync.
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

    [Fact]
    public async Task Retirement_AdditionIsPositive_RemovalIsNegativeAndClamped()
    {
        using var db = new SqliteHrmsContext();
        var employeeId = await SeedEmployeeAsync(db);

        await WithUowAsync(db, uow => new PayrollService(uow, Substitute.For<IMapper>())
            .AdjustRetirementBalanceAsync(employeeId, 1_000m, isAddition: true, "Opening balance", Jan31, CancellationToken.None));
        await WithUowAsync(db, uow => new PayrollService(uow, Substitute.For<IMapper>())
            .AdjustRetirementBalanceAsync(employeeId, 300m, isAddition: false, "Correction", Feb28, CancellationToken.None));
        await WithUowAsync(db, uow => new PayrollService(uow, Substitute.For<IMapper>())
            .AdjustRetirementBalanceAsync(employeeId, 5_000m, isAddition: false, "Over-removal", Mar31, CancellationToken.None));

        var ledger = await WithReportsAsync(db, s => s.GetRetirementLedgerAsync(Jan31, Mar31, CancellationToken.None));

        ledger.Select(x => (x.Amount, x.Balance, x.Particulars)).Should().Equal(
            (1_000m, 1_000m, "Opening balance"),
            (-300m, 700m, "Correction"),
            (-700m, 0m, "Over-removal"));
        ledger.Sum(x => x.Amount).Should().Be(ledger[^1].Balance);
    }

    [Fact]
    public async Task UniformAllowance_AccrualAndAdditionArePositive_RemovalAndReleaseAreNegative()
    {
        using var db = new SqliteHrmsContext();
        var employeeId = await SeedEmployeeAsync(db);

        await WithUowAsync(db, uow => new UniformAllowanceFundService(uow)
            .AccrueAsync(employeeId, 500m, Jan31, "January accrual", CancellationToken.None));
        await WithUowAsync(db, uow => new UniformAllowanceFundService(uow)
            .AdjustAsync(employeeId, 200m, isAddition: true, "Opening balance", Jan31, CancellationToken.None));
        await WithUowAsync(db, uow => new UniformAllowanceFundService(uow)
            .AdjustAsync(employeeId, 100m, isAddition: false, "Correction", Feb28, CancellationToken.None));
        await WithUowAsync(db, uow => new UniformAllowanceFundService(uow)
            .ReleaseBatchAsync([(employeeId, 250m)], Mar31, "Uniform issuance", CancellationToken.None));

        var ledger = await WithReportsAsync(db, s => s.GetUniformAllowanceLedgerAsync(Jan31, Mar31, CancellationToken.None));

        ledger.Select(x => (x.EntryType, x.Amount)).Should().BeEquivalentTo(new[]
        {
            ("Accrual", 500m),
            ("Adjustment", 200m),
            ("Adjustment", -100m),
            ("Release", -250m),
        });
        ledger.Sum(x => x.Amount).Should().Be(350m);
        ledger.Single(x => x.EntryType == "Release").Balance.Should().Be(350m);
    }

    [Fact]
    public async Task UniformAllowance_ReleaseIsClampedToTheBalance_AndFlagged()
    {
        using var db = new SqliteHrmsContext();
        var employeeId = await SeedEmployeeAsync(db);

        await WithUowAsync(db, uow => new UniformAllowanceFundService(uow)
            .AccrueAsync(employeeId, 400m, Jan31, "January accrual", CancellationToken.None));

        List<Guid> clamped = [];
        await WithUowAsync(db, async uow => clamped = await new UniformAllowanceFundService(uow)
            .ReleaseBatchAsync([(employeeId, 1_000m)], Feb28, "Uniform issuance", CancellationToken.None));

        clamped.Should().Equal(employeeId);
        var release = (await WithReportsAsync(db, s => s.GetUniformAllowanceLedgerAsync(Jan31, Feb28, CancellationToken.None)))
            .Single(x => x.EntryType == "Release");
        release.Amount.Should().Be(-400m);
        release.Balance.Should().Be(0m);
    }
}
