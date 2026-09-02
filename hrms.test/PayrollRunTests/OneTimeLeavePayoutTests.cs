using Hrms.Core.Services;
using hrms.test.TestSupport;

namespace hrms.test.PayrollRunTests;

/// <summary>
/// PayrollProcessorService.ApplyOneTimeLeavePayoutsToGross — the injection point for a
/// OneTime leave payout (LeaveApplication.PayoutMode). GovernmentAmount is deliberately kept
/// out of GrossIncome (a government benefit pass-through, not compensation, and all four
/// statutory calculators share the same GrossIncome-based bracket lookup); CompanyAmount is
/// taxable compensation and is added to GrossIncome so it flows through SSS/PHIC/HDMF/WTax
/// like regular pay. Made `internal` (not private) specifically so this can be tested here
/// without a database — see Hrms.Core's InternalsVisibleTo for hrms.test.
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
    public void CompanyAmount_IsAddedToGrossIncomeAndTaxableBenefits()
    {
        var empId = Guid.NewGuid();
        var payload = new CalculatorPayload();
        payload.OneTimeLeavePayouts[new EmployeeKey(empId)] = new List<LeaveApplication> { OneTimePayout(gov: 0, comp: 5_000) };
        var line = Line(20_000);

        PayrollProcessorService.ApplyOneTimeLeavePayoutsToGross(payload, Employee(empId), line);

        line.CompanyFundedLeavePay.Should().Be(5_000);
        line.TaxableBenefits.Should().Be(5_000);
        line.GrossIncome.Should().Be(25_000); // 20,000 + 5,000 company-funded variance
    }

    [Fact]
    public void GovernmentAndCompanyAmounts_BothAccumulate_OnTheSamePayout()
    {
        var empId = Guid.NewGuid();
        var payload = new CalculatorPayload();
        payload.OneTimeLeavePayouts[new EmployeeKey(empId)] = new List<LeaveApplication> { OneTimePayout(gov: 15_000, comp: 5_000) };
        var line = Line(20_000);

        PayrollProcessorService.ApplyOneTimeLeavePayoutsToGross(payload, Employee(empId), line);

        line.GovernmentFundedLeavePay.Should().Be(15_000);
        line.CompanyFundedLeavePay.Should().Be(5_000);
        line.GrossIncome.Should().Be(25_000); // only the company portion is added
    }

    [Fact]
    public void MultiplePayoutsForSameEmployee_AllAccumulate()
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
        line.GrossIncome.Should().Be(23_000);
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
