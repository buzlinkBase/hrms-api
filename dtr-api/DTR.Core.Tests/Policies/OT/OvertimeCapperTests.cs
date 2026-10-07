using DTR.Core.Tests.TestSupport;

namespace DTR.Core.Tests.Policies.OT;

/// <summary>
/// OvertimeCapper — first excludes non-OT time (only the pre-shift window before the scheduled
/// start and the post-shift window from the OT start onward count, filtered by the effective
/// OT Inclusion policy), then caps the remaining OT to CurrentShift.MaxOvertimeHours
/// (null/0 = unlimited) keeping the minutes the policy prioritises:
/// UseEarlyClockIn = nearest the shift start, UsePostShiftWork = earliest post-shift,
/// UseAllExcessOver8Hours = post-shift first, then pre-shift nearest the shift start.
/// Shift is 08:00-16:00 with MaxWorkingMinutes 480 and OTRequireTimeIn off, so FixedOT puts the
/// OT start at 16:00.
/// </summary>
public class OvertimeCapperTests : DtrTestBase
{
    private static DateTime At(int hour, int minute = 0) => new(2026, 1, 1, hour, minute, 0);

    private static TimeContext BuildContext(double? maxOvertimeHours, OvertimeInclusionPolicy policy = OvertimeInclusionPolicy.UsePostShiftWork)
    {
        var context = CreateContext(At(8), At(16), maxWorkingMinutes: 480);
        context.Payload.Data.CurrentShift.MaxOvertimeHours = maxOvertimeHours;
        context.Payload.Data.CurrentShift.OTRequireTimeIn = false;
        context.Payload.Data.CompanyPolicy.OTInclusionPolicy = policy;
        return context;
    }

    private static TimeRange MultiRecordRange(params (DateTime Start, DateTime End)[] spans)
    {
        var trc = new TimeRecordCollection();
        foreach (var (start, end) in spans) trc.Add(new TimeRecord(start, end));
        return TimeRange.Set(trc);
    }

    private static IEnumerable<(DateTime Start, DateTime End)> Spans(TimeRange range) =>
        range.TimeRecords.OrderBy(r => r.StartTime).Select(r => (r.StartTime, r.EndTime));

    [Fact]
    public void NullMaxOvertimeHours_ReturnsAllAcceptableOT()
    {
        var context = BuildContext(null);
        var ot = Range(At(16), At(19)); // 3h

        var result = OvertimeCapper.Cap(ot, context);

        result.TotalMinutes.Should().Be(180);
        Spans(result).Should().Equal((At(16), At(19)));
    }

    [Fact]
    public void ZeroMaxOvertimeHours_TreatedAsUnlimited()
    {
        var context = BuildContext(0);
        var ot = Range(At(16), At(19));

        var result = OvertimeCapper.Cap(ot, context);

        result.TotalMinutes.Should().Be(180);
    }

    [Fact]
    public void OTWithinTheCap_ReturnsUnchanged()
    {
        var context = BuildContext(4); // 4h cap
        var ot = Range(At(16), At(19)); // 3h

        var result = OvertimeCapper.Cap(ot, context);

        result.TotalMinutes.Should().Be(180);
        Spans(result).Should().Equal((At(16), At(19)));
    }

    [Fact]
    public void OTExceedingTheCap_KeepsOnlyTheEarliestPostShiftMinutes()
    {
        var context = BuildContext(2); // 2h cap
        var ot = Range(At(16), At(19)); // 3h

        var result = OvertimeCapper.Cap(ot, context);

        result.TotalMinutes.Should().Be(120);
        Spans(result).Should().Equal((At(16), At(18)));
    }

    [Fact]
    public void EmptyOT_ReturnsEmpty()
    {
        var context = BuildContext(2);

        var result = OvertimeCapper.Cap(TimeRange.Empty, context);

        result.IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void PostShiftWork_EarlyTimeInMinutesAreExcludedBeforeCapping()
    {
        // Early time-in 06:00-08:00 is not OT under UsePostShiftWork, so it must not use up the cap.
        var context = BuildContext(2);
        var ot = MultiRecordRange((At(6), At(8)), (At(16), At(19)));

        var result = OvertimeCapper.Cap(ot, context);

        result.TotalMinutes.Should().Be(120);
        Spans(result).Should().Equal((At(16), At(18)));
    }

    [Fact]
    public void PostShiftWork_NoCap_StillExcludesPreShiftMinutes()
    {
        var context = BuildContext(null);
        var ot = MultiRecordRange((At(6), At(8)), (At(16), At(19)));

        var result = OvertimeCapper.Cap(ot, context);

        result.TotalMinutes.Should().Be(180);
        Spans(result).Should().Equal((At(16), At(19)));
    }

    [Fact]
    public void PostShiftWork_MinutesBeforeTheConfiguredOTStartAreExcluded()
    {
        var context = BuildContext(null);
        context.Payload.Data.CurrentShift.OTRequireTimeIn = true;
        context.Payload.Data.CurrentShift.OTStartTime = new TimeSpan(17, 0, 0); // OT counts from 17:00
        var ot = Range(At(16), At(19));

        var result = OvertimeCapper.Cap(ot, context);

        result.TotalMinutes.Should().Be(120);
        Spans(result).Should().Equal((At(17), At(19)));
    }

    [Fact]
    public void EarlyClockIn_OverTheCap_KeepsTheMinutesNearestTheShiftStart()
    {
        var context = BuildContext(2, OvertimeInclusionPolicy.UseEarlyClockIn);
        var ot = Range(At(4), At(8)); // timed in 4h early

        var result = OvertimeCapper.Cap(ot, context);

        result.TotalMinutes.Should().Be(120);
        Spans(result).Should().Equal((At(6), At(8)));
    }

    [Fact]
    public void EarlyClockIn_ExcludesPostShiftMinutes()
    {
        var context = BuildContext(null, OvertimeInclusionPolicy.UseEarlyClockIn);
        var ot = MultiRecordRange((At(6), At(8)), (At(16), At(18)));

        var result = OvertimeCapper.Cap(ot, context);

        result.TotalMinutes.Should().Be(120);
        Spans(result).Should().Equal((At(6), At(8)));
    }

    [Fact]
    public void AllExcess_NoCap_KeepsBothPreAndPostShiftOT()
    {
        var context = BuildContext(null, OvertimeInclusionPolicy.UseAllExcessOver8Hours);
        var ot = MultiRecordRange((At(6), At(8)), (At(16), At(18)));

        var result = OvertimeCapper.Cap(ot, context);

        result.TotalMinutes.Should().Be(240);
    }

    [Fact]
    public void AllExcess_OverTheCap_FillsPostShiftFirstThenPreShiftNearestTheShiftStart()
    {
        var context = BuildContext(2, OvertimeInclusionPolicy.UseAllExcessOver8Hours);
        var ot = MultiRecordRange((At(5), At(8)), (At(16), At(17))); // 3h pre + 1h post

        var result = OvertimeCapper.Cap(ot, context);

        result.TotalMinutes.Should().Be(120);
        Spans(result).Should().Equal((At(7), At(8)), (At(16), At(17)));
    }

    [Fact]
    public void AllExcess_PostShiftAloneExceedsTheCap_NoPreShiftMinutesKept()
    {
        var context = BuildContext(3, OvertimeInclusionPolicy.UseAllExcessOver8Hours);
        var ot = MultiRecordRange((At(6), At(8)), (At(16), At(20))); // 2h pre + 4h post

        var result = OvertimeCapper.Cap(ot, context);

        result.TotalMinutes.Should().Be(180);
        Spans(result).Should().Equal((At(16), At(19)));
    }

    [Fact]
    public void ClientPolicyOverridesCompanyPolicy()
    {
        var context = BuildContext(null); // company: UsePostShiftWork
        var clientId = Guid.NewGuid();
        context.Payload.Data.Employee.ClientId = clientId;
        context.Payload.Provider.ClientPolicyProvider = new ClientPolicyProvider(new Dictionary<ClientPolicyKey, ClientPolicyRule>
        {
            [new ClientPolicyKey(clientId)] = new ClientPolicyRule { OvertimeInclusionPolicy = OvertimeInclusionPolicy.UseEarlyClockIn },
        });
        var ot = MultiRecordRange((At(6), At(8)), (At(16), At(18)));

        var result = OvertimeCapper.Cap(ot, context);

        Spans(result).Should().Equal((At(6), At(8)));
    }

    [Fact]
    public void ExcludeNonOvertime_FiltersByPolicyButIgnoresTheCap()
    {
        var context = BuildContext(1);
        var ot = MultiRecordRange((At(6), At(8)), (At(16), At(19)));

        var result = OvertimeCapper.ExcludeNonOvertime(ot, context);

        result.TotalMinutes.Should().Be(180);
        Spans(result).Should().Equal((At(16), At(19)));
    }
}
