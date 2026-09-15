using Hrms.Core.Messaging.BenefitWorkers;
using Hrms.Core.Services;

namespace hrms.test.PayrollRunTests;

/// <summary>
/// UniformAllowanceAccrualWorker.IsMonthEarned -- the pure "is this calendar month earned"
/// decision behind the monthly Uniform Allowance accrual, made `internal` specifically so it can
/// be tested without a database, matching ComputeRetirementAccrual/ClampRetirementPayout's
/// precedent this same session.
/// </summary>
public class UniformAllowanceAccrualTests
{
    [Theory]
    [InlineData(1, true)]   // exactly one full month since hire
    [InlineData(5, true)]   // well past one month
    [InlineData(0, false)]  // hired this same calendar month -- not earned yet
    public void TenureMonthsBasis_EarnedOnceAtLeastOneFullMonthHasPassed(int monthsServed, bool expectedEarned)
    {
        UniformAllowanceAccrualWorker.IsMonthEarned(
                BenefitAccrualBasis.TenureMonths, monthsServed, presentDaysThisMonth: 0)
            .Should().Be(expectedEarned);
    }

    [Theory]
    [InlineData(20, true)]  // exactly at the threshold
    [InlineData(25, true)]  // above the threshold
    [InlineData(19, false)] // one short -- pass/fail, no partial credit
    [InlineData(0, false)]
    public void PresentDaysBasis_IsPassFailAgainstTheThreshold_NotProrated(int presentDays, bool expectedEarned)
    {
        UniformAllowanceAccrualWorker.IsMonthEarned(
                BenefitAccrualBasis.PresentDays, monthsServed: 10, presentDaysThisMonth: presentDays)
            .Should().Be(expectedEarned);
    }

    [Fact]
    public void PresentDaysBasis_IgnoresTenureMonths_EvenWhenTenureWouldOtherwiseQualify()
    {
        // High tenure but short on present days this month -- PresentDays basis must not fall
        // back to the TenureMonths rule.
        UniformAllowanceAccrualWorker.IsMonthEarned(
                BenefitAccrualBasis.PresentDays, monthsServed: 24, presentDaysThisMonth: 5)
            .Should().BeFalse();
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
