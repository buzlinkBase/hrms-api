using Hrms.Core.Services;

namespace hrms.test.PayrollRunTests;

/// <summary>
/// The payroll run preview's "what is this amount made of" breakdown (PayrollSummaryLine.Sources):
/// every source line must add back up to the amount it explains.
/// </summary>
public class PayrollSourceBreakdownTests
{
    private static EmployeeModelPayrollRun FixedEmployee() => new()
    {
        Id = Guid.NewGuid(),
        SalaryType = SalaryType.FIXED,
        MonthlyRate = 30_000,
        DailyRate = 1_000,
        PayrollGroup = new PayrollGroupModel { PayrollFrequency = PayrollFrequency.SEMI_MONTHLY },
    };

    [Fact]
    public void AddSource_IgnoresZeroAmounts()
    {
        var line = new PayrollSummaryLine();
        line.AddSource("basicPay", "Late", 0m);
        line.Sources.Should().BeEmpty();
    }

    [Fact]
    public void GetBasicPay_Fixed_SourcesSumToBasicPay()
    {
        var line = new PayrollSummaryLine { PayPeriodStart = new DateOnly(2026, 8, 1) };
        var timeCalc = new List<DTRPayModel> { new() { LateAmount = 100, UTAmount = 50 } };

        EmployeePayrollLineService.GetBasicPay(line, timeCalc, FixedEmployee(), oneTimeLeaveDays: 2);

        var items = line.Sources["basicPay"];
        items.Select(x => x.Label).Should().Contain(new[] { "Fixed salary (period share)", "Late", "Under time" });
        items.Sum(x => x.Amount).Should().Be(line.BasicPay);
    }

    [Fact]
    public void GetBasicPay_Variable_SingleRegularDayPayLine()
    {
        var employee = FixedEmployee();
        employee.SalaryType = SalaryType.VARIABLE;
        var line = new PayrollSummaryLine { PayPeriodStart = new DateOnly(2026, 8, 1) };
        var timeCalc = new List<DTRPayModel> { new() { RegularDayPay = 800 }, new() { RegularDayPay = 800 } };

        EmployeePayrollLineService.GetBasicPay(line, timeCalc, employee, oneTimeLeaveDays: 0);

        line.Sources["basicPay"].Should().ContainSingle()
            .Which.Should().Be(new PayrollSourceItem("Regular day pay", 1_600, "2 day(s)"));
    }

    [Fact]
    public void ApplySalaryAdjustments_RecordsEachAdjustmentAsASourceWithItsRemarks()
    {
        var employee = FixedEmployee();
        var payload = new CalculatorPayload();
        payload.SalaryAdjustments[new EmployeeKey(employee.Id)] = new List<SalaryAdjustment>
        {
            new() { AdjustmentType = SalaryAdjustmentType.Salary, Amount = 500, Remarks = "Back pay" },
            new() { AdjustmentType = SalaryAdjustmentType.Allowance, Amount = 200, Remarks = "Rice" },
        };
        var line = new PayrollSummaryLine { BasicPay = 1_000 };

        EmployeePayrollLineService.ApplySalaryAdjustments(payload, employee, line);

        line.Sources["basicPay"].Should().ContainSingle()
            .Which.Should().Be(new PayrollSourceItem("Salary adjustment", 500, "Back pay"));
        line.Sources["totalOtherIncome"].Should().ContainSingle()
            .Which.Amount.Should().Be(200);
    }
}
