using Hrms.Core.Services;
using Hrms.Core.Services.UniformAllowance;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;

namespace hrms.test.PayrollRunTests;

/// <summary>
/// A client still on the legacy Client.UniformAllowance/UniformAllowanceBasis columns keeps the
/// old monthly rule through the policy engine: one full month of service (Tenure basis), or at
/// least 20 present days in the month just ended (Present-Days basis), pass/fail.
/// </summary>
public class UniformAllowanceAccrualTests
{
    private static readonly DateOnly CreditDate = new(2026, 6, 1);

    private static UniformCreditDecision Decide(BenefitAccrualBasis basis, DateOnly hired, int presentDays)
    {
        var policy = UniformPolicySnapshot.FromLegacy(
            new Client { Id = Guid.NewGuid(), UniformAllowance = 500m, UniformAllowanceBasis = basis })!;
        return UniformCreditDecider.Decide(
            new UniformCreditCandidate(policy, EmploymentStatus.Regular, false, hired, CreditDate, presentDays), 0);
    }

    [Theory]
    [InlineData("2026-05-01", true)]   // exactly one full month since hire
    [InlineData("2025-12-01", true)]   // well past one month
    [InlineData("2026-05-15", false)]  // hired mid-May -- not a full month on June 1
    public void TenureBasis_EarnedOnceAtLeastOneFullMonthHasPassed(string hired, bool earned)
    {
        var decision = Decide(BenefitAccrualBasis.TenureMonths, DateOnly.Parse(hired), presentDays: 0);

        decision.Outcome.Should().Be(earned ? UniformCreditOutcome.Credited : UniformCreditOutcome.Skipped);
        if (earned) decision.Amount.Should().Be(500m);
        else decision.Reason!.Reason.Should().Be(UniformSkipReason.WaitingPeriod);
    }

    [Theory]
    [InlineData(20, true)]  // exactly at the threshold
    [InlineData(25, true)]  // above the threshold
    [InlineData(19, false)] // one short -- pass/fail, no partial credit
    [InlineData(0, false)]
    public void PresentDaysBasis_IsPassFailAgainstTwentyDays_NotProrated(int presentDays, bool earned)
    {
        var decision = Decide(BenefitAccrualBasis.PresentDays, new DateOnly(2020, 1, 1), presentDays);

        decision.Outcome.Should().Be(earned ? UniformCreditOutcome.Credited : UniformCreditOutcome.Skipped);
        decision.Amount.Should().Be(earned ? 500m : 0m);
    }

    [Fact]
    public void PresentDaysBasis_HasNoWaitingPeriod_ButStillNeedsTheDays()
    {
        Decide(BenefitAccrualBasis.PresentDays, new DateOnly(2026, 5, 20), presentDays: 20)
            .Outcome.Should().Be(UniformCreditOutcome.Credited);
        Decide(BenefitAccrualBasis.PresentDays, new DateOnly(2016, 1, 1), presentDays: 5)
            .Reason!.Text.Should().Be("5 of 20 required present days");
    }

    [Fact]
    public void ClientWithoutARate_HasNoLegacyPolicy()
    {
        UniformPolicySnapshot.FromLegacy(new Client { UniformAllowance = null }).Should().BeNull();
        UniformPolicySnapshot.FromLegacy(new Client { UniformAllowance = 0 }).Should().BeNull();
    }
}

/// <summary>
/// UniformAllowanceFundService.ClampAmount -- the defensive clamp shared by AdjustAsync's
/// "Remove" direction and ReleaseBatchAsync, mirroring PayrollService.ClampRetirementPayout's
/// exact reasoning and test shape.
/// </summary>
public class UniformAllowanceClampTests
{
    [Fact]
    public void RequestedAmountWithinBalance_UsesTheFullRequestedAmount()
    {
        UniformAllowanceFundService.ClampAmount(requestedAmount: 300m, currentBalance: 1_000m)
            .Should().Be(300m);
    }

    [Fact]
    public void RequestedAmountExceedsBalance_CapsAtTheCurrentBalance()
    {
        UniformAllowanceFundService.ClampAmount(requestedAmount: 1_500m, currentBalance: 900m)
            .Should().Be(900m);
    }

    [Fact]
    public void RequestedAmountEqualsBalance_UsesItExactly()
    {
        UniformAllowanceFundService.ClampAmount(requestedAmount: 500m, currentBalance: 500m)
            .Should().Be(500m);
    }

    [Fact]
    public void ZeroBalance_ClampsToZero()
    {
        UniformAllowanceFundService.ClampAmount(requestedAmount: 100m, currentBalance: 0m)
            .Should().Be(0m);
    }

    [Fact]
    public void NegativeBalance_ClampsToZero_RatherThanGoingFurtherNegative()
    {
        UniformAllowanceFundService.ClampAmount(requestedAmount: 100m, currentBalance: -20m)
            .Should().Be(0m);
    }

    [Fact]
    public void NegativeRequestedAmount_NeverProducesANegativeResult()
    {
        UniformAllowanceFundService.ClampAmount(requestedAmount: -50m, currentBalance: 500m)
            .Should().Be(0m);
    }
}
