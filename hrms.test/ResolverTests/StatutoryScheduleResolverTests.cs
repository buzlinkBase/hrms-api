using hrms.test.TestSupport;

namespace hrms.test.ResolverTests;

public class StatutoryScheduleResolverTests : TestContextBase
{
    private static readonly CutoffPolicyResolver Resolver = new();

    private static DeductionPayloadContext Build(
        PayrollFrequency frequency, StatutoryDeductionSchedule schedule, DateOnly from, DateOnly to,
        params (int Day, bool Eom)[] cutoffs)
    {
        var ctx = CreateContext(SalaryType.FIXED, frequency, 0, from, to, schedule: schedule);
        foreach (var (day, eom) in cutoffs) AddCutoff(ctx, day, eom);
        return ctx;
    }

    [Fact]
    public void PerPayroll_AlwaysUsesDefaultProration()
    {
        var ctx = Build(PayrollFrequency.SEMI_MONTHLY, StatutoryDeductionSchedule.PerPayroll,
            new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 15), (15, false), (31, true));
        StatutoryScheduleResolver.Resolve(ctx, Resolver).Should().Be(StatutoryReleaseAction.UseDefaultProration);
    }

    [Fact]
    public void FirstHalfMonth_OnFirstCutoff_ReleasesFullBalanceNow()
    {
        var ctx = Build(PayrollFrequency.SEMI_MONTHLY, StatutoryDeductionSchedule.FirstHalfMonth,
            new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 15), (15, false), (31, true));
        StatutoryScheduleResolver.Resolve(ctx, Resolver).Should().Be(StatutoryReleaseAction.ReleaseFullBalanceNow);
    }

    [Fact]
    public void FirstHalfMonth_OnSecondCutoff_ReleasesNothing()
    {
        var ctx = Build(PayrollFrequency.SEMI_MONTHLY, StatutoryDeductionSchedule.FirstHalfMonth,
            new DateOnly(2025, 3, 16), new DateOnly(2025, 3, 31), (15, false), (31, true));
        StatutoryScheduleResolver.Resolve(ctx, Resolver).Should().Be(StatutoryReleaseAction.ReleaseNothing);
    }

    [Fact]
    public void SecondHalfMonth_OnLastCutoff_ReleasesFullBalanceNow()
    {
        var ctx = Build(PayrollFrequency.SEMI_MONTHLY, StatutoryDeductionSchedule.SecondHalfMonth,
            new DateOnly(2025, 3, 16), new DateOnly(2025, 3, 31), (15, false), (31, true));
        StatutoryScheduleResolver.Resolve(ctx, Resolver).Should().Be(StatutoryReleaseAction.ReleaseFullBalanceNow);
    }

    [Fact]
    public void SecondHalfMonth_OnFirstCutoff_ReleasesNothing()
    {
        var ctx = Build(PayrollFrequency.SEMI_MONTHLY, StatutoryDeductionSchedule.SecondHalfMonth,
            new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 15), (15, false), (31, true));
        StatutoryScheduleResolver.Resolve(ctx, Resolver).Should().Be(StatutoryReleaseAction.ReleaseNothing);
    }

    [Theory]
    [InlineData(StatutoryDeductionSchedule.FirstHalfMonth)]
    [InlineData(StatutoryDeductionSchedule.SecondHalfMonth)]
    public void MonthlyFrequency_AlwaysUsesDefaultProration_RegardlessOfSchedule(StatutoryDeductionSchedule schedule)
    {
        var ctx = Build(PayrollFrequency.MONTHLY, schedule, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 31), (31, true));
        StatutoryScheduleResolver.Resolve(ctx, Resolver).Should().Be(StatutoryReleaseAction.UseDefaultProration);
    }

    [Theory]
    [InlineData(StatutoryDeductionSchedule.FirstHalfMonth)]
    [InlineData(StatutoryDeductionSchedule.SecondHalfMonth)]
    public void DailyFrequency_AlwaysUsesDefaultProration_RegardlessOfSchedule(StatutoryDeductionSchedule schedule)
    {
        var ctx = Build(PayrollFrequency.DAILY, schedule, new DateOnly(2025, 3, 5), new DateOnly(2025, 3, 5), (31, true));
        StatutoryScheduleResolver.Resolve(ctx, Resolver).Should().Be(StatutoryReleaseAction.UseDefaultProration);
    }

    [Fact]
    public void NoCutoffsConfigured_FailsOpenToDefaultProration_InsteadOfThrowing()
    {
        var ctx = Build(PayrollFrequency.SEMI_MONTHLY, StatutoryDeductionSchedule.FirstHalfMonth,
            new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 15)); // no cutoffs added
        StatutoryScheduleResolver.Resolve(ctx, Resolver).Should().Be(StatutoryReleaseAction.UseDefaultProration);
    }

    [Fact]
    public void MissingStatutoryDeductionSchedule_DefaultsToPerPayroll()
    {
        var ctx = Build(PayrollFrequency.SEMI_MONTHLY, StatutoryDeductionSchedule.PerPayroll,
            new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 15), (15, false), (31, true));
        ctx.Employee.PayrollGroup = null; // simulate an employee with no payroll group assigned
        StatutoryScheduleResolver.Resolve(ctx, Resolver).Should().Be(StatutoryReleaseAction.UseDefaultProration);
    }
}
