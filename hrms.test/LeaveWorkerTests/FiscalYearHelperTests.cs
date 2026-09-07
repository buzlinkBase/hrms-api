namespace hrms.test.LeaveWorkerTests;

/// <summary>
/// FiscalYearHelper — pure date arithmetic driven entirely by startMonth (1-12, the first
/// calendar month of the fiscal year). No mocking needed; the class is public.
/// </summary>
public class FiscalYearHelperTests
{
    // --- FiscalYearOf --------------------------------------------------------------------------

    [Fact]
    public void FiscalYearOf_CalendarYear_MatchesTheDatesOwnYear()
    {
        FiscalYearHelper.FiscalYearOf(new DateOnly(2026, 6, 15), startMonth: 1).Should().Be(2026);
    }

    [Fact]
    public void FiscalYearOf_NonCalendarFiscalYear_DateAfterStartMonth_UsesCurrentYear()
    {
        // FY2026 = Apr 2026 - Mar 2027 when startMonth=4
        FiscalYearHelper.FiscalYearOf(new DateOnly(2026, 6, 1), startMonth: 4).Should().Be(2026);
    }

    [Fact]
    public void FiscalYearOf_NonCalendarFiscalYear_DateBeforeStartMonth_UsesPreviousYear()
    {
        // Jan 2026 still belongs to FY2025 (Apr 2025 - Mar 2026) when startMonth=4
        FiscalYearHelper.FiscalYearOf(new DateOnly(2026, 1, 15), startMonth: 4).Should().Be(2025);
    }

    // --- PeriodStart / PeriodEnd ---------------------------------------------------------------

    [Fact]
    public void PeriodStart_CalendarYear_IsJan1Utc()
    {
        var start = FiscalYearHelper.PeriodStart(2026, startMonth: 1);
        start.Should().Be(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void PeriodEnd_CalendarYear_IsDec31EndOfDayUtc()
    {
        var end = FiscalYearHelper.PeriodEnd(2026, startMonth: 1);
        end.Should().Be(new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc));
    }

    [Fact]
    public void PeriodStart_NonCalendarFiscalYear_StartsInTheLabeledYear()
    {
        var start = FiscalYearHelper.PeriodStart(2026, startMonth: 4);
        start.Should().Be(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void PeriodEnd_NonCalendarFiscalYear_EndsInMarchOfTheFollowingYear()
    {
        var end = FiscalYearHelper.PeriodEnd(2026, startMonth: 4);
        end.Should().Be(new DateTime(2027, 3, 31, 23, 59, 59, DateTimeKind.Utc));
    }

    // --- LastDayOfFiscalYear --------------------------------------------------------------------

    [Fact]
    public void LastDayOfFiscalYear_CalendarYear_IsDec31()
    {
        FiscalYearHelper.LastDayOfFiscalYear(2026, startMonth: 1).Should().Be(new DateOnly(2026, 12, 31));
    }

    [Fact]
    public void LastDayOfFiscalYear_NonCalendarFiscalYear_IsMarch31OfNextYear()
    {
        FiscalYearHelper.LastDayOfFiscalYear(2026, startMonth: 4).Should().Be(new DateOnly(2027, 3, 31));
    }

    // --- IsFiscalYearStart / IsFiscalYearEnd -----------------------------------------------------

    [Fact]
    public void IsFiscalYearStart_MatchingMonthAndDay1_ReturnsTrue()
    {
        FiscalYearHelper.IsFiscalYearStart(new DateOnly(2026, 4, 1), startMonth: 4).Should().BeTrue();
    }

    [Fact]
    public void IsFiscalYearStart_SameMonthButNotDay1_ReturnsFalse()
    {
        FiscalYearHelper.IsFiscalYearStart(new DateOnly(2026, 4, 2), startMonth: 4).Should().BeFalse();
    }

    [Fact]
    public void IsFiscalYearStart_DifferentMonth_ReturnsFalse()
    {
        FiscalYearHelper.IsFiscalYearStart(new DateOnly(2026, 1, 1), startMonth: 4).Should().BeFalse();
    }

    [Fact]
    public void IsFiscalYearEnd_CalendarYear_Dec31_ReturnsTrue()
    {
        FiscalYearHelper.IsFiscalYearEnd(new DateOnly(2026, 12, 31), startMonth: 1).Should().BeTrue();
    }

    [Fact]
    public void IsFiscalYearEnd_NonCalendarFiscalYear_LastDayOfMonthBeforeStart_ReturnsTrue()
    {
        FiscalYearHelper.IsFiscalYearEnd(new DateOnly(2027, 3, 31), startMonth: 4).Should().BeTrue();
    }

    [Fact]
    public void IsFiscalYearEnd_NonCalendarFiscalYear_NotTheLastDay_ReturnsFalse()
    {
        FiscalYearHelper.IsFiscalYearEnd(new DateOnly(2027, 3, 30), startMonth: 4).Should().BeFalse();
    }

    [Fact]
    public void IsFiscalYearEnd_HandlesFebruaryCorrectlyAcrossLeapYears()
    {
        // Fiscal year closes end of Feb when startMonth=3; Feb 2028 is a leap year (29 days).
        FiscalYearHelper.IsFiscalYearEnd(new DateOnly(2028, 2, 29), startMonth: 3).Should().BeTrue();
        FiscalYearHelper.IsFiscalYearEnd(new DateOnly(2028, 2, 28), startMonth: 3).Should().BeFalse();
    }
}
