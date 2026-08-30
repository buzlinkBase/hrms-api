using hrms.test.TestSupport;

namespace hrms.test.ResolverTests;

public class CutoffDivisorResolverTests : TestContextBase
{
    private static readonly CutoffPolicyResolver Resolver = new();
    private static readonly CutoffDivisorResolver DivisorResolver = new();

    private static DeductionPayloadContext Build(DateOnly from, DateOnly to, DateOnly? hireDate = null, params (int Day, bool Eom)[] cutoffs)
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.SEMI_MONTHLY, 0, from, to, hireDate: hireDate);
        foreach (var (day, eom) in cutoffs) AddCutoff(ctx, day, eom);
        return ctx;
    }

    [Fact]
    public void ReleaseFullBalanceNow_AlwaysWinsWithDivisorOne_EvenMidCrossMonth()
    {
        var ctx = Build(new DateOnly(2025, 3, 26), new DateOnly(2025, 4, 10), cutoffs: new[] { (15, false), (31, true) });
        DivisorResolver.Resolve(ctx, Resolver, StatutoryReleaseAction.ReleaseFullBalanceNow).Should().Be(1);
    }

    [Fact]
    public void CrossMonthPeriod_ResolvesDivisorOfOne()
    {
        var ctx = Build(new DateOnly(2025, 3, 26), new DateOnly(2025, 4, 10), cutoffs: new[] { (15, false), (31, true) });
        DivisorResolver.Resolve(ctx, Resolver, StatutoryReleaseAction.UseDefaultProration).Should().Be(1);
    }

    [Fact]
    public void MidPeriodHire_AfterCurrentCutoffDay_ResolvesToConfiguredCutoffCount()
    {
        // Hired the 20th, current cutoff resolves to the 15th (period covers Mar 1-15) —
        // hire date is after the current cutoff day.
        var ctx = Build(new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 15),
            hireDate: new DateOnly(2025, 3, 20), cutoffs: new[] { (15, false), (31, true) });
        DivisorResolver.Resolve(ctx, Resolver, StatutoryReleaseAction.UseDefaultProration).Should().Be(2);
    }

    [Fact]
    public void DefaultFallback_UsesConfiguredCutoffCount_NotHardcodedTwo()
    {
        // 4 configured cutoffs (a Weekly-style group), no special rule fires.
        var ctx = Build(new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 7),
            cutoffs: new[] { (7, false), (14, false), (21, false), (31, true) });
        DivisorResolver.Resolve(ctx, Resolver, StatutoryReleaseAction.UseDefaultProration).Should().Be(4);
    }

    [Fact]
    public void DefaultFallback_UsesTwo_WhenNoCutoffsConfiguredAtAll()
    {
        var ctx = Build(new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 15));
        DivisorResolver.Resolve(ctx, Resolver, StatutoryReleaseAction.UseDefaultProration).Should().Be(2);
    }
}
