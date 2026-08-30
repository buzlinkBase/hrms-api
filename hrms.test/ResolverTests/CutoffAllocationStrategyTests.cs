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
    public void VariableStrategy_AlwaysTakesTheFullBalance_IgnoringDivisorAndProration()
    {
        var strategy = new VariableActualGrossAllocationStrategy();
        strategy.AllocateFirstCutoffShare(rate: 1_000, balance: 437.50m, DummyContext(), divisor: 4, daysWorked: 3, totalDaysInMonth: 10)
            .Should().Be(437.50m);
    }

    [Fact]
    public void Factory_ResolvesFixedSalaryType_ToFixedDivisorStrategy()
    {
        CutoffAllocationStrategyFactory.Resolve(SalaryType.FIXED).Should().BeOfType<FixedDivisorAllocationStrategy>();
    }

    [Fact]
    public void Factory_ResolvesVariableSalaryType_ToVariableActualGrossStrategy()
    {
        CutoffAllocationStrategyFactory.Resolve(SalaryType.VARIABLE).Should().BeOfType<VariableActualGrossAllocationStrategy>();
    }
}
