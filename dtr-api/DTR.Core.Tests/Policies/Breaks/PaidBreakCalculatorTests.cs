namespace DTR.Core.Tests.Policies.Breaks;

/// <summary>
/// PaidBreakCalculator.ComputePaidBreaks / OverbreakCalculator.ComputeOverbreaks — pure
/// functions with no TimeContext dependency, allocating a fixed paid-break allowance across
/// one or more detected break windows in chronological order, truncating/dropping whatever
/// doesn't fit.
/// </summary>
public class PaidBreakCalculatorTests
{
    private static TimeRecordCollection Breaks(params TimeRecord[] records) => new(records);

    [Fact]
    public void NoBreaks_ReturnsEmpty()
    {
        var result = PaidBreakCalculator.ComputePaidBreaks(new TimeRecordCollection(), 60);

        result.Should().BeEmpty();
    }

    [Fact]
    public void SingleBreakWithinAllowance_KeptInFull()
    {
        var start = new DateTime(2026, 1, 1, 12, 0, 0);
        var breaks = Breaks(new TimeRecord(start, start.AddMinutes(30)));

        var result = PaidBreakCalculator.ComputePaidBreaks(breaks, allowedBreakMinutes: 60);

        result.Should().ContainSingle();
        result[0].StartTime.Should().Be(start);
        result[0].EndTime.Should().Be(start.AddMinutes(30));
    }

    [Fact]
    public void SingleBreakExceedingAllowance_TruncatedToTheAllowance()
    {
        var start = new DateTime(2026, 1, 1, 12, 0, 0);
        var breaks = Breaks(new TimeRecord(start, start.AddMinutes(90)));

        var result = PaidBreakCalculator.ComputePaidBreaks(breaks, allowedBreakMinutes: 60);

        result.Should().ContainSingle();
        result[0].StartTime.Should().Be(start);
        result[0].EndTime.Should().Be(start.AddMinutes(60));
    }

    [Fact]
    public void TwoBreaks_SecondTruncatedByWhateverAllowanceRemains()
    {
        var firstStart = new DateTime(2026, 1, 1, 10, 0, 0);
        var secondStart = new DateTime(2026, 1, 1, 12, 0, 0);
        var breaks = Breaks(
            new TimeRecord(firstStart, firstStart.AddMinutes(20)), // 20 min
            new TimeRecord(secondStart, secondStart.AddMinutes(20))); // 20 min, only 10 left of a 30-min allowance

        var result = PaidBreakCalculator.ComputePaidBreaks(breaks, allowedBreakMinutes: 30);

        result.Should().HaveCount(2);
        result[0].EndTime.Should().Be(firstStart.AddMinutes(20));
        result[1].StartTime.Should().Be(secondStart);
        result[1].EndTime.Should().Be(secondStart.AddMinutes(10));
    }

    [Fact]
    public void ThirdBreak_DroppedEntirelyOnceAllowanceIsExhausted()
    {
        var firstStart = new DateTime(2026, 1, 1, 9, 0, 0);
        var secondStart = new DateTime(2026, 1, 1, 12, 0, 0);
        var thirdStart = new DateTime(2026, 1, 1, 15, 0, 0);
        var breaks = Breaks(
            new TimeRecord(firstStart, firstStart.AddMinutes(30)),
            new TimeRecord(secondStart, secondStart.AddMinutes(30)),
            new TimeRecord(thirdStart, thirdStart.AddMinutes(10)));

        var result = PaidBreakCalculator.ComputePaidBreaks(breaks, allowedBreakMinutes: 60);

        result.Should().HaveCount(2); // the 60-minute allowance is used up by the first two
    }
}

public class OverbreakCalculatorTests
{
    private static TimeRecordCollection Breaks(params TimeRecord[] records) => new(records);

    [Fact]
    public void NoBreaks_ReturnsEmpty()
    {
        var result = OverbreakCalculator.ComputeOverbreaks(new TimeRecordCollection(), 60);

        result.Should().BeEmpty();
    }

    [Fact]
    public void BreakWithinAllowance_NoOverbreak()
    {
        var start = new DateTime(2026, 1, 1, 12, 0, 0);
        var breaks = Breaks(new TimeRecord(start, start.AddMinutes(30)));

        var result = OverbreakCalculator.ComputeOverbreaks(breaks, allowedBreakMinutes: 60);

        result.Should().BeEmpty();
    }

    [Fact]
    public void BreakExceedingAllowance_OverbreakIsOnlyTheExcessPortion()
    {
        var start = new DateTime(2026, 1, 1, 12, 0, 0);
        var breaks = Breaks(new TimeRecord(start, start.AddMinutes(90)));

        var result = OverbreakCalculator.ComputeOverbreaks(breaks, allowedBreakMinutes: 60);

        result.Should().ContainSingle();
        result[0].StartTime.Should().Be(start.AddMinutes(60));
        result[0].EndTime.Should().Be(start.AddMinutes(90));
    }

    [Fact]
    public void SecondBreak_EntirelyOverbreakOnceAllowanceIsExhausted()
    {
        var firstStart = new DateTime(2026, 1, 1, 9, 0, 0);
        var secondStart = new DateTime(2026, 1, 1, 12, 0, 0);
        var breaks = Breaks(
            new TimeRecord(firstStart, firstStart.AddMinutes(60)), // exactly consumes the allowance
            new TimeRecord(secondStart, secondStart.AddMinutes(15)));

        var result = OverbreakCalculator.ComputeOverbreaks(breaks, allowedBreakMinutes: 60);

        result.Should().ContainSingle();
        result[0].StartTime.Should().Be(secondStart);
        result[0].EndTime.Should().Be(secondStart.AddMinutes(15));
    }
}
