using hrms.test.TestSupport;

namespace hrms.test.StatutoryTests.WTax;

/// <summary>
/// WTax's Table calculators predate this session's Strategy/CoR refactor (no
/// CutoffAllocationStrategy/CutoffDivisorResolver involvement — Fixed and Variable both go
/// through the same divisor logic) but share StatutoryHelper.Get*GrossBaseRate for the
/// taxable-income base, so the gross-alignment fix still applies here. Calculators are
/// invoked directly (not through DeductionPipeline) to keep each test isolated from
/// SSS/PHIC/HDMF, since TableWTaxXCalculator subtracts those EE shares from its base.
/// </summary>
public class WTaxTableCalculatorTests : TestContextBase
{
    private static readonly WTaxModel[] Brackets =
    {
        TaxBracket(0, 19_999, 0, 0m),
        TaxBracket(20_000, 34_999, 0, 0.15m),
        TaxBracket(35_000, 999_999, 2_250, 0.20m),
    };

    private static DeductionPipeData Seed(DeductionPayloadContext ctx) =>
        new DeductionPipeData { RemainingGrossBalance = ctx.PayrollLine.GrossIncome };

    private static DeductionPayloadContext BuildSemiMonthly(decimal periodGross, DateOnly from, DateOnly to)
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.SEMI_MONTHLY, periodGross, from, to);
        AddCutoff(ctx, 15, label: "1st Cutoff");
        AddCutoff(ctx, 31, isEndOfMonth: true, label: "2nd Cutoff");
        SeedTaxTable(ctx, Brackets);
        return ctx;
    }

    [Fact]
    public void SemiMonthly_SelfCorrectsToFullMonthDue_AcrossBothCutoffs()
    {
        // Cutoff 1: 22,000 this period -> bracket B due = (22,000-20,000)*15% = 300. First
        // cutoff of a 2-cutoff group splits evenly (divisor 2).
        var cutoff1 = BuildSemiMonthly(22_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 15));
        var result1 = new TableWTaxSemiMonthlyCalculator(new CutoffPolicyResolver()).Calculate(cutoff1, Seed(cutoff1));
        result1.TaxInfo.TaxDue.Should().Be(150);

        // Cutoff 2: 22,000 more + 22,000 already posted this month = 44,000 -> bracket C due
        // = 2,250 + (44,000-35,000)*20% = 4,050. Last cutoff always takes the rest in full.
        var cutoff2 = BuildSemiMonthly(22_000, new DateOnly(2025, 3, 16), new DateOnly(2025, 3, 31));
        cutoff2.Employee.Id = cutoff1.Employee.Id;
        AddPriorPayroll(cutoff2, 22_000);
        AddTaxContribution(cutoff2, 150);
        var result2 = new TableWTaxSemiMonthlyCalculator(new CutoffPolicyResolver()).Calculate(cutoff2, Seed(cutoff2));
        result2.TaxInfo.TaxDue.Should().Be(3_900); // 4,050 - 150 already withheld

        (result1.TaxInfo.TaxDue + result2.TaxInfo.TaxDue).Should().Be(4_050);
    }

    [Fact]
    public void Monthly_TakesFullDueInOneShot()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.MONTHLY, 44_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 31));
        AddCutoff(ctx, 31, isEndOfMonth: true);
        SeedTaxTable(ctx, Brackets);

        var result = new TableWTaxMonthlyCalculator(new CutoffPolicyResolver()).Calculate(ctx, Seed(ctx));
        result.TaxInfo.TaxDue.Should().Be(4_050);
    }

    [Fact]
    public void Weekly_LastWeekOfMonth_TakesFullRemainingBalance()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.WEEKLY, 44_000, new DateOnly(2023, 2, 22), new DateOnly(2023, 2, 28));
        AddCutoff(ctx, 7); AddCutoff(ctx, 14); AddCutoff(ctx, 21); AddCutoff(ctx, 28, isEndOfMonth: true);
        SeedTaxTable(ctx, Brackets);
        AddTaxContribution(ctx, 1_000); // simulate partial withholding from earlier weeks

        var result = new TableWTaxWeeklyCalculator(new CutoffPolicyResolver()).Calculate(ctx, Seed(ctx));
        result.TaxInfo.TaxDue.Should().Be(3_050); // 4,050 full-month due - 1,000 already withheld
    }

    [Fact]
    public void Weekly_FirstWeekOfMonth_SplitsAcrossTotalWeeksInMonth_NotTakenInFull()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.WEEKLY, 44_000, new DateOnly(2023, 2, 1), new DateOnly(2023, 2, 7));
        AddCutoff(ctx, 7); AddCutoff(ctx, 14); AddCutoff(ctx, 21); AddCutoff(ctx, 28, isEndOfMonth: true);
        SeedTaxTable(ctx, Brackets);

        var result = new TableWTaxWeeklyCalculator(new CutoffPolicyResolver()).Calculate(ctx, Seed(ctx));
        result.TaxInfo.TaxDue.Should().BeGreaterThan(0).And.BeLessThan(4_050);
    }

    [Fact]
    public void Daily_AlwaysSettlesInFull_BecauseIsLastCutoffIsUnconditionallyTrueForDaily()
    {
        // CutoffPolicyResolver.IsLastCutoff hard-returns true for DAILY frequency regardless
        // of which day of the month it is — so TableWTaxDailyCalculator always takes the
        // "Month-End Settlement" branch (full remaining balance), unlike SSS/PHIC/HDMF's
        // Daily calculators, which prorate by effective day count instead.
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.DAILY, 44_000, new DateOnly(2025, 3, 3), new DateOnly(2025, 3, 3), monthlyRate: 44_000);
        AddCutoff(ctx, 31, isEndOfMonth: true);
        SeedTaxTable(ctx, Brackets);
        ctx.PayrollLine.TimeHourPayResults = new List<DTRPayModel>();

        var result = new TableWTaxDailyCalculator(new CutoffPolicyResolver()).Calculate(ctx, Seed(ctx));
        result.TaxInfo.TaxDue.Should().BeGreaterThan(0);
        result.Metadata["Status"].Should().Be("Month-End Settlement");
    }

    [Fact]
    public void CrossMonthSemiMonthly_ReleasesFullRemainingDueImmediately()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.SEMI_MONTHLY, 44_000, new DateOnly(2025, 3, 26), new DateOnly(2025, 4, 10));
        AddCutoff(ctx, 15); AddCutoff(ctx, 31, isEndOfMonth: true);
        SeedTaxTable(ctx, Brackets);

        var result = new TableWTaxSemiMonthlyCalculator(new CutoffPolicyResolver()).Calculate(ctx, Seed(ctx));
        result.TaxInfo.TaxDue.Should().Be(4_050);
    }

    [Fact]
    public void FixedPerPayrollBasis_UsesFlatConfiguredRate_IgnoringGross()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.SEMI_MONTHLY, 999_999, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 15));
        SetTaxRate(ctx, ComputationBasis.FixedPerPayroll, ee: 500);

        var result = new DeductionPipeline().Run(ctx);
        result.TaxInfo.TaxDue.Should().Be(500);
    }

    [Fact]
    public void ComputationBasisNone_NeverDeductsRegardlessOfGross()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.MONTHLY, 200_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 31));
        AddCutoff(ctx, 31, isEndOfMonth: true);
        SetTaxRate(ctx, ComputationBasis.None);
        SeedTaxTable(ctx, Brackets);

        new DeductionPipeline().Run(ctx).TaxInfo.TaxDue.Should().Be(0);
    }
}
