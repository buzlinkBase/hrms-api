using hrms.test.TestSupport;

namespace hrms.test.StatutoryTests.SSS;

/// <summary>
/// Covers ComputationBasis.Table across all four PayrollFrequency values, for both Fixed
/// (even divisor split, self-corrected on the final period) and Variable (actual-gross-to-
/// date, self-corrected the same way) salary types — see CutoffAllocationStrategy and the
/// per-frequency Table*Calculator classes. Each multi-period test simulates a real payroll
/// group by chaining periods sequentially, feeding forward posted contributions and prior
/// gross exactly as PayrollRangeContextComposerService would across real payroll runs.
/// </summary>
public class SSSTableCalculatorTests : TestContextBase
{
    private static readonly SSSModel[] Brackets =
    {
        SSSBracket(0, 19_999, 900, 1_890, 10),
        SSSBracket(20_000, 34_999, 1_350, 2_840, 10),
        SSSBracket(35_000, 999_999, 1_800, 3_790, 10),
    };

    private static DeductionPayloadContext BuildSemiMonthly(SalaryType salaryType, decimal periodGross, DateOnly from, DateOnly to)
    {
        var ctx = CreateContext(salaryType, PayrollFrequency.SEMI_MONTHLY, periodGross, from, to);
        AddCutoff(ctx, 15, label: "1st Cutoff");
        AddCutoff(ctx, 31, isEndOfMonth: true, label: "2nd Cutoff");
        SetSSSRate(ctx, ComputationBasis.Table);
        SeedSSSTable(ctx, Brackets);
        return ctx;
    }

    [Fact]
    public void SemiMonthly_Fixed_SelfCorrectsToFullMonthBracket_AcrossBothCutoffs()
    {
        // Cutoff 1: 15,000 this period, nothing posted yet -> bracket A (EE 900), divisor 2.
        var cutoff1 = BuildSemiMonthly(SalaryType.FIXED, 15_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 15));
        var result1 = new DeductionPipeline().Run(cutoff1);
        result1.SSS.EE.Should().Be(450); // 900 / 2

        // Cutoff 2: another 15,000 this period + 15,000 already posted this month = 30,000 ->
        // bracket B (EE 1,350). Balance nets out cutoff 1's 450 already withheld, and the
        // second cutoff always takes the full remaining balance regardless of divisor.
        var cutoff2 = BuildSemiMonthly(SalaryType.FIXED, 15_000, new DateOnly(2025, 3, 16), new DateOnly(2025, 3, 31));
        cutoff2.Employee.Id = cutoff1.Employee.Id;
        AddPriorPayroll(cutoff2, 15_000);
        AddSSSContribution(cutoff2, 450, 945, 5);
        var result2 = new DeductionPipeline().Run(cutoff2);
        result2.SSS.EE.Should().Be(900); // 1,350 - 450 already withheld

        (result1.SSS.EE + result2.SSS.EE).Should().Be(1_350); // matches the full-month bracket exactly
    }

    [Fact]
    public void SemiMonthly_Variable_TakesActualGrossBalanceInFull_NotAnEvenSplit()
    {
        // Cutoff 1: 12,000 actually earned -> bracket A (EE 900). Variable takes the whole
        // balance now instead of dividing by 2 — this cutoff's bracket is already based on
        // actual gross, so an even split would under-withhold relative to what was earned.
        var cutoff1 = BuildSemiMonthly(SalaryType.VARIABLE, 12_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 15));
        var result1 = new DeductionPipeline().Run(cutoff1);
        result1.SSS.EE.Should().Be(900);

        // Cutoff 2: 20,000 more actually earned, 12,000 already posted this month -> combined
        // 32,000 gross -> bracket B (EE 1,350). Second cutoff nets out the 900 already
        // withheld and takes the rest in full.
        var cutoff2 = BuildSemiMonthly(SalaryType.VARIABLE, 20_000, new DateOnly(2025, 3, 16), new DateOnly(2025, 3, 31));
        cutoff2.Employee.Id = cutoff1.Employee.Id;
        AddPriorPayroll(cutoff2, 12_000);
        AddSSSContribution(cutoff2, 900, 1_890, 10);
        var result2 = new DeductionPipeline().Run(cutoff2);
        result2.SSS.EE.Should().Be(450); // 1,350 - 900

        (result1.SSS.EE + result2.SSS.EE).Should().Be(1_350);
    }

    [Fact]
    public void SemiMonthly_FirstHalfMonthSchedule_ReleasesFullBalanceOnFirstCutoff_NothingOnSecond()
    {
        var cutoff1 = BuildSemiMonthly(SalaryType.FIXED, 30_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 15));
        cutoff1.Employee.PayrollGroup!.StatutoryDeductionSchedule = StatutoryDeductionSchedule.FirstHalfMonth;
        var result1 = new DeductionPipeline().Run(cutoff1);
        result1.SSS.EE.Should().Be(1_350); // full month's bracket-B amount, released now

        var cutoff2 = BuildSemiMonthly(SalaryType.FIXED, 0, new DateOnly(2025, 3, 16), new DateOnly(2025, 3, 31));
        cutoff2.Employee.Id = cutoff1.Employee.Id;
        cutoff2.Employee.PayrollGroup!.StatutoryDeductionSchedule = StatutoryDeductionSchedule.FirstHalfMonth;
        AddPriorPayroll(cutoff2, 30_000);
        AddSSSContribution(cutoff2, 1_350, 2_840, 10);
        var result2 = new DeductionPipeline().Run(cutoff2);
        result2.SSS.EE.Should().Be(0); // gated out — already released on the first cutoff
    }

    [Fact]
    public void Monthly_FixedAndVariable_TakeFullBracketBalanceInOneShot()
    {
        var fixedCtx = CreateContext(SalaryType.FIXED, PayrollFrequency.MONTHLY, 22_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 31));
        AddCutoff(fixedCtx, 31, isEndOfMonth: true);
        SetSSSRate(fixedCtx, ComputationBasis.Table);
        SeedSSSTable(fixedCtx, Brackets);
        new DeductionPipeline().Run(fixedCtx).SSS.EE.Should().Be(1_350);

        var variableCtx = CreateContext(SalaryType.VARIABLE, PayrollFrequency.MONTHLY, 22_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 31));
        AddCutoff(variableCtx, 31, isEndOfMonth: true);
        SetSSSRate(variableCtx, ComputationBasis.Table);
        SeedSSSTable(variableCtx, Brackets);
        new DeductionPipeline().Run(variableCtx).SSS.EE.Should().Be(1_350);
    }

    [Fact]
    public void Weekly_Fixed_SplitsByRemainingWeeksInMonth_SelfCorrectingToFullBracket()
    {
        // February 2023 (28 days) gives clean remaining-week divisors of 4, 3, 2, 1 for
        // weeks starting the 1st/8th/15th/22nd.
        var empId = NewEmployeeId();
        decimal totalEE = 0;
        decimal postedEE = 0, postedER = 0, postedEC = 0;
        decimal priorGross = 0;
        var weekGross = 7_500m;
        var starts = new[] { 1, 8, 15, 22 };
        var expected = new decimal[] { 225, 300, 675, 150 }; // hand-computed, see class doc

        for (int i = 0; i < 4; i++)
        {
            var from = new DateOnly(2023, 2, starts[i]);
            var to = from.AddDays(6);
            var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.WEEKLY, weekGross, from, to, employeeId: empId);
            AddCutoff(ctx, 7); AddCutoff(ctx, 14); AddCutoff(ctx, 21); AddCutoff(ctx, 28, isEndOfMonth: true);
            SetSSSRate(ctx, ComputationBasis.Table);
            SeedSSSTable(ctx, Brackets);
            if (priorGross > 0) AddPriorPayroll(ctx, priorGross);
            if (postedEE > 0) AddSSSContribution(ctx, postedEE, postedER, postedEC);

            var result = new DeductionPipeline().Run(ctx);
            result.SSS.EE.Should().Be(expected[i], because: $"week starting Feb {starts[i]}");

            postedEE += result.SSS.EE; postedER += result.SSS.ER; postedEC += result.SSS.EC;
            priorGross += weekGross;
            totalEE += result.SSS.EE;
        }

        totalEE.Should().Be(1_350); // full month's bracket-B amount for a 30,000 gross
    }

    [Fact]
    public void Weekly_Variable_TakesFullBalanceEachWeek_ShortCircuitingOnceBracketIsSatisfied()
    {
        var empId = NewEmployeeId();
        decimal totalEE = 0;
        decimal postedEE = 0;
        decimal priorGross = 0;
        var weekGross = 7_500m;
        var starts = new[] { 1, 8, 15, 22 };
        var expected = new decimal[] { 900, 0, 450, 0 }; // hand-computed, see class doc

        for (int i = 0; i < 4; i++)
        {
            var from = new DateOnly(2023, 2, starts[i]);
            var to = from.AddDays(6);
            var ctx = CreateContext(SalaryType.VARIABLE, PayrollFrequency.WEEKLY, weekGross, from, to, employeeId: empId);
            AddCutoff(ctx, 7); AddCutoff(ctx, 14); AddCutoff(ctx, 21); AddCutoff(ctx, 28, isEndOfMonth: true);
            SetSSSRate(ctx, ComputationBasis.Table);
            SeedSSSTable(ctx, Brackets);
            if (priorGross > 0) AddPriorPayroll(ctx, priorGross);
            if (postedEE > 0) AddSSSContribution(ctx, postedEE, 0, 0);

            var result = new DeductionPipeline().Run(ctx);
            result.SSS.EE.Should().Be(expected[i], because: $"week starting Feb {starts[i]}");

            postedEE += result.SSS.EE;
            priorGross += weekGross;
            totalEE += result.SSS.EE;
        }

        totalEE.Should().Be(1_350);
    }

    [Fact]
    public void Daily_Fixed_ProjectsFromMonthlyRate_AndScalesByEffectiveDayCount()
    {
        // 3 working days into a 31-day month, no absences/lates (empty TimeHourPayResults).
        // GrossIncome here seeds DeductionPipeline's RemainingGrossBalance (the DeductionValidator.CanApply
        // gate) — it is NOT what the Daily calculator brackets against; that comes from
        // StatutoryHelper.GetDailyProjectedGrossRate independently. Set generously so the gate
        // isn't what's under test here.
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.DAILY, 31_000, new DateOnly(2025, 3, 3), new DateOnly(2025, 3, 3), monthlyRate: 31_000);
        AddCutoff(ctx, 31, isEndOfMonth: true);
        SetSSSRate(ctx, ComputationBasis.Table);
        SeedSSSTable(ctx, Brackets);
        ctx.PayrollLine.TimeHourPayResults = new List<DTRPayModel>();

        var result = new DeductionPipeline().Run(ctx);

        // Projected gross = remaining daily-rate income over the rest of the month
        // (MonthlyRate/31 * (31-3) = 1,000 * 28 = 28,000) -> bracket B (EE 1,350).
        // TableSSSDailyCalculator clamps effectiveDayCount UP to the current cutoff's day
        // (here 31, the single EOM cutoff configured) whenever today's day is earlier than
        // it — "fallback: clamp to cutoff day" in the calculator's own comment — so
        // CalcRemainingBalance(1350, 1350, divisor=31) * effectiveDayCount(31) = 1350/31*31,
        // capped at the 1,350 balance, i.e. the full month's bracket amount comes out on day
        // 3 already. This is real, existing production behavior for a single-EOM-cutoff
        // Daily setup, not something this test asserts should change.
        result.SSS.EE.Should().Be(1_350m);
    }

    [Fact]
    public void CrossMonthSemiMonthly_Fixed_ReleasesFullRemainingBalanceImmediately()
    {
        // A period that starts in one month and ends in the next is always treated as the
        // month's last cutoff — deduct everything outstanding right now rather than split it.
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.SEMI_MONTHLY, 30_000, new DateOnly(2025, 3, 26), new DateOnly(2025, 4, 10));
        AddCutoff(ctx, 15); AddCutoff(ctx, 31, isEndOfMonth: true);
        SetSSSRate(ctx, ComputationBasis.Table);
        SeedSSSTable(ctx, Brackets);

        var result = new DeductionPipeline().Run(ctx);
        result.SSS.EE.Should().Be(1_350); // bracket B for a 30,000 gross, taken in full on the cross-month cutoff
    }

    [Fact]
    public void ComputationBasisNone_NeverDeductsRegardlessOfGross()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.MONTHLY, 100_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 31));
        AddCutoff(ctx, 31, isEndOfMonth: true);
        SetSSSRate(ctx, ComputationBasis.None);
        SeedSSSTable(ctx, Brackets);

        new DeductionPipeline().Run(ctx).SSS.EE.Should().Be(0);
    }

    [Fact]
    public void NoSSSRateConfigured_NeverDeducts()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.MONTHLY, 100_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 31));
        AddCutoff(ctx, 31, isEndOfMonth: true);
        SeedSSSTable(ctx, Brackets);
        // Employee.SSSRate left null.

        new DeductionPipeline().Run(ctx).SSS.EE.Should().Be(0);
    }
}
