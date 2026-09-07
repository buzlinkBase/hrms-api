using DTR.Core.Tests.TestSupport;
using Hrms.Domain.Entities;

namespace DTR.Core.Tests.LeaveSubsystem;

/// <summary>
/// TimeRangeLeaveTimeRangeStrategy — classifies how many minutes of an already-injected virtual
/// leave attendance pair count as leave, by intersecting it against the "work_time" ledger tag
/// (capped to the shift, and excluding any already-claimed "leave*"-tagged ranges from other
/// applications on the same day). It re-derives the virtual pair itself via
/// VirtualTimeComposer.SetLeaveAttendance(..., WithPayOnly: true — the default) rather than
/// reading a pre-computed one, so an application that fails IsEligibleForVirtualAttendance
/// (WithoutPay, OneTime payout, or a non-Company/Shared PaySource) always computes to Empty
/// through this path, regardless of its StartTime/EndTime.
/// </summary>
public class TimeRangeLeaveTimeRangeStrategyTests : DtrTestBase
{
    private static readonly DateTime ShiftStart = new(2026, 1, 5, 8, 0, 0);
    private static readonly DateTime ShiftEnd = new(2026, 1, 5, 17, 0, 0);

    private static TimeContext BuildContext(TimeRange? workTime = null)
    {
        var context = CreateContext(ShiftStart, ShiftEnd, maxWorkingMinutes: 540);
        context.Payload.Ledger.RecordByTag("work_time", context, workTime ?? Range(ShiftStart, ShiftEnd));
        return context;
    }

    private static LeaveApplication PartialLeave(DateTime start, DateTime end, PayType payType = PayType.WithPay, PaySource paySource = PaySource.Company, PayoutMode payoutMode = PayoutMode.PerDay) =>
        BuildLeaveApplication(DurationType.Partial, payType: payType, paySource: paySource, payoutMode: payoutMode, startTime: start, endTime: end);

    [Fact]
    public void LeaveWindowFullyWithinWorkHours_ReturnsTheFullLeaveWindow()
    {
        var context = BuildContext();
        var leave = PartialLeave(new DateTime(2026, 1, 5, 10, 0, 0), new DateTime(2026, 1, 5, 12, 0, 0));

        var result = new TimeRangeLeaveTimeRangeStrategy().ComputeTimeRange(leave, context);

        result.TotalMinutes.Should().Be(120);
    }

    [Fact]
    public void LeaveWindowPartiallyOutsideWorkHours_ReturnsOnlyTheOverlap()
    {
        var context = BuildContext(workTime: Range(ShiftStart, new DateTime(2026, 1, 5, 11, 0, 0)));
        var leave = PartialLeave(new DateTime(2026, 1, 5, 10, 0, 0), new DateTime(2026, 1, 5, 12, 0, 0));

        var result = new TimeRangeLeaveTimeRangeStrategy().ComputeTimeRange(leave, context);

        result.TotalMinutes.Should().Be(60); // 10:00-11:00 only
    }

    [Fact]
    public void AlreadyClaimedByAnotherLeaveApplication_ExclusionIsANoOp_LatentBug()
    {
        // Intent (per the class's own doc comment) is to exclude minutes another leave
        // application already claimed via a "leave<id>"-prefixed ledger tag. But
        // TimeRangeLedger.GetByStartWithTag matches `kvp.Key.ToString().StartsWith(tagName)` —
        // TimeRangeLedgerCacheKey has no custom ToString, so the compiler-generated one always
        // starts with "TimeRangeLedgerCacheKey { ...", never with "leave". The prefix check can
        // therefore never match anything, so claimedLeaved is always empty and this exclusion
        // never actually excludes anything in production today. Documented as current behavior,
        // not fixed here.
        var context = BuildContext();
        var leave = PartialLeave(new DateTime(2026, 1, 5, 10, 0, 0), new DateTime(2026, 1, 5, 12, 0, 0));
        var alreadyClaimed = Range(new DateTime(2026, 1, 5, 10, 0, 0), new DateTime(2026, 1, 5, 11, 0, 0));
        context.Payload.Ledger.RecordByTag("leave" + Guid.NewGuid(), context, alreadyClaimed);

        var result = new TimeRangeLeaveTimeRangeStrategy().ComputeTimeRange(leave, context);

        result.TotalMinutes.Should().Be(120); // the full window survives -- nothing was excluded
    }

    [Fact]
    public void WithoutPayApplication_ReturnsEmpty_IneligibleForVirtualAttendance()
    {
        var context = BuildContext();
        var leave = PartialLeave(new DateTime(2026, 1, 5, 10, 0, 0), new DateTime(2026, 1, 5, 12, 0, 0), payType: PayType.WithoutPay);

        new TimeRangeLeaveTimeRangeStrategy().ComputeTimeRange(leave, context).IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void OneTimePayoutApplication_ReturnsEmpty_IneligibleForVirtualAttendance()
    {
        var context = BuildContext();
        var leave = PartialLeave(new DateTime(2026, 1, 5, 10, 0, 0), new DateTime(2026, 1, 5, 12, 0, 0), payoutMode: PayoutMode.OneTime);

        new TimeRangeLeaveTimeRangeStrategy().ComputeTimeRange(leave, context).IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void GovernmentFundedApplication_ReturnsEmpty_IneligibleForVirtualAttendance()
    {
        var context = BuildContext();
        var leave = PartialLeave(new DateTime(2026, 1, 5, 10, 0, 0), new DateTime(2026, 1, 5, 12, 0, 0), paySource: PaySource.Government);

        new TimeRangeLeaveTimeRangeStrategy().ComputeTimeRange(leave, context).IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void NoWorkTimeRecordedYet_ReturnsEmpty()
    {
        var context = CreateContext(ShiftStart, ShiftEnd, maxWorkingMinutes: 540); // "work_time" never recorded
        var leave = PartialLeave(new DateTime(2026, 1, 5, 10, 0, 0), new DateTime(2026, 1, 5, 12, 0, 0));

        new TimeRangeLeaveTimeRangeStrategy().ComputeTimeRange(leave, context).IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void CurrentAttendanceIsNull_ReturnsEmpty()
    {
        var context = BuildContext();
        context.Payload.Data.CurrentAttendance = null!;
        var leave = PartialLeave(new DateTime(2026, 1, 5, 10, 0, 0), new DateTime(2026, 1, 5, 12, 0, 0));

        new TimeRangeLeaveTimeRangeStrategy().ComputeTimeRange(leave, context).IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void LeaveWindowStartingBeforeTheShift_IsClippedToTheShiftBoundary()
    {
        // CapAndCrop(shift)'s maxMinutes is itself the shift's own span, so the boundary
        // intersection (not a separate "max working minutes" cap) is what does the clipping here.
        var context = BuildContext();
        var leave = PartialLeave(ShiftStart.AddHours(-2), ShiftStart.AddHours(2)); // starts 2h before shift open

        var result = new TimeRangeLeaveTimeRangeStrategy().ComputeTimeRange(leave, context);

        result.TotalMinutes.Should().Be(120); // only the portion from ShiftStart onward
        result.TimeRecords.Single().StartTime.Should().Be(ShiftStart);
    }
}

/// <summary>
/// ManualEntryLeaveTimeRangeStrategy — currently unreachable via LeaveTimeRangeStrategyFactory
/// (its dispatch is commented out, see LeaveTimeRangeStrategyFactoryTests), but tested here in
/// isolation regardless. Previously always returned TimeRange.Empty: it built
/// `new TimeRange(minutes)` — the single-arg constructor pairs the total with an EMPTY
/// TimeRecordCollection — then called `.TimeRecords.ToTimeRange()` on that, which re-derives
/// TotalMinutes from the (empty) TimeRecords instead of keeping the minutes value. Fixed to
/// anchor the leave block at shift.StartTime spanning the declared (capped) duration, matching
/// PartialLeaveAttendanceStrategy's own manual-entry virtual-attendance convention.
/// </summary>
public class ManualEntryLeaveTimeRangeStrategyTests : DtrTestBase
{
    private static readonly DateTime ShiftStart = new(2026, 1, 5, 8, 0, 0);
    private static readonly DateTime ShiftEnd = new(2026, 1, 5, 17, 0, 0);

    [Fact]
    public void DeclaredMinutesWithinShiftCap_ReturnsABlockAnchoredAtShiftStart()
    {
        var context = CreateContext(ShiftStart, ShiftEnd, maxWorkingMinutes: 540);
        var leave = BuildLeaveApplication(DurationType.Partial, isManualEntry: true, totalMinutes: 120);

        var result = new ManualEntryLeaveTimeRangeStrategy().ComputeTimeRange(leave, context);

        result.TotalMinutes.Should().Be(120);
        result.TimeRecords.Single().StartTime.Should().Be(ShiftStart);
        result.TimeRecords.Single().EndTime.Should().Be(ShiftStart.AddMinutes(120));
    }

    [Fact]
    public void DeclaredMinutesExceedingTheShiftCap_IsCappedToMaxWorkingMinutes()
    {
        var context = CreateContext(ShiftStart, ShiftEnd, maxWorkingMinutes: 480);
        var leave = BuildLeaveApplication(DurationType.Partial, isManualEntry: true, totalMinutes: 600); // more than the shift allows

        var result = new ManualEntryLeaveTimeRangeStrategy().ComputeTimeRange(leave, context);

        result.TotalMinutes.Should().Be(480);
        result.TimeRecords.Single().EndTime.Should().Be(ShiftStart.AddMinutes(480));
    }

    [Fact]
    public void ZeroDeclaredMinutes_ReturnsEmpty()
    {
        var context = CreateContext(ShiftStart, ShiftEnd, maxWorkingMinutes: 480);
        var leave = BuildLeaveApplication(DurationType.Partial, isManualEntry: true, totalMinutes: 0);

        new ManualEntryLeaveTimeRangeStrategy().ComputeTimeRange(leave, context).IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void NoCurrentShift_ReturnsEmpty()
    {
        var context = CreateContext(ShiftStart, ShiftEnd, maxWorkingMinutes: 480);
        context.Payload.Data.CurrentShift = null!;
        var leave = BuildLeaveApplication(DurationType.Partial, isManualEntry: true, totalMinutes: 120);

        new ManualEntryLeaveTimeRangeStrategy().ComputeTimeRange(leave, context).IsEmpty().Should().BeTrue();
    }
}
