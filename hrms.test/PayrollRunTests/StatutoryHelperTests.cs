using hrms.test.TestSupport;

namespace hrms.test.PayrollRunTests;

/// <summary>
/// StatutoryHelper.Get*GrossBaseRate is the gross-alignment fix from earlier this session:
/// Fixed and Variable employees now both bracket off PayrollLine.GrossIncome (the upstream,
/// authoritative figure PayrollProcessorService already computes) instead of Fixed using an
/// independent MonthlyRate-based approximation that omitted rest-day/holiday/ND pay and other
/// components already folded into GrossIncome.
/// </summary>
public class StatutoryHelperTests : TestContextBase
{
    [Theory]
    [InlineData(SalaryType.FIXED)]
    [InlineData(SalaryType.VARIABLE)]
    public void GetMonthlyGrossBaseRate_ReturnsPayrollLineGrossIncome_ForBothSalaryTypes(SalaryType salaryType)
    {
        var ctx = CreateContext(salaryType, PayrollFrequency.MONTHLY, 27_500, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 31));
        StatutoryHelper.GetMonthlyGrossBaseRate(ctx).Should().Be(27_500);
    }

    [Theory]
    [InlineData(SalaryType.FIXED)]
    [InlineData(SalaryType.VARIABLE)]
    public void GetSemiMonthlyGrossBaseRate_AccumulatesThisCutoffPlusPriorPostedGross(SalaryType salaryType)
    {
        var ctx = CreateContext(salaryType, PayrollFrequency.SEMI_MONTHLY, 16_000, new DateOnly(2025, 3, 16), new DateOnly(2025, 3, 31));
        AddPriorPayroll(ctx, 14_000);
        StatutoryHelper.GetSemiMonthlyGrossBaseRate(ctx).Should().Be(30_000);
    }

    [Fact]
    public void GetSemiMonthlyGrossBaseRate_WithNoPriorPayroll_IsJustThisCutoffsGross()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.SEMI_MONTHLY, 15_000, new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 15));
        StatutoryHelper.GetSemiMonthlyGrossBaseRate(ctx).Should().Be(15_000);
    }

    [Theory]
    [InlineData(SalaryType.FIXED)]
    [InlineData(SalaryType.VARIABLE)]
    public void GetWeeklyGrossBaseRate_AccumulatesThisWeekPlusPriorPostedGross(SalaryType salaryType)
    {
        var ctx = CreateContext(salaryType, PayrollFrequency.WEEKLY, 7_500, new DateOnly(2025, 3, 8), new DateOnly(2025, 3, 14));
        AddPriorPayroll(ctx, 7_500);
        StatutoryHelper.GetWeeklyGrossBaseRate(ctx).Should().Be(15_000);
    }

    [Fact]
    public void IsHiredThisMonth_TrueWhenHireDateSharesYearAndMonthWithFromDate()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.SEMI_MONTHLY, 0,
            new DateOnly(2025, 3, 16), new DateOnly(2025, 3, 31), hireDate: new DateOnly(2025, 3, 20));
        StatutoryHelper.IsHiredThisMonth(ctx).Should().BeTrue();
    }

    [Fact]
    public void IsHiredThisMonth_FalseForAnEstablishedEmployee()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.SEMI_MONTHLY, 0,
            new DateOnly(2025, 3, 16), new DateOnly(2025, 3, 31), hireDate: new DateOnly(2020, 1, 1));
        StatutoryHelper.IsHiredThisMonth(ctx).Should().BeFalse();
    }

    [Fact]
    public void IsCrossMonh_TrueWhenPeriodSpansTwoCalendarMonths()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.SEMI_MONTHLY, 0,
            new DateOnly(2025, 3, 26), new DateOnly(2025, 4, 10));
        StatutoryHelper.IsCrossMonh(ctx).Should().BeTrue();
    }

    [Fact]
    public void IsCrossMonh_FalseWithinASingleCalendarMonth()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.SEMI_MONTHLY, 0,
            new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 15));
        StatutoryHelper.IsCrossMonh(ctx).Should().BeFalse();
    }

    [Fact]
    public void CalcRemainingBalance_SplitsByDivisor_CappedAtBalance()
    {
        StatutoryHelper.CalcRemainingBalance(1_000, 1_000, divisor: 4).Should().Be(250);
        StatutoryHelper.CalcRemainingBalance(1_000, 100, divisor: 4).Should().Be(100); // capped
    }

    [Fact]
    public void CalcRemainingBalance_ReturnsZero_WhenBalanceIsAlreadyZeroOrNegative()
    {
        StatutoryHelper.CalcRemainingBalance(1_000, 0, divisor: 2).Should().Be(0);
        StatutoryHelper.CalcRemainingBalance(1_000, -50, divisor: 2).Should().Be(0);
    }

    [Fact]
    public void CalcRemainingBalance_UsesDaysWorkedProration_OverDivisor_WhenBothProvided()
    {
        // 15 of 30 days -> half the rate, regardless of the divisor value passed alongside it.
        StatutoryHelper.CalcRemainingBalance(1_000, 1_000, divisor: 4, daysWorked: 15, totalDaysInMonth: 30)
            .Should().Be(500);
    }

    [Fact]
    public void CalcRemainingBalance_ClampsDaysWorkedToTotalDaysInMonth_AvoidingOvershoot()
    {
        StatutoryHelper.CalcRemainingBalance(1_000, 1_000, divisor: 1, daysWorked: 45, totalDaysInMonth: 30)
            .Should().Be(1_000); // effectiveDays clamped to 30/30 = full rate, capped at balance
    }

    [Fact]
    public void CalcRemainingBalance_TreatsNonPositiveDivisorAsOne_SafetyFallback()
    {
        StatutoryHelper.CalcRemainingBalance(1_000, 1_000, divisor: 0).Should().Be(1_000);
        StatutoryHelper.CalcRemainingBalance(1_000, 1_000, divisor: -3).Should().Be(1_000);
    }

    [Fact]
    public void IsPartialDeduction_FalseForAnEstablishedEmployee_RegardlessOfFrequency()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.DAILY, 0,
            new DateOnly(2025, 3, 5), new DateOnly(2025, 3, 5), hireDate: new DateOnly(2020, 1, 1));
        StatutoryHelper.IsPartialDeduction(ctx).Should().BeFalse();
    }

    [Fact]
    public void IsPartialDeduction_AlwaysTrueForDaily_WhenHiredThisMonth()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.DAILY, 0,
            new DateOnly(2025, 3, 20), new DateOnly(2025, 3, 20), hireDate: new DateOnly(2025, 3, 20));
        StatutoryHelper.IsPartialDeduction(ctx).Should().BeTrue();
    }

    [Fact]
    public void IsPartialDeduction_SemiMonthly_FalseOnFirstCutoff_TrueOnSecond_WhenHiredThisMonth()
    {
        var firstCutoffCtx = CreateContext(SalaryType.FIXED, PayrollFrequency.SEMI_MONTHLY, 0,
            new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 15), hireDate: new DateOnly(2025, 3, 5));
        AddCutoff(firstCutoffCtx, 15); AddCutoff(firstCutoffCtx, 31, isEndOfMonth: true);
        StatutoryHelper.IsPartialDeduction(firstCutoffCtx).Should().BeFalse();

        var secondCutoffCtx = CreateContext(SalaryType.FIXED, PayrollFrequency.SEMI_MONTHLY, 0,
            new DateOnly(2025, 3, 16), new DateOnly(2025, 3, 31), hireDate: new DateOnly(2025, 3, 5));
        AddCutoff(secondCutoffCtx, 15); AddCutoff(secondCutoffCtx, 31, isEndOfMonth: true);
        StatutoryHelper.IsPartialDeduction(secondCutoffCtx).Should().BeTrue();
    }

    // --- IsOneTimePayLeave / one-time-leave-payout bracket short-circuit ------------------
    // A OneTime-payout leave (e.g. an SSS maternity lump sum) coinciding with this payroll
    // period means statutory contributions should bracket off the ACTUAL gross released this
    // period, not a FIXED monthly projection or Variable prior-gross accumulation — see
    // StatutoryHelper.IsOneTimePayLeave and its callers.

    [Fact]
    public void IsOneTimePayLeave_TrueWhenReleaseDateFallsWithinThePayrollPeriod()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.SEMI_MONTHLY, 5_000,
            new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 15));
        AddOneTimeLeavePayout(ctx, releasePayrollDate: new DateOnly(2025, 3, 10));

        StatutoryHelper.IsOneTimePayLeave(ctx).Should().BeTrue();
    }

    [Fact]
    public void IsOneTimePayLeave_FalseWhenReleaseDateFallsOutsideThePayrollPeriod()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.SEMI_MONTHLY, 5_000,
            new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 15));
        AddOneTimeLeavePayout(ctx, releasePayrollDate: new DateOnly(2025, 3, 20));

        StatutoryHelper.IsOneTimePayLeave(ctx).Should().BeFalse();
    }

    [Fact]
    public void IsOneTimePayLeave_FalseWhenNoPayoutsExistForThisEmployee()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.SEMI_MONTHLY, 5_000,
            new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 15));

        StatutoryHelper.IsOneTimePayLeave(ctx).Should().BeFalse();
    }

    [Fact]
    public void GetSemiMonthlyBracketBaseRate_OneTimePayLeave_ReturnsActualGrossIncome_NotFixedProjection()
    {
        // FIXED, non-last cutoff would normally project the full 30,000 MonthlyRate (see
        // SemiMonthly_Fixed_BracketsOffFullMonthlyRate_EvenlySplitAcrossBothCutoffs in
        // SSSTableCalculatorTests) — a coinciding OneTime payout bypasses that entirely.
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.SEMI_MONTHLY, 5_000,
            new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 15));
        AddOneTimeLeavePayout(ctx);

        StatutoryHelper.GetSemiMonthlyBracketBaseRate(ctx, new CutoffPolicyResolver()).Should().Be(5_000);
    }

    [Fact]
    public void GetWeeklyBracketBaseRate_OneTimePayLeave_ReturnsActualGrossIncome_NotFixedProjection()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.WEEKLY, 5_000,
            new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 7));
        AddCutoff(ctx, 7); AddCutoff(ctx, 14); AddCutoff(ctx, 21); AddCutoff(ctx, 31, isEndOfMonth: true);
        AddOneTimeLeavePayout(ctx);

        StatutoryHelper.GetWeeklyBracketBaseRate(ctx, new CutoffPolicyResolver()).Should().Be(5_000);
    }

    [Fact]
    public void GetWeeklyGrossBaseRate_OneTimePayLeave_ReturnsThisPeriodsGrossOnly_IgnoringPriorPostedGross()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.WEEKLY, 5_000,
            new DateOnly(2025, 3, 8), new DateOnly(2025, 3, 14));
        AddPriorPayroll(ctx, 7_500);
        AddOneTimeLeavePayout(ctx);

        StatutoryHelper.GetWeeklyGrossBaseRate(ctx).Should().Be(5_000); // not 12,500
    }

    [Fact]
    public void GetDailyProjectedGrossRate_OneTimePayLeave_ProjectsGrossIncomeDirectly_BypassingDayCountProjection()
    {
        var ctx = CreateContext(SalaryType.FIXED, PayrollFrequency.DAILY, 5_000,
            new DateOnly(2025, 3, 3), new DateOnly(2025, 3, 3), monthlyRate: 31_000);
        AddOneTimeLeavePayout(ctx);

        var result = StatutoryHelper.GetDailyProjectedGrossRate(ctx);

        result.ProjectedGross.Should().Be(5_000);
        result.BaseRate.Should().Be(5_000);
        result.Divisor.Should().Be(1);
    }
}
