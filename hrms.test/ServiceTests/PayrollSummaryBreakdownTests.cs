using Hrms.Domain.Entities.EmployeeEntities;
using hrms.test.TestSupport;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>
/// The Payroll Summary's per-type Deductions / Other Income columns: one item per type per payroll,
/// summed over that payroll's own rows, with the type's name kept even after it is deleted.
/// </summary>
public class PayrollSummaryBreakdownTests
{
    [Fact]
    public async Task AttachDeductionItemsAsync_GroupsPerDeductionType_FlagsLoans_AndKeepsDeletedTypeNames()
    {
        using var db = new SqliteHrmsContext();
        var payrollA = Guid.NewGuid();
        var payrollB = Guid.NewGuid();
        var loanType = new DeductionType { Id = Guid.NewGuid(), Code = "SSSLOAN", Name = "SSS Loan" };
        var otherType = new DeductionType { Id = Guid.NewGuid(), Code = "DUES", Name = "Dues" };
        var sssLoan = new Deduction { Id = Guid.NewGuid(), Code = "SSS", Name = "SSS Loan", CategoryId = loanType.Id };
        var dues = new Deduction { Id = Guid.NewGuid(), Code = "UD", Name = "Union Dues", CategoryId = otherType.Id, DeletedAt = DateTime.UtcNow };
        await using (var seed = db.NewContext())
        {
            // Only the detail rows matter here -- not the Payroll / installment they point at --
            // so the FKs are switched off for this seed (same as DeductionApplicationScheduleTests).
            await seed.Database.OpenConnectionAsync();
            await seed.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF");
            seed.DeductionTypes.AddRange(loanType, otherType);
            seed.Deductions.AddRange(sssLoan, dues);
            PayrollDeductionDetail Row(Guid payroll, Deduction d, decimal amount) => new()
            {
                Id = Guid.NewGuid(), PayrollId = payroll, DeductionApplicationDetailId = Guid.NewGuid(),
                DeductionId = d.Id, Amount = amount,
            };
            seed.PayrollDeductionDetails.AddRange(
                Row(payrollA, sssLoan, 300m),
                Row(payrollA, sssLoan, 200m),   // two installments of the same type -> one item
                Row(payrollA, dues, 80m),
                Row(payrollB, dues, 50m));
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var service = new PayrollService(new UnitOfWorkService(context), Substitute.For<IMapper>());
        var a = new Payroll { Id = payrollA };
        var b = new Payroll { Id = payrollB };
        var none = new Payroll { Id = Guid.NewGuid() };

        await service.AttachDeductionItemsAsync([a, b, none], CancellationToken.None);

        a.DeductionItems.Should().BeEquivalentTo(new[]
        {
            new PayrollBreakdownItem(sssLoan.Id, "SSS Loan", 500m, IsLoan: true),
            new PayrollBreakdownItem(dues.Id, "Union Dues", 80m),
        });
        b.DeductionItems.Should().Equal(new PayrollBreakdownItem(dues.Id, "Union Dues", 50m));
        none.DeductionItems.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOtherIncomeItemsAsync_SumsPerIncomeType_OnlyForOthersClass_AndOnlyThesePayrolls()
    {
        using var db = new SqliteHrmsContext();
        var payrollId = Guid.NewGuid();
        var hg = new OtherIncome { Id = Guid.NewGuid(), Code = "HG", Name = "HG Allowance", IncomeClass = IncomeClassType.Others };
        var bonus = new OtherIncome { Id = Guid.NewGuid(), Code = "BN", Name = "13th Bonus", IncomeClass = IncomeClassType.SpecialBonus };
        await using (var seed = db.NewContext())
        {
            var groupId = Guid.NewGuid();
            seed.PayrollGroups.Add(new PayrollGroup { Id = groupId });
            var employee = new Employee { Id = Guid.NewGuid(), PayrollGroupId = groupId };
            seed.Employees.Add(employee);
            seed.Allowances.AddRange(hg, bonus);
            var app = new OtherIncomeApplication { Id = Guid.NewGuid(), EmployeeId = employee.Id, IncomeId = hg.Id };
            seed.OtherIncomeApplications.Add(app);

            OtherIncomeSchedules Row(OtherIncome income, decimal amount, Guid? consumedBy) => new()
            {
                Id = Guid.NewGuid(), EmployeeId = employee.Id, OtherIncomeApplicationId = app.Id,
                IncomeId = income.Id, Date = new DateOnly(2026, 10, 15), Amount = amount, ConsumedByPayrollId = consumedBy,
            };
            seed.OtherIncomeApplicationDetails.AddRange(
                Row(hg, 250m, payrollId),
                Row(hg, 250m, payrollId),
                Row(bonus, 900m, payrollId),         // a Bonuses-class type has its own column
                Row(hg, 999m, Guid.NewGuid()),       // another payroll's
                Row(hg, 999m, null));                // not paid yet
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var items = await new IncomeAplDtlService(new UnitOfWorkService(context))
            .GetOtherIncomeItemsAsync([payrollId], CancellationToken.None);

        items.Keys.Should().Equal(payrollId);
        items[payrollId].Should().Equal(new PayrollBreakdownItem(hg.Id, "HG Allowance", 500m));
    }

    [Fact]
    public async Task GetOtherIncomeItemsAsync_ReturnsNothingForNoPayrolls()
    {
        using var db = new SqliteHrmsContext();
        await using var context = db.NewContext();

        var items = await new IncomeAplDtlService(new UnitOfWorkService(context))
            .GetOtherIncomeItemsAsync([], CancellationToken.None);

        items.Should().BeEmpty();
    }

    [Fact]
    public void AttachIncomeItems_FillsEachPayrollFromItsOwnEntry_AndEmptyWhenMissing()
    {
        var a = new Payroll { Id = Guid.NewGuid() };
        var b = new Payroll { Id = Guid.NewGuid() };
        var item = new PayrollBreakdownItem(Guid.NewGuid(), "HG Allowance", 500m);

        PayrollService.AttachIncomeItems([a, b], new() { [a.Id] = [item] });

        a.IncomeItems.Should().Equal(item);
        b.IncomeItems.Should().BeEmpty();
    }
}
