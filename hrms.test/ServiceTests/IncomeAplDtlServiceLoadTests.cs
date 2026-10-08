using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Infrastructure;
using hrms.test.TestSupport;

namespace hrms.test.ServiceTests;

/// <summary>
/// IncomeAplDtlService.LoadAsync feeds the payroll run's Other Income. The income class must come
/// from the query itself (a join to Allowances), never from the lazy Income navigation inside a
/// client-side selector -- on MySQL that second query, fired while the first reader is still open,
/// failed every payroll calculate with "This MySqlConnection is already in use".
/// </summary>
public class IncomeAplDtlServiceLoadTests
{
    private static readonly DateOnly From = new(2026, 9, 16);
    private static readonly DateOnly To = new(2026, 9, 30);

    private static (Guid EmployeeId, Guid IncomeId, Guid ApplicationId) SeedParents(HrmsContext seed, IncomeClassType incomeClass)
    {
        var groupId = Guid.NewGuid();
        seed.PayrollGroups.Add(new PayrollGroup { Id = groupId });
        var employee = new Employee { Id = Guid.NewGuid(), PayrollGroupId = groupId };
        seed.Employees.Add(employee);
        var income = new OtherIncome { Id = Guid.NewGuid(), Code = "BONUS", Name = "Bonus", IncomeClass = incomeClass };
        seed.Allowances.Add(income);
        var application = new OtherIncomeApplication { Id = Guid.NewGuid(), EmployeeId = employee.Id, IncomeId = income.Id };
        seed.OtherIncomeApplications.Add(application);
        return (employee.Id, income.Id, application.Id);
    }

    private static OtherIncomeSchedules Schedule(Guid employeeId, Guid incomeId, Guid applicationId, DateOnly date, decimal amount) => new()
    {
        Id = Guid.NewGuid(),
        EmployeeId = employeeId,
        IncomeId = incomeId,
        ApplicationId = applicationId,
        Date = date,
        Amount = amount,
        IsTaxable = true,
    };

    [Fact]
    public async Task LoadAsync_ReadsIncomeClassFromTheQuery_NotALazyNavigation()
    {
        using var db = new SqliteHrmsContext();
        Guid employeeId, incomeId, scheduleId;
        await using (var seed = db.NewContext())
        {
            Guid applicationId;
            (employeeId, incomeId, applicationId) = SeedParents(seed, IncomeClassType.SpecialBonus);
            var row = Schedule(employeeId, incomeId, applicationId, new DateOnly(2026, 9, 20), 1500m);
            scheduleId = row.Id;
            seed.OtherIncomeApplicationDetails.Add(row);
            await seed.SaveChangesAsync();
        }

        // Plain context (no lazy-loading proxies): a navigation read would come back null here.
        await using var context = db.NewContext();
        var service = new IncomeAplDtlService(new UnitOfWorkService(context));

        var result = await service.LoadAsync([employeeId], From, To, CancellationToken.None);

        var info = result.Should().ContainKey(new EmployeeKey(employeeId)).WhoseValue.Should().ContainSingle().Subject;
        info.Id.Should().Be(scheduleId);
        info.IncomeId.Should().Be(incomeId);
        info.Type.Should().Be(IncomeClassType.SpecialBonus);
        info.Amount.Should().Be(1500m);
        info.Taxable.Should().BeTrue();
        info.PayrollDate.Should().Be(new DateOnly(2026, 9, 20));
    }

    [Fact]
    public async Task LoadAsync_ExcludesConsumedOutOfRangeAndInactiveRows()
    {
        using var db = new SqliteHrmsContext();
        Guid employeeId, keptId;
        await using (var seed = db.NewContext())
        {
            (employeeId, var incomeId, var applicationId) = SeedParents(seed, IncomeClassType.Regular);
            var kept = Schedule(employeeId, incomeId, applicationId, new DateOnly(2026, 9, 30), 500m);
            keptId = kept.Id;
            var consumed = Schedule(employeeId, incomeId, applicationId, new DateOnly(2026, 9, 16), 500m);
            consumed.ConsumedByPayrollId = Guid.NewGuid();
            var outOfRange = Schedule(employeeId, incomeId, applicationId, new DateOnly(2026, 10, 1), 500m);
            var inactive = Schedule(employeeId, incomeId, applicationId, new DateOnly(2026, 9, 25), 500m);
            inactive.Status = "Inactive";
            seed.OtherIncomeApplicationDetails.AddRange(kept, consumed, outOfRange, inactive);
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var service = new IncomeAplDtlService(new UnitOfWorkService(context));

        var result = await service.LoadAsync([employeeId, Guid.NewGuid()], From, To, CancellationToken.None);

        result.Should().ContainSingle("only the seeded employee has rows");
        result[new EmployeeKey(employeeId)].Select(x => x.Id).Should().Equal(keptId);
    }
}
