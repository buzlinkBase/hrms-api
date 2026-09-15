using Hrms.Core.Services;

namespace hrms.test.PayrollRunTests;

/// <summary>
/// PayrollService.ClampRetirementPayout — the defensive clamp applied when a Last Pay's
/// RetirementPayout is settled against RetirementFund.Balance at Post time (the balance could
/// have shifted between Generate and Post, e.g. another of the employee's payrolls posted in
/// between). Made `internal` specifically so this pure math can be tested without a database,
/// matching LeaveLedgerService.ComputeConvertibleLeaveValue's precedent for the analogous
/// leave-conversion math.
/// </summary>
public class RetirementPayoutClampTests
{
    [Fact]
    public void RequestedPayoutWithinBalance_PaysOutTheFullRequestedAmount()
    {
        PayrollService.ClampRetirementPayout(requestedPayout: 5_000m, currentBalance: 10_000m)
            .Should().Be(5_000m);
    }

    [Fact]
    public void RequestedPayoutExceedsBalance_CapsAtTheCurrentBalance()
    {
        // e.g. the balance shrank between this run's Generate and its Post.
        PayrollService.ClampRetirementPayout(requestedPayout: 10_000m, currentBalance: 6_000m)
            .Should().Be(6_000m);
    }

    [Fact]
    public void RequestedPayoutEqualsBalance_PaysOutExactly()
    {
        PayrollService.ClampRetirementPayout(requestedPayout: 7_500m, currentBalance: 7_500m)
            .Should().Be(7_500m);
    }

    [Fact]
    public void ZeroBalance_PaysOutNothing()
    {
        PayrollService.ClampRetirementPayout(requestedPayout: 1_000m, currentBalance: 0m)
            .Should().Be(0m);
    }

    [Fact]
    public void NegativeBalance_PaysOutNothing_RatherThanGoingFurtherNegative()
    {
        PayrollService.ClampRetirementPayout(requestedPayout: 1_000m, currentBalance: -50m)
            .Should().Be(0m);
    }

    [Fact]
    public void NegativeRequestedPayout_NeverPaysOutANegativeAmount()
    {
        PayrollService.ClampRetirementPayout(requestedPayout: -1_000m, currentBalance: 5_000m)
            .Should().Be(0m);
    }
}
