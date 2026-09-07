using hrms.test.TestSupport;

namespace hrms.test.ResolverTests;

public class CutoffAllocationStrategyTests : TestContextBase
{
    private static DeductionPayloadContext DummyContext() =>
        CreateContext(SalaryType.FIXED, PayrollFrequency.SEMI_MONTHLY, 0, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 15));

    [Fact]
    public void FixedStrategy_SplitsEvenlyByDivisor_CappedAtBalance()
    {
        var strategy = new FixedDivisorAllocationStrategy();
        strategy.AllocateFirstCutoffShare(1_000, 1_000, DummyContext(), divisor: 2).Should().Be(500);
    }

    [Fact]
    public void FixedStrategy_NeverExceedsTheOutstandingBalance()
    {
        var strategy = new FixedDivisorAllocationStrategy();
        // rate/divisor = 500, but only 300 is left owed this month.
        strategy.AllocateFirstCutoffShare(1_000, 300, DummyContext(), divisor: 2).Should().Be(300);
    }

    [Fact]
    public void FixedStrategy_HonorsDaysWorkedProration_ForMidMonthHires()
    {
        var strategy = new FixedDivisorAllocationStrategy();
        // 10 of 30 days worked -> a third of the rate, regardless of divisor.
        strategy.AllocateFirstCutoffShare(900, 900, DummyContext(), divisor: 2, daysWorked: 10, totalDaysInMonth: 30)
            .Should().Be(300);
    }

    [Fact]
    public void Factory_ResolvesFixedSalaryType_ToFixedDivisorStrategy()
    {
        CutoffAllocationStrategyFactory.Resolve(SalaryType.FIXED).Should().BeOfType<FixedDivisorAllocationStrategy>();
    }

    [Fact]
    public void Factory_ResolvesVariableSalaryType_ToFixedDivisorStrategyToo()
    {
        // Variable now follows the exact same PayrollGroup cutoff-allocation policy as
        // Fixed (50/50 split, FirstHalfMonth, SecondHalfMonth) — see
        // ICutoffAllocationStrategy's doc comment for why a dedicated Variable strategy
        // was retired.
        CutoffAllocationStrategyFactory.Resolve(SalaryType.VARIABLE).Should().BeOfType<FixedDivisorAllocationStrategy>();
    }
}
