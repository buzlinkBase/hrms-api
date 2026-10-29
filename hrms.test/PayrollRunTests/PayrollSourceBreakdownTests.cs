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
            new() { AdjustmentType = SalaryAdjustmentType.Salary, Amount = 500, Remarks = "Back pay", PayrollDate = new DateOnly(2026, 10, 16) },
            new() { AdjustmentType = SalaryAdjustmentType.Allowance, Amount = 200, Remarks = "Rice", PayrollDate = new DateOnly(2026, 10, 16) },
        };
        var line = new PayrollSummaryLine { BasicPay = 1_000 };

        EmployeePayrollLineService.ApplySalaryAdjustments(payload, employee, line);

        line.Sources["basicPay"].Should().ContainSingle()
            .Which.Should().Be(new PayrollSourceItem("Salary adjustment", 500, "Oct 16, 2026 · Back pay"));
        line.Sources["totalOtherIncome"].Should().ContainSingle()
            .Which.Amount.Should().Be(200);
    }

    [Fact]
    public void DeductionSource_IsNamedAfterTheDeductionAndDatedBySchedule()
    {
        var d = new DeductionInfo { Name = "Cash Advance", PayrollDate = new DateOnly(2026, 10, 16), Amount = 195 };

        EmployeePayrollLineService.DeductionSourceLabel(d).Should().Be("Cash Advance");
        EmployeePayrollLineService.DeductionSourceNote(d).Should().Be("Scheduled Oct 16, 2026");
    }

    [Fact]
    public void DeductionSource_FallsBackToTheGenericLabelWhenThereIsNoName()
    {
        var others = new DeductionInfo { Type = DeductionInfoType.Others, PayrollDate = new DateOnly(2026, 10, 16) };
        var loan = new DeductionInfo { Type = DeductionInfoType.Loan, PayrollDate = new DateOnly(2026, 10, 16) };

        EmployeePayrollLineService.DeductionSourceLabel(others).Should().Be("Scheduled deduction");
        EmployeePayrollLineService.DeductionSourceLabel(loan).Should().Be("Loan");
    }

    [Fact]
    public void DeductionSource_KeepsRemarksAfterTheDate()
    {
        var d = new DeductionInfo { Name = "Uniform", PayrollDate = new DateOnly(2026, 10, 31), Remarks = "2 of 6" };

        EmployeePayrollLineService.DeductionSourceNote(d).Should().Be("Scheduled Oct 31, 2026 · 2 of 6");
    }

    [Fact]
    public void AddIncomeSources_OneRowPerScheduleRowWithItsDate_SummingToTheTotal()
    {
        var incomeId = Guid.NewGuid();
        var items = new List<OtherIncomeInfo>
        {
            new() { IncomeId = incomeId, Name = "Rice Subsidy", Amount = 300, PayrollDate = new DateOnly(2026, 10, 31) },
            new() { IncomeId = incomeId, Name = "Rice Subsidy", Amount = 200, PayrollDate = new DateOnly(2026, 10, 16) },
        };
        var line = new PayrollSummaryLine();

        EmployeePayrollLineService.AddIncomeSources(line, "totalOtherIncome", items);

        var rows = line.Sources["totalOtherIncome"];
        rows.Should().Equal(
            new PayrollSourceItem("Rice Subsidy", 200, "Scheduled Oct 16, 2026"),
            new PayrollSourceItem("Rice Subsidy", 300, "Scheduled Oct 31, 2026"));
        rows.Sum(x => x.Amount).Should().Be(500);
    }
}
