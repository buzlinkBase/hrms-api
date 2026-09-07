namespace DTR.Core.Tests.Utilities;

/// <summary>
/// NightDiffTimeSplitter/NightDiffChecker/NightDiffCalculator — pure date-math with no
/// TimeContext dependency. The night-diff window is a fixed 10PM-6AM band; Split walks day by
/// day (starting one day before shiftStart, to catch a window that started the night before)
/// and intersects the shift range against each night-diff band it overlaps.
/// </summary>
public class NightDiffTests
{
    // --- NightDiffTimeSplitter.Split ---------------------------------------------------------

    [Fact]
    public void Split_ShiftEntirelyWithinNightWindow_ReturnsTheFullShiftAsOneSegment()
    {
        var start = new DateTime(2026, 1, 1, 23, 0, 0);
        var end = new DateTime(2026, 1, 2, 2, 0, 0);

        var result = NightDiffTimeSplitter.Split(start, end);

        result.Should().ContainSingle();
        result[0].Start.Should().Be(start);
        result[0].End.Should().Be(end);
    }

    [Fact]
    public void Split_ShiftEntirelyOutsideNightWindow_ReturnsEmpty()
    {
        var start = new DateTime(2026, 1, 1, 8, 0, 0);
        var end = new DateTime(2026, 1, 1, 17, 0, 0);

        var result = NightDiffTimeSplitter.Split(start, end);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Split_ShiftCrossingIntoNightWindow_ReturnsOnlyTheOverlappingPortion()
    {
        var start = new DateTime(2026, 1, 1, 20, 0, 0); // 8 PM
        var end = new DateTime(2026, 1, 1, 23, 0, 0);   // 11 PM -> only 22:00-23:00 is ND

        var result = NightDiffTimeSplitter.Split(start, end);

        result.Should().ContainSingle();
        result[0].Start.Should().Be(new DateTime(2026, 1, 1, 22, 0, 0));
        result[0].End.Should().Be(end);
    }

    [Fact]
    public void Split_ShiftSpanning24Hours_ReturnsBothNightWindows()
    {
        var start = new DateTime(2026, 1, 1, 8, 0, 0);
        var end = new DateTime(2026, 1, 2, 8, 0, 0); // spans day 1's 22:00-06:00 window fully

        var result = NightDiffTimeSplitter.Split(start, end);

        result.Should().ContainSingle(); // one continuous 22:00-06:00 band falls inside [start,end)
        result[0].Start.Should().Be(new DateTime(2026, 1, 1, 22, 0, 0));
        result[0].End.Should().Be(new DateTime(2026, 1, 2, 6, 0, 0));
    }

    [Fact]
    public void Split_EndBeforeStart_ReturnsEmpty()
    {
        var start = new DateTime(2026, 1, 1, 23, 0, 0);
        var end = new DateTime(2026, 1, 1, 22, 0, 0);

        NightDiffTimeSplitter.Split(start, end).Should().BeEmpty();
    }

    // --- NightDiffChecker ---------------------------------------------------------------------

    [Fact]
    public void IsDutyNightDiff_OverlapsNightWindow_ReturnsTrue()
    {
        NightDiffChecker.IsDutyNightDiff(
            new DateTime(2026, 1, 1, 21, 0, 0), new DateTime(2026, 1, 1, 23, 0, 0))
            .Should().BeTrue();
    }

    [Fact]
    public void IsDutyNightDiff_NoOverlap_ReturnsFalse()
    {
        NightDiffChecker.IsDutyNightDiff(
            new DateTime(2026, 1, 1, 8, 0, 0), new DateTime(2026, 1, 1, 17, 0, 0))
            .Should().BeFalse();
    }

    [Fact]
    public void GetTotalMinutes_SumsAllOverlappingNightSegments()
    {
        var minutes = NightDiffChecker.GetTotalMinutes(
            new DateTime(2026, 1, 1, 21, 0, 0), new DateTime(2026, 1, 1, 23, 0, 0)); // 1h inside 22:00-23:00

        minutes.Should().Be(60);
    }

    // --- NightDiffCalculator -------------------------------------------------------------------

    [Fact]
    public void Calculate_DateTimeOverload_ReturnsOnlyTheNightPortion()
    {
        var result = NightDiffCalculator.Calculate(
            new DateTime(2026, 1, 1, 20, 0, 0), new DateTime(2026, 1, 1, 23, 0, 0));

        result.TotalMinutes.Should().Be(60); // 22:00-23:00
    }

    [Fact]
    public void Calculate_TimeRangeOverload_Empty_ReturnsEmpty()
    {
        NightDiffCalculator.Calculate(TimeRange.Empty).IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void Calculate_TimeRecordCollection_MultipleRecords_SumsEachOverlap()
    {
        var records = new TimeRecordCollection
        {
            new TimeRecord(new DateTime(2026, 1, 1, 21, 0, 0), new DateTime(2026, 1, 1, 23, 0, 0)), // 1h ND
            new TimeRecord(new DateTime(2026, 1, 2, 5, 0, 0), new DateTime(2026, 1, 2, 7, 0, 0)),   // 1h ND (05-06)
        };

        var result = NightDiffCalculator.Calculate(records);

        result.TotalMinutes.Should().Be(120);
    }

    [Fact]
    public void Calculate_TimeRecordCollection_NoOverlap_ReturnsEmpty()
    {
        var records = new TimeRecordCollection
        {
            new TimeRecord(new DateTime(2026, 1, 1, 8, 0, 0), new DateTime(2026, 1, 1, 17, 0, 0)),
        };

        NightDiffCalculator.Calculate(records).IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void Calculate_EmptyTimeRecordCollection_ReturnsEmpty()
    {
        NightDiffCalculator.Calculate(new TimeRecordCollection()).IsEmpty().Should().BeTrue();
    }
}
