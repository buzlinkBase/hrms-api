using hrms.test.TestSupport;

namespace hrms.test.StatutoryTests.HDMF;

/// <summary>
/// Mirrors SSSTableCalculatorTests' scenario shapes and gross figures (HDMF, like PHIC, has
/// no EC component) — see that class for the full self-correction/allocation-strategy
/// rationale. Uses the same bracket amounts as PHICTableCalculatorTests since the mechanics
/// (and therefore the arithmetic) are identical.
/// </summary>
public class HDMFTableCalculatorTests : TestContextBase
{
    private static readonly HDMFModel[] Brackets =
    {
        HDMFBracket(0, 19_999, 450, 450),
        HDMFBracket(20_000, 34_999, 675, 675),
        HDMFBracket(35_000, 999_999, 900, 900),
    };

    private static DeductionPayloadContext BuildSemiMonthly(SalaryType salaryType, decimal periodGross, DateOnly from, DateOnly to)
    {
        var ctx = CreateContext(salaryType, PayrollFrequency.SEMI_MONTHLY, periodGross, from, to);
        AddCutoff(ctx, 15, label: "1st Cutoff");
        AddCutoff(ctx, 31, isEndOfMonth: true, label: "2nd Cutoff");
        SetHDMFRate(ctx, ComputationBasis.Table);
        SeedHDMFTable(ctx, Brackets);
        return ctx;
    }

    [Fact]
    public void SemiMonthly_Fixed_BracketsOffFullMonthlyRate_EvenlySplitAcrossBothCutoffs()
    {
        // Cutoff 1 brackets off the projected 30,000 MonthlyRate (bracket B, EE 675), not
        // just this period's 15,000 half — see SSSTableCalculatorTests' equivalent test.
        var cutoff1 = BuildSemiMonthly(SalaryType.FIXED, 15_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 15));
        var result1 = new DeductionPipeline().Run(cutoff1);
        result1.HDMF.EE.Should().Be(337.5m); // 675 / 2

        var cutoff2 = BuildSemiMonthly(SalaryType.FIXED, 15_000, new DateOnly(2025, 3, 16), new DateOnly(2025, 3, 31));
        cutoff2.Employee.Id = cutoff1.Employee.Id;
        AddPriorPayroll(cutoff2, 15_000);
        AddHDMFContribution(cutoff2, 337.5m, 337.5m);
        var result2 = new DeductionPipeline().Run(cutoff2);
        result2.HDMF.EE.Should().Be(337.5m); // 675 - 337.5 already withheld

        (result1.HDMF.EE + result2.HDMF.EE).Should().Be(675); // evenly split
    }

    [Fact]
    public void SemiMonthly_Variable_SplitsEvenlyAcrossCutoffs_SameAsFixed()
    {
        // See SSSTableCalculatorTests' equivalent test — Variable's bracket lookup stays
        // actual-gross-based, but the withholding split (evenly across the 2 configured
        // cutoffs) is identical to Fixed's.
        var cutoff1 = BuildSemiMonthly(SalaryType.VARIABLE, 12_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 15));
        var result1 = new DeductionPipeline().Run(cutoff1);
        result1.HDMF.EE.Should().Be(225);

        var cutoff2 = BuildSemiMonthly(SalaryType.VARIABLE, 20_000, new DateOnly(2025, 3, 16), new DateOnly(2025, 3, 31));
        cutoff2.Employee.Id = cutoff1.Employee.Id;
        AddPriorPayroll(cutoff2, 12_000);
        AddHDMFContribution(cutoff2, 225, 225);
        var result2 = new DeductionPipeline().Run(cutoff2);
        result2.HDMF.EE.Should().Be(450); // 675 - 225

        (result1.HDMF.EE + result2.HDMF.EE).Should().Be(675);
    }

    [Fact]
    public void SemiMonthly_FirstHalfMonthSchedule_Variable_SecondCutoffStillTruesUpBracketShift()
    {
        // See SSSTableCalculatorTests' equivalent test for the full bug rationale — the
        // second cutoff's true-up branch was unreachable under FirstHalfMonth.
        var cutoff1 = BuildSemiMonthly(SalaryType.VARIABLE, 12_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 15));
        cutoff1.Employee.PayrollGroup!.StatutoryDeductionSchedule = StatutoryDeductionSchedule.FirstHalfMonth;
        var result1 = new DeductionPipeline().Run(cutoff1);
        result1.HDMF.EE.Should().Be(450);

        var cutoff2 = BuildSemiMonthly(SalaryType.VARIABLE, 20_000, new DateOnly(2025, 3, 16), new DateOnly(2025, 3, 31));
        cutoff2.Employee.Id = cutoff1.Employee.Id;
        cutoff2.Employee.PayrollGroup!.StatutoryDeductionSchedule = StatutoryDeductionSchedule.FirstHalfMonth;
        AddPriorPayroll(cutoff2, 12_000);
        AddHDMFContribution(cutoff2, 450, 450);
        var result2 = new DeductionPipeline().Run(cutoff2);
        result2.HDMF.EE.Should().Be(225); // 675 - 450 — must NOT be 0

        (result1.HDMF.EE + result2.HDMF.EE).Should().Be(675);
    }

    [Fact]
    public void SemiMonthly_FirstHalfMonthSchedule_Fixed_ProjectsFullMonthlyRate_NotJustThisCutoffsHalf()
    {
        // See SSSTableCalculatorTests' equivalent test for the full bug rationale — the
        // bracket lookup on the releasing cutoff must use the projected full MonthlyRate,
        // not just this cutoff's half-month gross.
        var cutoff1 = BuildSemiMonthly(SalaryType.FIXED, 15_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 15));
        cutoff1.Employee.PayrollGroup!.StatutoryDeductionSchedule = StatutoryDeductionSchedule.FirstHalfMonth;
        var result1 = new DeductionPipeline().Run(cutoff1);
        result1.HDMF.EE.Should().Be(675); // projected from the 30,000 MonthlyRate — must NOT be 450

        var cutoff2 = BuildSemiMonthly(SalaryType.FIXED, 15_000, new DateOnly(2025, 3, 16), new DateOnly(2025, 3, 31));
        cutoff2.Employee.Id = cutoff1.Employee.Id;
        cutoff2.Employee.PayrollGroup!.StatutoryDeductionSchedule = StatutoryDeductionSchedule.FirstHalfMonth;
        AddPriorPayroll(cutoff2, 15_000);
        AddHDMFContribution(cutoff2, 675, 675);
        var result2 = new DeductionPipeline().Run(cutoff2);
        result2.HDMF.EE.Should().Be(0); // already fully released on cutoff 1
    }

    [Fact]
    public void Monthly_FixedAndVariable_TakeFullBracketBalanceInOneShot()
    {
        var fixedCtx = CreateContext(SalaryType.FIXED, PayrollFrequency.MONTHLY, 22_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 31));
        AddCutoff(fixedCtx, 31, isEndOfMonth: true);
        SetHDMFRate(fixedCtx, ComputationBasis.Table);
        SeedHDMFTable(fixedCtx, Brackets);
        new DeductionPipeline().Run(fixedCtx).HDMF.EE.Should().Be(675);

        var variableCtx = CreateContext(SalaryType.VARIABLE, PayrollFrequency.MONTHLY, 22_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 31));
        AddCutoff(variableCtx, 31, isEndOfMonth: true);
        SetHDMFRate(variableCtx, ComputationBasis.Table);
        SeedHDMFTable(variableCtx, Brackets);
        new DeductionPipeline().Run(variableCtx).HDMF.EE.Should().Be(675);
    }

    [Fact]
    public void Weekly_Fixed_BracketsOffFullMonthlyRate_SplitByRemainingWeeksInMonth()
    {
        // Every non-final week brackets off the projected 30,000 MonthlyRate (bracket B, EE
        // 675) instead of a growing actual-to-date total — see SSSTableCalculatorTests'
        // equivalent test. Only the divisor shrinks week to week (4, 3, 2), not the bracket.
        var empId = NewEmployeeId();
        decimal postedEE = 0, postedER = 0, priorGross = 0, totalEE = 0;
        var weekGross = 7_500m;
        var starts = new[] { 1, 8, 15, 22 };
        var expected = new decimal[] { 168.75m, 225m, 281.25m, 0m };

        for (int i = 0; i < 4; i++)
        {
            var from = new DateOnly(2023, 2, starts[i]);
            var to = from.AddDays(6);
            var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.WEEKLY, weekGross, from, to, employeeId: empId);
            AddCutoff(ctx, 7); AddCutoff(ctx, 14); AddCutoff(ctx, 21); AddCutoff(ctx, 28, isEndOfMonth: true);
            SetHDMFRate(ctx, ComputationBasis.Table);
            SeedHDMFTable(ctx, Brackets);
            if (priorGross > 0) AddPriorPayroll(ctx, priorGross);
            if (postedEE > 0) AddHDMFContribution(ctx, postedEE, postedER);

            var result = new DeductionPipeline().Run(ctx);
            result.HDMF.EE.Should().Be(expected[i], because: $"week starting Feb {starts[i]}");

            postedEE += result.HDMF.EE; postedER += result.HDMF.ER;
            priorGross += weekGross;
            totalEE += result.HDMF.EE;
        }

        totalEE.Should().Be(675);
    }

    [Fact]
    public void Weekly_FirstHalfMonthSchedule_Fixed_ProjectsFullMonthlyRate_ThenTruesUpToZeroOnLastWeek()
    {
        // See SSSTableCalculatorTests' equivalent test for the full bug rationale.
        var empId = NewEmployeeId();
        decimal postedEE = 0, postedER = 0, priorGross = 0;
        var weekGross = 7_500m;
        var starts = new[] { 1, 8, 15, 22 };
        var expected = new decimal[] { 675, 0, 0, 0 };

        for (int i = 0; i < 4; i++)
        {
            var from = new DateOnly(2023, 2, starts[i]);
            var to = from.AddDays(6);
            var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.WEEKLY, weekGross, from, to, employeeId: empId);
            AddCutoff(ctx, 7); AddCutoff(ctx, 14); AddCutoff(ctx, 21); AddCutoff(ctx, 28, isEndOfMonth: true);
            SetHDMFRate(ctx, ComputationBasis.Table);
            SeedHDMFTable(ctx, Brackets);
            ctx.Employee.PayrollGroup!.StatutoryDeductionSchedule = StatutoryDeductionSchedule.FirstHalfMonth;
            if (priorGross > 0) AddPriorPayroll(ctx, priorGross);
            if (postedEE > 0) AddHDMFContribution(ctx, postedEE, postedER);

            var result = new DeductionPipeline().Run(ctx);
            result.HDMF.EE.Should().Be(expected[i], because: $"week starting Feb {starts[i]}");

            postedEE += result.HDMF.EE; postedER += result.HDMF.ER;
            priorGross += weekGross;
        }

        postedEE.Should().Be(675);
    }

    [Fact]
    public void CrossMonthSemiMonthly_Fixed_ReleasesFullRemainingBalanceImmediately()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.SEMI_MONTHLY, 30_000, new DateOnly(2025, 3, 26), new DateOnly(2025, 4, 10));
        AddCutoff(ctx, 15); AddCutoff(ctx, 31, isEndOfMonth: true);
        SetHDMFRate(ctx, ComputationBasis.Table);
        SeedHDMFTable(ctx, Brackets);

        new DeductionPipeline().Run(ctx).HDMF.EE.Should().Be(675);
    }

    // --- OneTime Leave Payout (maternity-style lump sum) coinciding with this period -----
    // See SSSTableCalculatorTests' equivalent tests for the full rationale — a coinciding
    // OneTime payout brackets off the period's actual gross and releases the full bracket
    // amount immediately, bypassing the normal projection/split.

    [Fact]
    public void SemiMonthly_OneTimePayLeave_ReleasesFullBracketAmountImmediately_OnANonLastCutoff()
    {
        var ctx = BuildSemiMonthly(SalaryType.FIXED, 5_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 15));
        AddOneTimeLeavePayout(ctx);

        var result = new DeductionPipeline().Run(ctx);

        result.HDMF.EE.Should().Be(450);
        result.HDMF.ER.Should().Be(450);
    }

    [Fact]
    public void Weekly_OneTimePayLeave_ReleasesFullBracketAmountImmediately_OnANonLastWeek()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.WEEKLY, 5_000, new DateOnly(2025, 2, 1), new DateOnly(2025, 2, 7));
        AddCutoff(ctx, 7); AddCutoff(ctx, 14); AddCutoff(ctx, 21); AddCutoff(ctx, 28, isEndOfMonth: true);
        SetHDMFRate(ctx, ComputationBasis.Table);
        SeedHDMFTable(ctx, Brackets);
        AddOneTimeLeavePayout(ctx);

        var result = new DeductionPipeline().Run(ctx);

        result.HDMF.EE.Should().Be(450);
        result.HDMF.ER.Should().Be(450);
    }

    [Fact]
    public void Daily_OneTimePayLeave_ReleasesFullBracketAmountImmediately_BypassingDayCountProjection()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.DAILY, 5_000, new DateOnly(2025, 3, 3), new DateOnly(2025, 3, 3), monthlyRate: 31_000);
        AddCutoff(ctx, 31, isEndOfMonth: true);
        SetHDMFRate(ctx, ComputationBasis.Table);
        SeedHDMFTable(ctx, Brackets);
        ctx.PayrollLine.TimeHourPayResults = new List<DTRPayModel>();
        AddOneTimeLeavePayout(ctx);

        var result = new DeductionPipeline().Run(ctx);

        result.HDMF.EE.Should().Be(450);
        result.HDMF.ER.Should().Be(450);
    }

    [Fact]
    public void ComputationBasisNone_NeverDeductsRegardlessOfGross()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.MONTHLY, 100_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 31));
        AddCutoff(ctx, 31, isEndOfMonth: true);
        SetHDMFRate(ctx, ComputationBasis.None);
        SeedHDMFTable(ctx, Brackets);

        new DeductionPipeline().Run(ctx).HDMF.EE.Should().Be(0);
    }

    [Fact]
    public void NoHDMFRateConfigured_NeverDeducts()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.MONTHLY, 100_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 31));
        AddCutoff(ctx, 31, isEndOfMonth: true);
        SeedHDMFTable(ctx, Brackets);

        new DeductionPipeline().Run(ctx).HDMF.EE.Should().Be(0);
    }
}
