using hrms.test.TestSupport;

namespace hrms.test.StatutoryTests;

/// <summary>
/// A semi-monthly payroll group whose cutoffs don't reach month-end: 10 and 25. Its periods roll
/// over the month by design -- Feb 26–Mar 10 and Mar 11–25 are March's two runs -- so both belong
/// to March's SSS / PhilHealth / Pag-IBIG (StatutoryMonthResolver), the first is March's first
/// cutoff although it starts after the 10th (CutoffPolicyResolver), and the second sees what the
/// first withheld. Each test chains the two runs the way PayrollRangeContextComposerService feeds
/// them: the first run's payroll and ledger rows (credited to the date the line was) go into the
/// second run's payload.
/// </summary>
public class TenTwentyFiveCutoffStatutoryTests : TestContextBase
{
    private static readonly DateOnly FirstFrom = new(2025, 2, 26);
    private static readonly DateOnly FirstTo = new(2025, 3, 10);
    private static readonly DateOnly SecondFrom = new(2025, 3, 11);
    private static readonly DateOnly SecondTo = new(2025, 3, 25);

    private static readonly SSSModel[] SssBrackets =
    [
        SSSBracket(0, 19_999, 900, 1_890, 10),
        SSSBracket(20_000, 34_999, 1_350, 2_840, 10),
        SSSBracket(35_000, 999_999, 1_800, 3_790, 10),
    ];

    private static readonly PHICModel[] PhicBrackets =
    [
        PHICBracket(0, 19_999, 450, 450),
        PHICBracket(20_000, 34_999, 675, 675),
        PHICBracket(35_000, 999_999, 900, 900),
    ];

    private static readonly HDMFModel[] HdmfBrackets =
    [
        HDMFBracket(0, 19_999, 450, 450),
        HDMFBracket(20_000, 34_999, 675, 675),
        HDMFBracket(35_000, 999_999, 900, 900),
    ];

    private static DeductionPayloadContext Build(
        Guid employeeId, decimal monthlyRate, decimal periodGross, DateOnly from, DateOnly to,
        StatutoryDeductionSchedule schedule, decimal absences = 0)
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.SEMI_MONTHLY, periodGross, from, to,
            monthlyRate: monthlyRate, schedule: schedule, employeeId: employeeId);
        AddCutoff(ctx, 10, label: "1st Cutoff");
        AddCutoff(ctx, 25, label: "2nd Cutoff");
        ctx.PayrollLine.AbsencesAmount = absences;
        // As EmployeePayrollLineService credits the line.
        ctx.PayrollLine.StatutoryCreditDate = StatutoryMonthResolver.CreditDate(
            ctx.Employee.PayrollGroup, from, to, CrossMonthStatutoryCreditPolicy.CutoffStartMonth, null);
        SetSSSRate(ctx, ComputationBasis.Table);
        SeedSSSTable(ctx, SssBrackets);
        SetPHICRate(ctx, ComputationBasis.Table);
        SeedPHICTable(ctx, PhicBrackets);
        SetHDMFRate(ctx, ComputationBasis.Table);
        SeedHDMFTable(ctx, HdmfBrackets);
        return ctx;
    }

    /// <summary>Feeds an earlier run into a later one's payload: its payroll and its ledger rows, credited where its line was.</summary>
    private static void FeedEarlierRun(DeductionPayloadContext later, DeductionPayloadContext earlier, DeductionPipeData result)
    {
        var key = new EmployeeKey(later.Employee.Id);
        later.Payload.PostedPriorPayrolls[key] =
        [
            .. later.Payload.PostedPriorPayrolls.GetValueOrDefault(key) ?? [],
            new Payroll
            {
                EmployeeId = later.Employee.Id, PayrollType = PayrollType.Regular,
                PayPeriodStart = earlier.Payload.FromDate, PayPeriodEnd = earlier.Payload.ToDate, PayrollDate = earlier.Payload.ToDate,
                GrossIncome = earlier.PayrollLine.GrossIncome, AbsencesAmount = earlier.PayrollLine.AbsencesAmount,
            },
        ];
        var creditedTo = earlier.PayrollLine.StatutoryCreditDate;
        AddLedger(later, creditedTo, result.SSS.EE, result.SSS.ER, result.SSS.EC, result.PHIC.EE, result.PHIC.ER, result.HDMF.EE, result.HDMF.ER);
    }

    private static void AddLedger(DeductionPayloadContext ctx, DateOnly creditedTo,
        decimal sssEe, decimal sssEr, decimal sssEc, decimal phicEe, decimal phicEr, decimal hdmfEe, decimal hdmfEr)
    {
        var key = new EmployeeKey(ctx.Employee.Id);
        if (sssEe > 0)
            ctx.Payload.SSSContribution[key] = [.. ctx.Payload.SSSContribution.GetValueOrDefault(key) ?? [],
                new SSSContributionModel { EmployeeId = ctx.Employee.Id, EE = sssEe, ER = sssEr, EC = sssEc, PayrollDate = creditedTo }];
        if (phicEe > 0)
            ctx.Payload.PHICContribution[key] = [.. ctx.Payload.PHICContribution.GetValueOrDefault(key) ?? [],
                new PHICContributionModel { EmployeeId = ctx.Employee.Id, EmployeeShare = phicEe, EmployerShare = phicEr, PayrollDate = creditedTo }];
        if (hdmfEe > 0)
            ctx.Payload.HDMFContribution[key] = [.. ctx.Payload.HDMFContribution.GetValueOrDefault(key) ?? [],
                new HDMFContributionModel { EmployeeId = ctx.Employee.Id, EmployeeShare = hdmfEe, EmployerShare = hdmfEr, PayrollDate = creditedTo }];
    }

    // ── Where each run sits ───────────────────────────────────────────────────

    [Fact]
    public void BothRunsAreCreditedToMarch_TheMonthTheyEndIn()
    {
        var first = Build(Guid.NewGuid(), 30_000, 15_000, FirstFrom, FirstTo, StatutoryDeductionSchedule.PerPayroll);
        var second = Build(Guid.NewGuid(), 30_000, 15_000, SecondFrom, SecondTo, StatutoryDeductionSchedule.PerPayroll);

        StatutoryMonthResolver.RollsOverMonth(first.Employee.PayrollGroup).Should().BeTrue();
        first.PayrollLine.StatutoryCreditDate.Should().Be(FirstTo);
        second.PayrollLine.StatutoryCreditDate.Should().Be(SecondTo);
    }

    [Fact]
    public void TheCrossMonthRun_IsMarchsFirstCutoff_AndTheOtherIsItsLast()
    {
        var resolver = new CutoffPolicyResolver();
        var first = Build(Guid.NewGuid(), 30_000, 15_000, FirstFrom, FirstTo, StatutoryDeductionSchedule.PerPayroll);
        var second = Build(Guid.NewGuid(), 30_000, 15_000, SecondFrom, SecondTo, StatutoryDeductionSchedule.PerPayroll);

        resolver.IsFirstCutoff(first).Should().BeTrue();
        resolver.IsSecondCutoff(first).Should().BeFalse();
        resolver.IsLastCutoff(first).Should().BeFalse("a 10/25 group's cross-month run is the first of the month it ends in");
        resolver.IsFirstCutoff(second).Should().BeFalse();
        resolver.IsSecondCutoff(second).Should().BeTrue();
        resolver.IsLastCutoff(second).Should().BeTrue();
    }

    [Theory]
    [InlineData(StatutoryDeductionSchedule.SecondHalfMonth, StatutoryReleaseAction.ReleaseNothing)]
    [InlineData(StatutoryDeductionSchedule.FirstHalfMonth, StatutoryReleaseAction.ReleaseFullBalanceNow)]
    [InlineData(StatutoryDeductionSchedule.PerPayroll, StatutoryReleaseAction.UseDefaultProration)]
    public void TheScheduleIsHonouredOnTheFirstRun(StatutoryDeductionSchedule schedule, StatutoryReleaseAction expected)
    {
        var first = Build(Guid.NewGuid(), 30_000, 15_000, FirstFrom, FirstTo, schedule);

        StatutoryScheduleResolver.Resolve(first, new CutoffPolicyResolver()).Should().Be(expected);
    }

    [Fact]
    public void AGroupWithCutoffsAtMonthEnd_DoesNotRollOver()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.SEMI_MONTHLY, 15_000, new DateOnly(2025, 3, 26), new DateOnly(2025, 4, 10));
        AddCutoff(ctx, 15);
        AddCutoff(ctx, 31, isEndOfMonth: true);

        StatutoryMonthResolver.RollsOverMonth(ctx.Employee.PayrollGroup).Should().BeFalse();
        StatutoryMonthResolver.CreditDate(ctx.Employee.PayrollGroup, ctx.Payload.FromDate, ctx.Payload.ToDate,
            CrossMonthStatutoryCreditPolicy.CutoffStartMonth, null).Should().Be(ctx.Payload.FromDate, "the company policy still applies");
        new CutoffPolicyResolver().IsLastCutoff(ctx).Should().BeTrue("a 15/EOM cross-month period still clears the month it is credited to");
    }

    [Theory]
    [InlineData(1, 15, true)]
    [InlineData(10, 25, true)]
    [InlineData(15, 30, false)] // a fixed 30th is month-end in practice
    public void WhichGroupsRollOverTheMonth(int first, int second, bool rollsOver)
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.SEMI_MONTHLY, 0, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 15));
        AddCutoff(ctx, first);
        AddCutoff(ctx, second);

        StatutoryMonthResolver.RollsOverMonth(ctx.Employee.PayrollGroup).Should().Be(rollsOver);
    }

    // ── "Last cutoff" (SecondHalfMonth) ───────────────────────────────────────

    [Fact]
    public void LastCutoff_FixedNoDtr_WithholdsNothingOnThe10th_AndTheFullMonthOnThe25th()
    {
        var employee = Guid.NewGuid();
        // No DTR needed: each run is half the monthly rate, nothing deducted for attendance.
        var first = Build(employee, 30_000, 15_000, FirstFrom, FirstTo, StatutoryDeductionSchedule.SecondHalfMonth);
        var result1 = new DeductionPipeline().Run(first);

        result1.SSS.EE.Should().Be(0);
        result1.PHIC.EE.Should().Be(0);
        result1.HDMF.EE.Should().Be(0);

        var second = Build(employee, 30_000, 15_000, SecondFrom, SecondTo, StatutoryDeductionSchedule.SecondHalfMonth);
        FeedEarlierRun(second, first, result1);
        var result2 = new DeductionPipeline().Run(second);

        result2.SSS.EE.Should().Be(1_350, "30,000 for March, bracket B");
        result2.PHIC.EE.Should().Be(675);
        result2.HDMF.EE.Should().Be(675);
    }

    [Fact]
    public void LastCutoff_FixedWithDtr_BracketsOffWhatWasActuallyEarned()
    {
        var employee = Guid.NewGuid();
        // 21,000 a month, 2,000 of absences on the second run: March's gross is 19,000 -> bracket A.
        var first = Build(employee, 21_000, 10_500, FirstFrom, FirstTo, StatutoryDeductionSchedule.SecondHalfMonth);
        var result1 = new DeductionPipeline().Run(first);
        result1.SSS.EE.Should().Be(0);

        var second = Build(employee, 21_000, 8_500, SecondFrom, SecondTo, StatutoryDeductionSchedule.SecondHalfMonth, absences: 2_000);
        FeedEarlierRun(second, first, result1);
        var result2 = new DeductionPipeline().Run(second);

        result2.SSS.EE.Should().Be(900);
        result2.PHIC.EE.Should().Be(450);
        result2.HDMF.EE.Should().Be(450);
    }

    [Fact]
    public void LastCutoff_TheSameEmployeeWithoutDtr_IsInTheHigherBracket()
    {
        var employee = Guid.NewGuid();
        var first = Build(employee, 21_000, 10_500, FirstFrom, FirstTo, StatutoryDeductionSchedule.SecondHalfMonth);
        var result1 = new DeductionPipeline().Run(first);
        var second = Build(employee, 21_000, 10_500, SecondFrom, SecondTo, StatutoryDeductionSchedule.SecondHalfMonth);
        FeedEarlierRun(second, first, result1);

        new DeductionPipeline().Run(second).SSS.EE.Should().Be(1_350, "the full 21,000 is bracket B");
    }

    [Fact]
    public void LastCutoff_FebruarysContributions_AreNotTakenOffMarchs()
    {
        var employee = Guid.NewGuid();
        var first = Build(employee, 30_000, 15_000, FirstFrom, FirstTo, StatutoryDeductionSchedule.SecondHalfMonth);
        var result1 = new DeductionPipeline().Run(first);
        var second = Build(employee, 30_000, 15_000, SecondFrom, SecondTo, StatutoryDeductionSchedule.SecondHalfMonth);
        FeedEarlierRun(second, first, result1);
        // February's own run (Feb 11–25) and what it withheld are loaded too (the composer reads
        // every month the run touches), but belong to February.
        second.Payload.PostedPriorPayrolls[new EmployeeKey(employee)].Add(new Payroll
        {
            EmployeeId = employee, PayrollType = PayrollType.Regular, GrossIncome = 15_000,
            PayPeriodStart = new DateOnly(2025, 2, 11), PayPeriodEnd = new DateOnly(2025, 2, 25), PayrollDate = new DateOnly(2025, 2, 25),
        });
        AddLedger(second, new DateOnly(2025, 2, 25), 1_350, 2_840, 10, 675, 675, 675, 675);

        var result2 = new DeductionPipeline().Run(second);

        result2.SSS.EE.Should().Be(1_350);
        result2.PHIC.EE.Should().Be(675);
        result2.HDMF.EE.Should().Be(675);
    }

    // ── "Per payroll" ─────────────────────────────────────────────────────────

    [Fact]
    public void PerPayroll_SplitsMarchAcrossBothRuns_WithoutDeductingTwice()
    {
        var employee = Guid.NewGuid();
        var first = Build(employee, 30_000, 15_000, FirstFrom, FirstTo, StatutoryDeductionSchedule.PerPayroll);
        var result1 = new DeductionPipeline().Run(first);
        result1.SSS.EE.Should().Be(675, "half of March's 1,350 on its first cutoff");

        var second = Build(employee, 30_000, 15_000, SecondFrom, SecondTo, StatutoryDeductionSchedule.PerPayroll);
        FeedEarlierRun(second, first, result1);
        var result2 = new DeductionPipeline().Run(second);

        result2.SSS.EE.Should().Be(675);
        (result1.SSS.EE + result2.SSS.EE).Should().Be(1_350);
        (result1.PHIC.EE + result2.PHIC.EE).Should().Be(675);
        (result1.HDMF.EE + result2.HDMF.EE).Should().Be(675);
    }

    [Fact]
    public void FirstCutoff_TakesMarchOnThe10th_AndNothingMoreOnThe25th()
    {
        var employee = Guid.NewGuid();
        var first = Build(employee, 30_000, 15_000, FirstFrom, FirstTo, StatutoryDeductionSchedule.FirstHalfMonth);
        var result1 = new DeductionPipeline().Run(first);
        result1.SSS.EE.Should().Be(1_350);

        var second = Build(employee, 30_000, 15_000, SecondFrom, SecondTo, StatutoryDeductionSchedule.FirstHalfMonth);
        FeedEarlierRun(second, first, result1);

        new DeductionPipeline().Run(second).SSS.EE.Should().Be(0);
    }
}
