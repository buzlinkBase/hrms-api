using hrms.test.TestSupport;

namespace hrms.test.StatutoryTests.WTax;

/// <summary>
/// WTax is computed independently per pay period — BIR's Revised Withholding Tax Table (RR
/// 11-2018) brackets each period's own compensation against that period's own
/// frequency-specific table (Daily/Weekly/Semi-Monthly/Monthly each have distinct
/// thresholds). Unlike SSS/PHIC/HDMF, there is no monthly aggregation, no splitting a
/// "monthly due" across cutoffs, and no cross-period true-up — tax doesn't accumulate a
/// running balance to settle later in the month. See TableWTaxCalculator.
/// </summary>
public class WTaxTableCalculatorTests : TestContextBase
{
    private static readonly WTaxModel[] SemiMonthlyBrackets =
    {
        TaxBracket(0, 19_999, 0, 0m, "SEMI_MONTHLY"),
        TaxBracket(20_000, 34_999, 0, 0.15m, "SEMI_MONTHLY"),
        TaxBracket(35_000, 999_999, 2_250, 0.20m, "SEMI_MONTHLY"),
    };
    private static readonly WTaxModel[] MonthlyBrackets =
    {
        TaxBracket(0, 39_999, 0, 0m, "MONTHLY"),
        TaxBracket(40_000, 69_999, 0, 0.15m, "MONTHLY"),
        TaxBracket(70_000, 1_999_999, 4_500, 0.20m, "MONTHLY"),
    };
    private static readonly WTaxModel[] WeeklyBrackets =
    {
        TaxBracket(0, 9_999, 0, 0m, "WEEKLY"),
        TaxBracket(10_000, 17_499, 0, 0.15m, "WEEKLY"),
        TaxBracket(17_500, 499_999, 1_125, 0.20m, "WEEKLY"),
    };
    private static readonly WTaxModel[] DailyBrackets =
    {
        TaxBracket(0, 999, 0, 0m, "DAILY"),
        TaxBracket(1_000, 1_749, 0, 0.15m, "DAILY"),
        TaxBracket(1_750, 49_999, 112.50m, 0.20m, "DAILY"),
    };

    private static DeductionPipeData Seed(DeductionPayloadContext ctx) =>
        new DeductionPipeData { RemainingGrossBalance = ctx.PayrollLine.GrossIncome };

    [Fact]
    public void SemiMonthly_ComputesIndependentlyPerPeriod_NoMonthlySplitOrCrossPeriodInfluence()
    {
        // 22,000 this period -> bracket B due = (22,000-20,000)*15% = 300 — the FULL
        // period's own due, not divided by 2 the way it used to be.
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.SEMI_MONTHLY, 22_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 15));
        AddCutoff(ctx, 15); AddCutoff(ctx, 31, isEndOfMonth: true);
        SeedTaxTable(ctx, SemiMonthlyBrackets);

        var result = new TableWTaxCalculator().Calculate(ctx, Seed(ctx));
        result.TaxInfo.TaxDue.Should().Be(300);
        result.TaxInfo.TaxableIncome.Should().Be(22_000);
    }

    [Fact]
    public void Monthly_ComputesFromOwnGross()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.MONTHLY, 75_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 31));
        AddCutoff(ctx, 31, isEndOfMonth: true);
        SeedTaxTable(ctx, MonthlyBrackets);

        var result = new TableWTaxCalculator().Calculate(ctx, Seed(ctx));
        result.TaxInfo.TaxDue.Should().Be(5_500); // 4,500 + (75,000-70,000)*20%
    }

    [Fact]
    public void Weekly_UsesWeeklyTable_NotSemiMonthlyOrAnyOtherFrequency()
    {
        // 12,000 would fall in SemiMonthlyBrackets' 0% bracket (due 0) if the tables were
        // ever mixed — asserting a nonzero due here specifically catches that.
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.WEEKLY, 12_000, new DateOnly(2023, 2, 1), new DateOnly(2023, 2, 7));
        AddCutoff(ctx, 7); AddCutoff(ctx, 14); AddCutoff(ctx, 21); AddCutoff(ctx, 28, isEndOfMonth: true);
        SeedTaxTable(ctx, WeeklyBrackets);

        var result = new TableWTaxCalculator().Calculate(ctx, Seed(ctx));
        result.TaxInfo.TaxDue.Should().Be(300); // (12,000-10,000)*15%
    }

    [Fact]
    public void Daily_UsesDailyTable_NotAnAnnualizedProjection()
    {
        // Old behavior multiplied a projected monthly gross by 12 before bracketing — for
        // 1,500/day that's 18,000, wildly outside Daily's own ~700-2,000 threshold scale.
        // Bracketing the day's own 1,500 gross directly is the only way to land here.
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.DAILY, 1_500, new DateOnly(2025, 3, 3), new DateOnly(2025, 3, 3), monthlyRate: 31_000);
        AddCutoff(ctx, 31, isEndOfMonth: true);
        SeedTaxTable(ctx, DailyBrackets);
        ctx.PayrollLine.TimeHourPayResults = new List<DTRPayModel>();

        var result = new TableWTaxCalculator().Calculate(ctx, Seed(ctx));
        result.TaxInfo.TaxDue.Should().Be(75); // (1,500-1,000)*15%
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
        SeedTaxTable(ctx, MonthlyBrackets);

        new DeductionPipeline().Run(ctx).TaxInfo.TaxDue.Should().Be(0);
    }
}
