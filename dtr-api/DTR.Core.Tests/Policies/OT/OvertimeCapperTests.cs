using DTR.Core.Tests.TestSupport;

namespace DTR.Core.Tests.Policies.OT;

/// <summary>
/// OvertimeCapper.Cap — caps total OT to CurrentShift.MaxOvertimeHours (null/0 = unlimited),
/// keeping the earliest minutes and dropping the rest.
/// </summary>
public class OvertimeCapperTests : DtrTestBase
{
    private static TimeContext BuildContext(double? maxOvertimeHours)
    {
        var context = CreateContext(new DateTime(2026, 1, 1, 8, 0, 0), new DateTime(2026, 1, 1, 16, 0, 0));
        context.Payload.Data.CurrentShift.MaxOvertimeHours = maxOvertimeHours;
        return context;
    }

    [Fact]
    public void NullMaxOvertimeHours_ReturnsOTUnchanged()
    {
        var context = BuildContext(null);
        var ot = Range(new DateTime(2026, 1, 1, 16, 0, 0), new DateTime(2026, 1, 1, 19, 0, 0)); // 3h

        var result = OvertimeCapper.Cap(ot, context);

        result.Should().Be(ot);
    }

    [Fact]
    public void ZeroMaxOvertimeHours_TreatedAsUnlimited()
    {
        var context = BuildContext(0);
        var ot = Range(new DateTime(2026, 1, 1, 16, 0, 0), new DateTime(2026, 1, 1, 19, 0, 0));

        var result = OvertimeCapper.Cap(ot, context);

        result.Should().Be(ot);
    }

    [Fact]
    public void OTWithinTheCap_ReturnsUnchanged()
    {
        var context = BuildContext(4); // 4h cap
        var ot = Range(new DateTime(2026, 1, 1, 16, 0, 0), new DateTime(2026, 1, 1, 19, 0, 0)); // 3h

        var result = OvertimeCapper.Cap(ot, context);

        result.Should().Be(ot);
    }

    [Fact]
    public void OTExceedingTheCap_KeepsOnlyTheEarliestMinutesUpToTheCap()
    {
        var context = BuildContext(2); // 2h cap
        var otStart = new DateTime(2026, 1, 1, 16, 0, 0);
        var ot = Range(otStart, new DateTime(2026, 1, 1, 19, 0, 0)); // 3h

        var result = OvertimeCapper.Cap(ot, context);

        result.TotalMinutes.Should().Be(120);
        result.TimeRecords.Single().StartTime.Should().Be(otStart);
        result.TimeRecords.Single().EndTime.Should().Be(otStart.AddHours(2));
    }

    [Fact]
    public void EmptyOT_ReturnsEmpty()
    {
        var context = BuildContext(2);

        var result = OvertimeCapper.Cap(TimeRange.Empty, context);

        result.IsEmpty().Should().BeTrue();
    }
}
