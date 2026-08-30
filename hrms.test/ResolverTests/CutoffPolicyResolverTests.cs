using hrms.test.TestSupport;

namespace hrms.test.ResolverTests;

/// <summary>
/// CutoffPolicyResolver drives every Table calculator's cutoff-relative behavior (self-
/// correction on the last cutoff, first-cutoff allocation strategy, statutory release
/// schedule gating). These tests exercise it directly, independent of any calculator.
/// </summary>
public class CutoffPolicyResolverTests : TestContextBase
{
    private static readonly CutoffPolicyResolver Resolver = new();

    private static DeductionPayloadContext Build(PayrollFrequency frequency, DateOnly from, DateOnly to, params (int Day, bool Eom)[] cutoffs)
    {
        var ctx = CreateContext(SalaryType.FIXED, frequency, 0, from, to);
        foreach (var (day, eom) in cutoffs) AddCutoff(ctx, day, eom);
        return ctx;
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(15, true)]
    [InlineData(16, false)]
    [InlineData(31, false)]
    public void SemiMonthly_IsFirstCutoff_TrueOnOrBeforeFirstConfiguredDay(int day, bool expected)
    {
        var ctx = Build(PayrollFrequency.SEMI_MONTHLY, new DateOnly(2025, 3, day), new DateOnly(2025, 3, day), (15, false), (31, true));
        Resolver.IsFirstCutoff(ctx).Should().Be(expected);
    }

    [Theory]
    [InlineData(16, true)]
    [InlineData(31, true)]
    [InlineData(15, false)]
    [InlineData(1, false)]
    public void SemiMonthly_IsSecondCutoff_TrueAfterFirstConfiguredDay(int day, bool expected)
    {
        var ctx = Build(PayrollFrequency.SEMI_MONTHLY, new DateOnly(2025, 3, day), new DateOnly(2025, 3, day), (15, false), (31, true));
        Resolver.IsSecondCutoff(ctx).Should().Be(expected);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(15, false)]
    [InlineData(16, true)]
    [InlineData(31, true)]
    public void SemiMonthly_IsLastCutoff_TrueOnOrAfterFinalConfiguredDay(int day, bool expected)
    {
        var ctx = Build(PayrollFrequency.SEMI_MONTHLY, new DateOnly(2025, 3, day), new DateOnly(2025, 3, day), (15, false), (31, true));
        Resolver.IsLastCutoff(ctx).Should().Be(expected);
    }

    [Fact]
    public void CrossMonthPeriod_IsAlwaysLastCutoff_RegardlessOfConfiguredDays()
    {
        var ctx = Build(PayrollFrequency.SEMI_MONTHLY, new DateOnly(2025, 3, 26), new DateOnly(2025, 4, 10), (15, false), (31, true));
        Resolver.IsLastCutoff(ctx).Should().BeTrue();
    }

    [Fact]
    public void Monthly_IsAlwaysFirstAndLastCutoff_NeverSecond()
    {
        var ctx = Build(PayrollFrequency.MONTHLY, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 31), (31, true));
        Resolver.IsFirstCutoff(ctx).Should().BeTrue();
        Resolver.IsSecondCutoff(ctx).Should().BeFalse();
        Resolver.IsLastCutoff(ctx).Should().BeTrue();
    }

    [Fact]
    public void Daily_IsAlwaysFirstAndLastCutoff_NeverSecond()
    {
        var ctx = Build(PayrollFrequency.DAILY, new DateOnly(2025, 3, 5), new DateOnly(2025, 3, 5), (31, true));
        Resolver.IsFirstCutoff(ctx).Should().BeTrue();
        Resolver.IsSecondCutoff(ctx).Should().BeFalse();
        Resolver.IsLastCutoff(ctx).Should().BeTrue();
    }

    [Theory]
    [InlineData(1, false)]   // week 1
    [InlineData(8, false)]   // week 2
    [InlineData(21, false)]  // week 3
    [InlineData(22, true)]   // week 4 (final configured cutoff day 28)
    [InlineData(28, true)]
    public void Weekly_FourConfiguredCutoffs_IsLastCutoff_OnlyOnFinalWeek(int day, bool expected)
    {
        var ctx = Build(PayrollFrequency.WEEKLY, new DateOnly(2025, 3, day), new DateOnly(2025, 3, day), (7, false), (14, false), (21, false), (28, true));
        Resolver.IsLastCutoff(ctx).Should().Be(expected);
    }

    [Fact]
    public void GetCurrentCutoff_ReturnsFirstConfiguredDayOnOrAfterReferenceDay()
    {
        var ctx = Build(PayrollFrequency.SEMI_MONTHLY, new DateOnly(2025, 3, 10), new DateOnly(2025, 3, 15), (15, false), (31, true));
        Resolver.GetCurrentCutoff(ctx).Day.Should().Be(15);
    }

    [Fact]
    public void GetCurrentCutoff_FallsBackToLastConfiguredCutoff_WhenReferenceDayIsPastAll()
    {
        var ctx = Build(PayrollFrequency.SEMI_MONTHLY, new DateOnly(2025, 3, 31), new DateOnly(2025, 3, 31), (15, false), (31, true));
        Resolver.GetCurrentCutoff(ctx).Day.Should().Be(31);
    }

    [Fact]
    public void GetCurrentCutoff_ThrowsCutoffMismatch_WhenNoCutoffsConfigured()
    {
        var ctx = Build(PayrollFrequency.SEMI_MONTHLY, new DateOnly(2025, 3, 10), new DateOnly(2025, 3, 15));
        var act = () => Resolver.GetCurrentCutoff(ctx);
        act.Should().Throw<CutoffMismatchException>();
    }

    [Fact]
    public void GetFirstCutoff_And_GetSecondCutoff_ReturnConfiguredDaysInOrder_EvenIfAddedOutOfOrder()
    {
        var ctx = Build(PayrollFrequency.SEMI_MONTHLY, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 1), (31, true), (15, false));
        Resolver.GetFirstCutoff(ctx).Day.Should().Be(15);
        Resolver.GetSecondCutoff(ctx).Day.Should().Be(31);
    }

    [Fact]
    public void GetSecondCutoff_ThrowsCutoffMismatch_ForMonthlyFrequency()
    {
        var ctx = Build(PayrollFrequency.MONTHLY, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 31), (31, true));
        var act = () => Resolver.GetSecondCutoff(ctx);
        act.Should().Throw<CutoffMismatchException>();
    }

    [Fact]
    public void GetSecondCutoff_ThrowsCutoffMismatch_WhenOnlyOneCutoffConfigured()
    {
        var ctx = Build(PayrollFrequency.WEEKLY, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 7), (7, false));
        var act = () => Resolver.GetSecondCutoff(ctx);
        act.Should().Throw<CutoffMismatchException>();
    }

    [Fact]
    public void IsEndOfMonth_ResolvesToActualLastDayOfTheReferenceMonth()
    {
        // February 2024 is a leap year — end-of-month should resolve to the 29th, not a
        // hardcoded 30/31.
        var ctx = Build(PayrollFrequency.SEMI_MONTHLY, new DateOnly(2024, 2, 29), new DateOnly(2024, 2, 29), (15, false), (1, true));
        Resolver.GetCurrentCutoff(ctx).Day.Should().Be(29);
    }
}
