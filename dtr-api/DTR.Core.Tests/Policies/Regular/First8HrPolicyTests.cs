using DTR.Core.Tests.TestSupport;

namespace DTR.Core.Tests.Policies.Regular;

/// <summary>
/// First8HrPolicy (OffsetAgainstUndertimeOrLateness) — tops up a regular-time shortfall by
/// borrowing from OT. The borrowed minutes become regular time, so they must be taken from the
/// uncapped OT and the shift's Max OT Hours cap must only apply to what remains ("FinalOT").
/// Shift 08:00-16:00 (480 min), timed in 05:00 (early) and out 15:00 (1h undertime), so the
/// 05:00-08:00 early time-in is pre-shift OT under UseEarlyClockIn.
/// </summary>
public class First8HrPolicyTests : DtrTestBase
{
    private static DateTime At(int hour) => new(2026, 1, 1, hour, 0, 0);

    private static TimeContext BuildContext(double? maxOvertimeHours, TimeRange regular)
    {
        var context = CreateContext(At(8), At(16), maxWorkingMinutes: 480);
        context.Payload.Data.CompanyPolicy.OTEligibility = OvertimeEligibilityRule.OffsetAgainstUndertimeOrLateness;
        context.Payload.Data.CompanyPolicy.OTInclusionPolicy = OvertimeInclusionPolicy.UseEarlyClockIn;
        context.Payload.Data.CurrentShift.MaxOvertimeHours = maxOvertimeHours;
        context.CanonicalTimeRange = Range(At(5), At(15));
        context.Payload.Ledger.Record(TimeRangeLedger.CreateKey<RegularHourPolicy>(context), regular);
        return context;
    }

    private static TimeRange FinalOT(TimeContext context) =>
        context.Payload.Ledger.GetByTag("FinalOT", context);

    [Fact]
    public void Shortfall_BorrowsFromUncappedOT_ThenCapsTheRemainder()
    {
        var regular = Range(At(8), At(15)); // 420 of 480 -> 60-min shortfall
        var context = BuildContext(maxOvertimeHours: 2, regular);

        var result = new First8HrPolicy().Apply(regular, context);

        result.TotalMinutes.Should().Be(480);
        // 05:00-08:00 OT, 07:00-08:00 borrowed for regular, 05:00-07:00 left = exactly the 2h cap.
        var finalOT = FinalOT(context);
        finalOT.TotalMinutes.Should().Be(120);
        finalOT.TimeRecords.Min(r => r.StartTime).Should().Be(At(5));
        finalOT.TimeRecords.Max(r => r.EndTime).Should().Be(At(7));
    }

    [Fact]
    public void Shortfall_RemainderOverTheCap_KeepsTheMinutesNearestTheShiftStart()
    {
        var regular = Range(At(8), At(15));
        var context = BuildContext(maxOvertimeHours: 1, regular);

        new First8HrPolicy().Apply(regular, context);

        var finalOT = FinalOT(context);
        finalOT.TotalMinutes.Should().Be(60);
        finalOT.TimeRecords.Single().StartTime.Should().Be(At(6));
        finalOT.TimeRecords.Single().EndTime.Should().Be(At(7));
    }

    [Fact]
    public void RegularAlreadyFulfilled_LeavesOTToTheOTPipeline()
    {
        var regular = Range(At(8), At(16)); // full 480
        var context = BuildContext(maxOvertimeHours: 2, regular);

        var result = new First8HrPolicy().Apply(regular, context);

        result.TotalMinutes.Should().Be(480);
        context.Payload.Ledger.GetByKey(TimeRangeLedger.CreateKey("FinalOT", context)).Found.Should().BeFalse();
    }
}
