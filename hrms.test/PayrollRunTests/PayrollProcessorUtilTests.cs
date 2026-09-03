using Hrms.Core.Services;

namespace hrms.test.PayrollRunTests;

public class PayrollProcessorUtilTests
{
    [Fact]
    public void GetGross_SumsBasicPayTimeHourPayAllIncomeAndCompanyFundedLeavePayouts()
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

        PayrollProcessorUtil.GetGross(line).Should().Be(14_500);
    }

    [Fact]
    public void GetGross_ExcludesGovernmentFundedLeavePay_IncludesCompanyFundedLeavePay()
    {
        // GovernmentFundedLeavePay is a non-taxable government benefit pass-through, added
        // straight to NetPay after deductions are computed off Gross — including it here
        // would wrongly subject it to SSS/PhilHealth/Pag-IBIG/WTax and double-count it into
        // NetPay (see ApplyOneTimeLeavePayoutsToGross / GenerateLastPayAsync).
        var withGov = new PayrollSummaryLine { BasicPay = 1_000, GovernmentFundedLeavePay = 5_000 };
        var withoutGov = new PayrollSummaryLine { BasicPay = 1_000 };

        PayrollProcessorUtil.GetGross(withGov).Should().Be(PayrollProcessorUtil.GetGross(withoutGov));

        var withComp = new PayrollSummaryLine { BasicPay = 1_000, CompanyFundedLeavePay = 5_000 };
        PayrollProcessorUtil.GetGross(withComp).Should().Be(6_000);
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
