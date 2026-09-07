using hrms.test.TestSupport;

namespace hrms.test.StatutoryTests.SSS;

/// <summary>
/// Covers ComputationBasis.Table across all four PayrollFrequency values. Fixed and Variable
/// salary types split their outstanding balance the same way every cutoff (even divisor
/// split, self-corrected on the final period — see CutoffAllocationStrategy); they only
/// differ in what the bracket lookup itself is based on — Fixed projects the full month from
/// MonthlyRate, Variable brackets off actual gross earned so far (see
/// StatutoryHelper.GetSemiMonthlyBracketBaseRate/GetWeeklyBracketBaseRate). Each multi-period
/// test simulates a real payroll group by chaining periods sequentially, feeding forward
/// posted contributions and prior gross exactly as PayrollRangeContextComposerService would
/// across real payroll runs.
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
    public void SemiMonthly_Fixed_BracketsOffFullMonthlyRate_EvenlySplitAcrossBothCutoffs()
    {
        // Cutoff 1: 15,000 this period, but a FIXED employee's monthly rate (30,000, see
        // CreateContext's default) is known in advance — bracket lookup projects the full
        // month instead of just this period's half, landing in bracket B (EE 1,350) not
        // bracket A. Divisor 2 -> a genuine half, not an under-bracketed guess.
        var cutoff1 = BuildSemiMonthly(SalaryType.FIXED, 15_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 15));
        var result1 = new DeductionPipeline().Run(cutoff1);
        result1.SSS.EE.Should().Be(675); // 1,350 / 2

        // Cutoff 2 (the last cutoff): actual accumulated gross (15,000 + 15,000 posted =
        // 30,000) lands in the same bracket B, so the true-up simply nets out cutoff 1's
        // 675 already withheld — the second cutoff always takes the full remaining balance
        // regardless of divisor.
        var cutoff2 = BuildSemiMonthly(SalaryType.FIXED, 15_000, new DateOnly(2025, 3, 16), new DateOnly(2025, 3, 31));
        cutoff2.Employee.Id = cutoff1.Employee.Id;
        AddPriorPayroll(cutoff2, 15_000);
        AddSSSContribution(cutoff2, 675, 1_420, 5);
        var result2 = new DeductionPipeline().Run(cutoff2);
        result2.SSS.EE.Should().Be(675); // 1,350 - 675 already withheld

        (result1.SSS.EE + result2.SSS.EE).Should().Be(1_350); // matches the full-month bracket exactly, evenly split
    }

    [Fact]
    public void SemiMonthly_Variable_SplitsEvenlyAcrossCutoffs_SameAsFixed()
    {
        // Cutoff 1: 12,000 actually earned -> bracket A (EE 900), split evenly by the 2
        // configured cutoffs -> 450. Variable's bracket lookup is still actual-gross-based
        // (unlike Fixed's monthly-rate projection), but the WITHHOLDING SPLIT is identical
        // to Fixed's — see CutoffAllocationStrategy.
        var cutoff1 = BuildSemiMonthly(SalaryType.VARIABLE, 12_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 15));
        var result1 = new DeductionPipeline().Run(cutoff1);
        result1.SSS.EE.Should().Be(450);

        // Cutoff 2: 20,000 more actually earned, 12,000 already posted this month -> combined
        // 32,000 gross -> bracket B (EE 1,350). Second cutoff always takes the exact
        // remaining balance regardless of divisor, netting out the 450 already withheld.
        var cutoff2 = BuildSemiMonthly(SalaryType.VARIABLE, 20_000, new DateOnly(2025, 3, 16), new DateOnly(2025, 3, 31));
        cutoff2.Employee.Id = cutoff1.Employee.Id;
        AddPriorPayroll(cutoff2, 12_000);
        AddSSSContribution(cutoff2, 450, 945, 5);
        var result2 = new DeductionPipeline().Run(cutoff2);
        result2.SSS.EE.Should().Be(900); // 1,350 - 450

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
    public void SemiMonthly_FirstHalfMonthSchedule_Variable_SecondCutoffStillTruesUpBracketShift()
    {
        // Regression test for the bug where FirstHalfMonth's ReleaseNothing short-circuit
        // ran before the IsSecondCutoff branching, making the second cutoff's "always true
        // up the remaining balance" logic unreachable — silently under-withholding whenever
        // a Variable employee's cumulative gross crossed into a new bracket between cutoffs.
        var cutoff1 = BuildSemiMonthly(SalaryType.VARIABLE, 12_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 15));
        cutoff1.Employee.PayrollGroup!.StatutoryDeductionSchedule = StatutoryDeductionSchedule.FirstHalfMonth;
        var result1 = new DeductionPipeline().Run(cutoff1);
        result1.SSS.EE.Should().Be(900); // bracket A, released in full on cutoff 1

        var cutoff2 = BuildSemiMonthly(SalaryType.VARIABLE, 20_000, new DateOnly(2025, 3, 16), new DateOnly(2025, 3, 31));
        cutoff2.Employee.Id = cutoff1.Employee.Id;
        cutoff2.Employee.PayrollGroup!.StatutoryDeductionSchedule = StatutoryDeductionSchedule.FirstHalfMonth;
        AddPriorPayroll(cutoff2, 12_000);
        AddSSSContribution(cutoff2, 900, 1_890, 10);
        var result2 = new DeductionPipeline().Run(cutoff2);
        result2.SSS.EE.Should().Be(450); // 1,350 (bracket B) - 900 already withheld — must NOT be 0

        (result1.SSS.EE + result2.SSS.EE).Should().Be(1_350); // matches the full-month bracket exactly
    }

    [Fact]
    public void SemiMonthly_FirstHalfMonthSchedule_Fixed_ProjectsFullMonthlyRate_NotJustThisCutoffsHalf()
    {
        // A real Semi-Monthly Fixed employee's cutoff-1 period gross is naturally about
        // half the monthly rate (15,000 here vs a 30,000 MonthlyRate) — before this fix,
        // GetSemiMonthlyGrossBaseRate only saw this cutoff's 15,000 (nothing posted yet on
        // the first cutoff), landing in bracket A (EE 900) instead of the whole-month
        // bracket B (EE 1,350) that "release everything now" is supposed to withhold.
        var cutoff1 = BuildSemiMonthly(SalaryType.FIXED, 15_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 15));
        cutoff1.Employee.PayrollGroup!.StatutoryDeductionSchedule = StatutoryDeductionSchedule.FirstHalfMonth;
        var result1 = new DeductionPipeline().Run(cutoff1);
        result1.SSS.EE.Should().Be(1_350); // projected from the 30,000 MonthlyRate — must NOT be 900

        var cutoff2 = BuildSemiMonthly(SalaryType.FIXED, 15_000, new DateOnly(2025, 3, 16), new DateOnly(2025, 3, 31));
        cutoff2.Employee.Id = cutoff1.Employee.Id;
        cutoff2.Employee.PayrollGroup!.StatutoryDeductionSchedule = StatutoryDeductionSchedule.FirstHalfMonth;
        AddPriorPayroll(cutoff2, 15_000);
        AddSSSContribution(cutoff2, 1_350, 2_840, 10);
        var result2 = new DeductionPipeline().Run(cutoff2);
        result2.SSS.EE.Should().Be(0); // already fully released on cutoff 1
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
    public void Weekly_Fixed_BracketsOffFullMonthlyRate_SplitByRemainingWeeksInMonth()
    {
        // February 2023 (28 days) gives clean remaining-week divisors of 4, 3, 2 for weeks
        // starting the 1st/8th/15th, with the 22nd as the final true-up week. Every non-final
        // week now brackets off the projected 30,000 MonthlyRate (bracket B, EE 1,350)
        // instead of a growing actual-to-date total that used to cross brackets mid-month —
        // only the divisor shrinks week to week, not the bracket itself.
        var empId = NewEmployeeId();
        decimal totalEE = 0;
        decimal postedEE = 0, postedER = 0, postedEC = 0;
        decimal priorGross = 0;
        var weekGross = 7_500m;
        var starts = new[] { 1, 8, 15, 22 };
        var expected = new decimal[] { 337.5m, 450m, 562.5m, 0m }; // hand-computed, see class doc

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
    public void Weekly_Variable_SplitsEvenlyByRemainingWeeks_SameAsFixed()
    {
        // Bracket shifts from A (EE 900) to B (EE 1,350) once combined gross crosses
        // 20,000 (between week 2 and week 3) — the divisor still shrinks week to week (4,
        // 3, 2, then the last week's unconditional true-up), same mechanics as Fixed.
        var empId = NewEmployeeId();
        decimal totalEE = 0;
        decimal postedEE = 0;
        decimal priorGross = 0;
        var weekGross = 7_500m;
        var starts = new[] { 1, 8, 15, 22 };
        var expected = new decimal[] { 225m, 300m, 675m, 150m }; // hand-computed, see class doc

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
    public void Weekly_FirstHalfMonthSchedule_Fixed_ProjectsFullMonthlyRate_ThenTruesUpToZeroOnLastWeek()
    {
        // Week 1's own gross (7,500) is only a quarter of the 30,000 MonthlyRate — before
        // this fix, "release everything now" on week 1 would have bracketed off just that
        // week's gross (bracket A, EE 900) instead of the whole month (bracket B, EE
        // 1,350), and the old ReleaseNothing gate would have blocked any later true-up for
        // the rest of the month.
        var empId = NewEmployeeId();
        decimal postedEE = 0, postedER = 0, postedEC = 0, priorGross = 0;
        var weekGross = 7_500m;
        var starts = new[] { 1, 8, 15, 22 };
        var expected = new decimal[] { 1_350, 0, 0, 0 };

        for (int i = 0; i < 4; i++)
        {
            var from = new DateOnly(2023, 2, starts[i]);
            var to = from.AddDays(6);
            var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.WEEKLY, weekGross, from, to, employeeId: empId);
            AddCutoff(ctx, 7); AddCutoff(ctx, 14); AddCutoff(ctx, 21); AddCutoff(ctx, 28, isEndOfMonth: true);
            SetSSSRate(ctx, ComputationBasis.Table);
            SeedSSSTable(ctx, Brackets);
            ctx.Employee.PayrollGroup!.StatutoryDeductionSchedule = StatutoryDeductionSchedule.FirstHalfMonth;
            if (priorGross > 0) AddPriorPayroll(ctx, priorGross);
            if (postedEE > 0) AddSSSContribution(ctx, postedEE, postedER, postedEC);

            var result = new DeductionPipeline().Run(ctx);
            result.SSS.EE.Should().Be(expected[i], because: $"week starting Feb {starts[i]}");

            postedEE += result.SSS.EE; postedER += result.SSS.ER; postedEC += result.SSS.EC;
            priorGross += weekGross;
        }

        postedEE.Should().Be(1_350);
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

    // --- OneTime Leave Payout (maternity-style lump sum) coinciding with this period -----
    // A OneTime payout's release period brackets off the ACTUAL gross (not a FIXED
    // projection/Variable accumulation — see StatutoryHelperTests) AND releases the full
    // bracket amount immediately, bypassing whatever cutoff-position split/proration the
    // frequency would normally apply — see StatutoryHelper.IsOneTimePayLeave.

    [Fact]
    public void SemiMonthly_OneTimePayLeave_ReleasesFullBracketAmountImmediately_OnANonLastCutoff()
    {
        // Cutoff 1 of 2 (non-last) would normally project the full 30,000 MonthlyRate and
        // split the resulting bracket-B amount in half — see
        // SemiMonthly_Fixed_BracketsOffFullMonthlyRate_EvenlySplitAcrossBothCutoffs. A
        // coinciding OneTime payout instead brackets off this period's actual 5,000 gross
        // (bracket A) and takes it in full despite not being the last cutoff.
        var ctx = BuildSemiMonthly(SalaryType.FIXED, 5_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 15));
        AddOneTimeLeavePayout(ctx);

        var result = new DeductionPipeline().Run(ctx);

        result.SSS.EE.Should().Be(900);
        result.SSS.ER.Should().Be(1_890);
        result.SSS.EC.Should().Be(10);
    }

    [Fact]
    public void Weekly_OneTimePayLeave_ReleasesFullBracketAmountImmediately_OnANonLastWeek()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.WEEKLY, 5_000, new DateOnly(2025, 2, 1), new DateOnly(2025, 2, 7));
        AddCutoff(ctx, 7); AddCutoff(ctx, 14); AddCutoff(ctx, 21); AddCutoff(ctx, 28, isEndOfMonth: true);
        SetSSSRate(ctx, ComputationBasis.Table);
        SeedSSSTable(ctx, Brackets);
        AddOneTimeLeavePayout(ctx);

        var result = new DeductionPipeline().Run(ctx);

        result.SSS.EE.Should().Be(900);
        result.SSS.ER.Should().Be(1_890);
        result.SSS.EC.Should().Be(10);
    }

    [Fact]
    public void Daily_OneTimePayLeave_ReleasesFullBracketAmountImmediately_BypassingDayCountProjection()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.DAILY, 5_000, new DateOnly(2025, 3, 3), new DateOnly(2025, 3, 3), monthlyRate: 31_000);
        AddCutoff(ctx, 31, isEndOfMonth: true);
        SetSSSRate(ctx, ComputationBasis.Table);
        SeedSSSTable(ctx, Brackets);
        ctx.PayrollLine.TimeHourPayResults = new List<DTRPayModel>();
        AddOneTimeLeavePayout(ctx);

        var result = new DeductionPipeline().Run(ctx);

        result.SSS.EE.Should().Be(900);
        result.SSS.ER.Should().Be(1_890);
        result.SSS.EC.Should().Be(10);
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
