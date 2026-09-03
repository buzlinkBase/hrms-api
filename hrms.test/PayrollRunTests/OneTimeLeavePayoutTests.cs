using Hrms.Core.Services;
using hrms.test.TestSupport;

namespace hrms.test.PayrollRunTests;

/// <summary>
/// PayrollProcessorService.ApplyOneTimeLeavePayoutsToGross — the injection point for a
/// OneTime leave payout (LeaveApplication.PayoutMode). GovernmentAmount is a government
/// benefit pass-through (excluded from GrossIncome/statutory bases); CompanyAmount is taxable
/// compensation. This method itself never touches GrossIncome directly — it only accumulates
/// GovernmentFundedLeavePay/CompanyFundedLeavePay/NonTaxableBenefits/TaxableBenefits;
/// PayrollProcessorUtil.GetGross (called right after, in CalculateAsync) is what folds
/// CompanyFundedLeavePay into GrossIncome while deliberately excluding
/// GovernmentFundedLeavePay — see PayrollProcessorUtilTests.GetGross_ExcludesGovernmentFundedLeavePay_IncludesCompanyFundedLeavePay
/// and OneTimeLeavePayoutPipelineTests for that full interaction. Made `internal` (not
/// private) specifically so this can be tested here without a database — see Hrms.Core's
/// InternalsVisibleTo for hrms.test.
/// </summary>
public class OneTimeLeavePayoutTests : TestContextBase
{
    private static EmployeeModelPayrollRun Employee(Guid id) => new() { Id = id, FullName = "Test Employee" };

    private static PayrollSummaryLine Line(decimal startingGrossIncome = 20_000) => new()
    {
        GrossIncome = startingGrossIncome,
    };

    private static LeaveApplication OneTimePayout(decimal? gov, decimal? comp) => new()
    {
        Leave = new Leave { Description = "SSS Sickness Benefit" },
        PayoutMode = PayoutMode.OneTime,
        GovernmentAmount = gov,
        CompanyAmount = comp,
    };

    [Fact]
    public void NoPayoutForEmployee_LeavesPayrollLineUntouched()
    {
        var payload = new CalculatorPayload();
        var line = Line(20_000);

        PayrollProcessorService.ApplyOneTimeLeavePayoutsToGross(payload, Employee(Guid.NewGuid()), line);

        line.GrossIncome.Should().Be(20_000);
        line.GovernmentFundedLeavePay.Should().Be(0);
        line.CompanyFundedLeavePay.Should().Be(0);

    }

    [Fact]
    public void GovernmentAmount_IsAddedToNetPayBookkeeping_ButExcludedFromGrossIncome()
    {
        var empId = Guid.NewGuid();
        var payload = new CalculatorPayload();
        payload.OneTimeLeavePayouts[new EmployeeKey(empId)] = new List<LeaveApplication> { OneTimePayout(gov: 15_000, comp: 0) };
        var line = Line(20_000);

        PayrollProcessorService.ApplyOneTimeLeavePayoutsToGross(payload, Employee(empId), line);

        line.GovernmentFundedLeavePay.Should().Be(15_000);
        line.NonTaxableBenefits.Should().Be(15_000);
        line.GrossIncome.Should().Be(20_000); // unchanged — Government amount excluded
    }

    [Fact]
    public void CompanyAmount_AccumulatesIntoCompanyFundedLeavePayAndTaxableBenefits_GrossIncomeUntouchedHere()
    {
        var empId = Guid.NewGuid();
        var payload = new CalculatorPayload();
        payload.OneTimeLeavePayouts[new EmployeeKey(empId)] = new List<LeaveApplication> { OneTimePayout(gov: 0, comp: 5_000) };
        var line = Line(20_000);

        PayrollProcessorService.ApplyOneTimeLeavePayoutsToGross(payload, Employee(empId), line);

        line.CompanyFundedLeavePay.Should().Be(5_000);
        line.TaxableBenefits.Should().Be(5_000);
        // GetGross (not this method) is what folds CompanyFundedLeavePay into GrossIncome —
        // see OneTimeLeavePayoutPipelineTests for that chained behavior.
        line.GrossIncome.Should().Be(20_000);
    }

    [Fact]
    public void GovernmentAndCompanyAmounts_BothAccumulate_GrossIncomeUntouchedHere()
    {
        var empId = Guid.NewGuid();
        var payload = new CalculatorPayload();
        payload.OneTimeLeavePayouts[new EmployeeKey(empId)] = new List<LeaveApplication> { OneTimePayout(gov: 15_000, comp: 5_000) };
        var line = Line(20_000);

        PayrollProcessorService.ApplyOneTimeLeavePayoutsToGross(payload, Employee(empId), line);

        line.GovernmentFundedLeavePay.Should().Be(15_000);
        line.CompanyFundedLeavePay.Should().Be(5_000);
        line.GrossIncome.Should().Be(20_000);
    }

    [Fact]
    public void MultiplePayoutsForSameEmployee_AllAccumulate_GrossIncomeUntouchedHere()
    {
        var empId = Guid.NewGuid();
        var payload = new CalculatorPayload();
        payload.OneTimeLeavePayouts[new EmployeeKey(empId)] = new List<LeaveApplication>
        {
            OneTimePayout(gov: 10_000, comp: 2_000),
            OneTimePayout(gov: 5_000, comp: 1_000),
        };
        var line = Line(20_000);

        PayrollProcessorService.ApplyOneTimeLeavePayoutsToGross(payload, Employee(empId), line);

        line.GovernmentFundedLeavePay.Should().Be(15_000);
        line.CompanyFundedLeavePay.Should().Be(3_000);
        line.GrossIncome.Should().Be(20_000);
    }

    [Fact]
    public void NullAmounts_AreTreatedAsZero()
    {
        var empId = Guid.NewGuid();
        var payload = new CalculatorPayload();
        payload.OneTimeLeavePayouts[new EmployeeKey(empId)] = new List<LeaveApplication> { OneTimePayout(gov: null, comp: null) };
        var line = Line(20_000);

        PayrollProcessorService.ApplyOneTimeLeavePayoutsToGross(payload, Employee(empId), line);

        line.GovernmentFundedLeavePay.Should().Be(0);
        line.CompanyFundedLeavePay.Should().Be(0);
        line.GrossIncome.Should().Be(20_000);
    }
}
