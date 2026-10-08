using Hrms.Api.Documents;
using Hrms.Api.Documents.Fixed;
using Hrms.Api.Documents.Shared;
using Hrms.Domain.Entities.EmployeeEntities;
using hrms.test.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace hrms.test.PayrollRunTests;

/// <summary>
/// Other Income on the payslip: itemized by income name ("HG Allowance") from the schedule rows a
/// saved payroll consumed, falling back to the category totals saved on the Payroll row whenever
/// the items don't account for them -- so a payslip always adds up to its Gross Income.
/// </summary>
public class PayslipOtherIncomeTests
{
    private static Payroll RegularPayroll() => new()
    {
        SalaryType = SalaryType.FIXED,
        PayrollType = PayrollType.Regular,
        FixedBasicSalary = 10_000m,
        BasicPay = 10_000m,
        TotalRegularAllowances = 500m,
        TotalBonuses = 1_000m,
    };

    private static readonly PayslipIncomeLine[] Items =
    [
        new("HG Allowance", 500m),
        new("Performance Bonus", 1_000m),
    ];

    // ---- PayslipOtherIncome ----

    [Fact]
    public void Rows_AreItemizedByName_WhenTheItemsAccountForTheSavedTotals()
    {
        new PayslipOtherIncome(Items).Rows(RegularPayroll())
            .Should().Equal(Items);
    }

    [Fact]
    public void Rows_FallBackToCategoryTotals_ForAPayrollWithNoStampedItems()
    {
        var rows = PayslipOtherIncome.Categories.Rows(RegularPayroll());

        rows.Single(x => x.Label == "Regular Allowances").Amount.Should().Be(500m);
        rows.Single(x => x.Label == "Bonuses").Amount.Should().Be(1_000m);
    }

    [Fact]
    public void Rows_FallBackToCategoryTotals_WhenTheItemsDontAddUp()
    {
        var rows = new PayslipOtherIncome([new("HG Allowance", 500m)]).Rows(RegularPayroll());

        rows.Select(x => x.Label).Should().Contain("Bonuses").And.NotContain("HG Allowance");
    }

    [Fact]
    public void Rows_LeaveOutZeroAmountItems()
    {
        var items = Items.Append(new PayslipIncomeLine("Rice Subsidy", 0m)).ToList();

        new PayslipOtherIncome(items).Rows(RegularPayroll()).Select(x => x.Label)
            .Should().NotContain("Rice Subsidy");
    }

    // ---- layouts ----

    [Fact]
    public void FixedLayout_ListsEachIncomeByName()
    {
        var rows = new FixedPayslipRowBuilder(RegularPayroll(), new PayslipOtherIncome(Items)).Build();

        rows.Should().Contain(new FixedPayslipRow("HG Allowance", 500m, FixedPayslipRowKind.Earning));
        rows.Should().Contain(new FixedPayslipRow("Performance Bonus", 1_000m, FixedPayslipRowKind.Earning));
        rows.Select(x => x.Label).Should().NotContain(["Regular Allowances", "Bonuses"]);
    }

    [Fact]
    public void HoursLayout_ListsEachIncomeByName()
    {
        var payroll = RegularPayroll();
        payroll.SalaryType = SalaryType.VARIABLE;

        var rows = new PayslipHoursDocument(payroll, new EmployeeFullModel(), null, new PayslipOtherIncome(Items)).BuildRows();

        rows.Select(x => x.Label).Should().Contain(["HG Allowance", "Performance Bonus"])
            .And.NotContain(["Regular Allowances", "Bonuses"]);
    }

    // Last Pay adds HR-confirmed Other Income straight into GrossIncome; it used to hide inside
    // the "13th Month + Leave" line.
    [Fact]
    public void LastPay_ListsOtherIncomeApartFromThe13thMonthAndLeaveFigure()
    {
        var payroll = new Payroll { PayrollType = PayrollType.LastPay, GrossIncome = 8_500m, TotalOtherIncome = 500m };

        var rows = PayslipSections.LastPayLumpSum(payroll, new PayslipOtherIncome([new("HG Allowance", 500m)]));

        rows.Should().Equal(
            ("Prorated 13th Month + Leave Conversion", 8_000m, false),
            ("OTHER INCOME", null, false),
            ("HG Allowance", 500m, false),
            ("GROSS INCOME", 8_500m, true));
    }

    [Fact]
    public void LastPay_WithNoOtherIncome_KeepsTheSingleLumpSumLine()
    {
        var payroll = new Payroll { PayrollType = PayrollType.LastPay, GrossIncome = 8_000m };

        PayslipSections.LastPayLumpSum(payroll, PayslipOtherIncome.Categories)
            .Should().Equal([("Prorated 13th Month + Leave Conversion", (decimal?)8_000m, true)]);
    }

    // ---- ScheduledIncomePolicy ----

    // An income type deleted after its schedule was made has no class (the loader's join to it
    // finds nothing). It was still paid via the running total but landed in no category, so it
    // showed on no payslip line.
    [Fact]
    public void ScheduledIncomePolicy_CountsAnUnclassifiedIncomeAsOtherIncome()
    {
        var employee = new EmployeeModelPayrollRun { Id = Guid.NewGuid() };
        var payload = new CalculatorPayload
        {
            Incomes = new Dictionary<EmployeeKey, List<OtherIncomeInfo>>
            {
                [new EmployeeKey(employee.Id)] = [new OtherIncomeInfo { Amount = 300m, Type = null }],
            },
        };
        var context = new PayrollContextBuilder().SetEmployee(employee).SetPayload(payload).Build();

        var result = new Hrms.Core.Policies.OtherIncome.ScheduledIncomePolicy().ApplyIfSatisfied(new AllowancePipeData(), context);

        result.OtherIncome.Sum(x => x.Amount).Should().Be(300m);
        result.RunningTotal.Should().Be(300m);
    }

    // ---- IncomeAplDtlService.GetPayslipLinesAsync ----

    [Fact]
    public async Task GetPayslipLinesAsync_SumsThisPayrollsRowsPerIncomeName_EvenForADeletedIncomeType()
    {
        using var db = new SqliteHrmsContext();
        var payrollId = Guid.NewGuid();
        await using (var seed = db.NewContext())
        {
            var groupId = Guid.NewGuid();
            seed.PayrollGroups.Add(new PayrollGroup { Id = groupId });
            var employee = new Employee { Id = Guid.NewGuid(), PayrollGroupId = groupId };
            seed.Employees.Add(employee);
            var hg = new OtherIncome { Id = Guid.NewGuid(), Code = "HG", Name = "HG Allowance" };
            var old = new OtherIncome { Id = Guid.NewGuid(), Code = "OLD", Name = "Old Incentive", DeletedAt = DateTime.UtcNow };
            seed.Allowances.AddRange(hg, old);
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
                Row(old, 100m, payrollId),
                Row(hg, 999m, Guid.NewGuid()),   // paid by another payroll
                Row(hg, 999m, null));             // not paid yet
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var lines = await new IncomeAplDtlService(new UnitOfWorkService(context))
            .GetPayslipLinesAsync(payrollId, CancellationToken.None);

        lines.Should().Equal(new PayslipIncomeLine("HG Allowance", 500m), new PayslipIncomeLine("Old Incentive", 100m));
    }
}
