using Hrms.Core.Services;

namespace hrms.test.PayrollRunTests;

public class PayrollProcessorUtilTests
{
    [Fact]
    public void GetGross_SumsBasicPayTimeHourPayAllIncomeAndLeavePayouts()
    {
        var line = new PayrollSummaryLine
        {
            BasicPay = 10_000,
            TimeHourPayResults = new List<DTRPayModel>
            {
                new() { TotalExcludingBasic = 2_500 },
                new() { TotalExcludingBasic = 500 },
            },
            TotalAllIncome = 1_200,
            CompanyFundedLeavePay = 300,
            GovernmentFundedLeavePay = 100,
        };

        PayrollProcessorUtil.GetGross(line).Should().Be(14_600);
    }

    [Fact]
    public void GetGross_ZeroWhenLineIsEmpty()
    {
        PayrollProcessorUtil.GetGross(new PayrollSummaryLine()).Should().Be(0);
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
