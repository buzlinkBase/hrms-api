using Hrms.Core.Services;

namespace hrms.test.PayrollRunTests;

public class PayrollProcessorUtilTests
{
    [Fact]
    public void GetGrossIncome_SumsBasicPayrollGrossAndAllowanceRunningTotal()
    {
        var basics = new List<DTRPayModel>
        {
            new() { Gross = 10_000 },
            new() { Gross = 2_500 },
        };
        var incomes = new AllowancePipeData { RunningTotal = 1_200 };

        PayrollProcessorUtil.GetGrossIncome(incomes, basics).Should().Be(13_700);
    }

    [Fact]
    public void GetGrossIncome_ZeroWhenNoBasicsAndNoAllowances()
    {
        PayrollProcessorUtil.GetGrossIncome(new AllowancePipeData(), new List<DTRPayModel>()).Should().Be(0);
    }

    [Fact]
    public void GetNetPay_IsGrossIncomeMinusTotalDeductionsRunningTotal()
    {
        var line = new PayrollSummaryLine { GrossIncome = 25_000 };
        var deductions = new DeductionPipeData { RunningTotal = 3_400 };

        PayrollProcessorUtil.GetNetPay(line, deductions).Should().Be(21_600);
    }

    [Fact]
    public void GetNetPay_CanGoNegative_WhenDeductionsExceedGross_NoFlooringApplied()
    {
        // Documents actual behavior — GetNetPay does not clamp at zero itself; any floor is
        // the caller's responsibility.
        var line = new PayrollSummaryLine { GrossIncome = 1_000 };
        var deductions = new DeductionPipeData { RunningTotal = 1_500 };

        PayrollProcessorUtil.GetNetPay(line, deductions).Should().Be(-500);
    }
}
