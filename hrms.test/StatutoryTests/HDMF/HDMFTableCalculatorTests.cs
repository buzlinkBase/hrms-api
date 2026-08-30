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
    public void SemiMonthly_Fixed_SelfCorrectsToFullMonthBracket_AcrossBothCutoffs()
    {
        var cutoff1 = BuildSemiMonthly(SalaryType.FIXED, 15_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 15));
        var result1 = new DeductionPipeline().Run(cutoff1);
        result1.HDMF.EE.Should().Be(225);

        var cutoff2 = BuildSemiMonthly(SalaryType.FIXED, 15_000, new DateOnly(2025, 3, 16), new DateOnly(2025, 3, 31));
        cutoff2.Employee.Id = cutoff1.Employee.Id;
        AddPriorPayroll(cutoff2, 15_000);
        AddHDMFContribution(cutoff2, 225, 225);
        var result2 = new DeductionPipeline().Run(cutoff2);
        result2.HDMF.EE.Should().Be(450);

        (result1.HDMF.EE + result2.HDMF.EE).Should().Be(675);
    }

    [Fact]
    public void SemiMonthly_Variable_TakesActualGrossBalanceInFull()
    {
        var cutoff1 = BuildSemiMonthly(SalaryType.VARIABLE, 12_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 15));
        var result1 = new DeductionPipeline().Run(cutoff1);
        result1.HDMF.EE.Should().Be(450);

        var cutoff2 = BuildSemiMonthly(SalaryType.VARIABLE, 20_000, new DateOnly(2025, 3, 16), new DateOnly(2025, 3, 31));
        cutoff2.Employee.Id = cutoff1.Employee.Id;
        AddPriorPayroll(cutoff2, 12_000);
        AddHDMFContribution(cutoff2, 450, 450);
        var result2 = new DeductionPipeline().Run(cutoff2);
        result2.HDMF.EE.Should().Be(225);

        (result1.HDMF.EE + result2.HDMF.EE).Should().Be(675);
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
    public void Weekly_Fixed_SplitsByRemainingWeeksInMonth_SelfCorrectingToFullBracket()
    {
        var empId = NewEmployeeId();
        decimal postedEE = 0, postedER = 0, priorGross = 0, totalEE = 0;
        var weekGross = 7_500m;
        var starts = new[] { 1, 8, 15, 22 };
        var expected = new decimal[] { 112.5m, 150m, 337.5m, 75m };

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
    public void CrossMonthSemiMonthly_Fixed_ReleasesFullRemainingBalanceImmediately()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.SEMI_MONTHLY, 30_000, new DateOnly(2025, 3, 26), new DateOnly(2025, 4, 10));
        AddCutoff(ctx, 15); AddCutoff(ctx, 31, isEndOfMonth: true);
        SetHDMFRate(ctx, ComputationBasis.Table);
        SeedHDMFTable(ctx, Brackets);

        new DeductionPipeline().Run(ctx).HDMF.EE.Should().Be(675);
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
