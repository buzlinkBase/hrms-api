using Hrms.Core.Services;

namespace hrms.test.ServiceTests;

/// <summary>
/// PayrollReportService.ComputeCashBondRemaining -- the one pure calculation inside
/// GetCashBondReportAsync, extracted so it's testable without a database. The query/join logic
/// around it (raw Context.DeductionApplicationDetails/Deductions/DeductionTypes EF access) is
/// NOT unit-tested here for the same reason DeductionAplDtlService.LoadAsync and
/// PayrollReportService.GetDeductionLedgerAsync aren't: it needs its own EF-InMemory +
/// ITenantProvider test harness that doesn't exist in this project yet -- called out to the user
/// rather than silently skipped.
/// </summary>
public class CashBondReportCalculationTests
{
    [Fact]
    public void ComputeCashBondRemaining_SubtractsCollectedFromTarget()
    {
        PayrollReportService.ComputeCashBondRemaining(target: 5000m, totalCollected: 1800m)
            .Should().Be(3200m);
    }

    [Fact]
    public void ComputeCashBondRemaining_FloorsAtZero_WhenOvercollected()
    {
        PayrollReportService.ComputeCashBondRemaining(target: 5000m, totalCollected: 6000m)
            .Should().Be(0m);
    }

    [Fact]
    public void ComputeCashBondRemaining_ReturnsFullTarget_WhenNothingCollected()
    {
        PayrollReportService.ComputeCashBondRemaining(target: 5000m, totalCollected: 0m)
            .Should().Be(5000m);
    }
}
