using DTR.Core.Tests.TestSupport;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;

namespace DTR.Core.Tests.Pipelines.OT;

/// <summary>
/// OverTimeHandlerProcessor — the real top-level OT entry point (mirrors
/// CleanDTRDetailProcessor's usage). Always runs AutoComputedOTHandler first (its result is
/// cached under AutoComputeOvertimePolicy's ledger key as a side effect, needed by
/// AppliedOvertimePolicy's "intersect against system OT" branch), then tries
/// ApprovalBasedOTHandler (CanHandle = IsAppliedOTSpec, i.e. an OT application filed for the
/// date) chained back to the same system handler as its fallback, and finally records the
/// result under the "OT" ledger tag.
/// </summary>
public class OverTimeHandlerProcessorTests : DtrTestBase
{
    private static readonly DateTime ShiftStart = new(2026, 1, 1, 8, 0, 0);
    private static readonly DateTime ShiftEnd = new(2026, 1, 1, 16, 0, 0);
    private static readonly DateTime PreStart = new(2026, 1, 1, 6, 0, 0); // 2h pre-shift work

    private static TimeRange SingleRange(DateTime start, DateTime end) => Range(start, end);

    private static TimeContext BuildContext(double? maxOvertimeHours = null)
    {
        var context = CreateContext(ShiftStart, ShiftEnd, maxWorkingMinutes: 480);
        context.Payload.Data.CurrentShift.MaxOvertimeHours = maxOvertimeHours;
        // Only the pre-shift block — PreShiftOTHandler's crop condition requires a record whose
        // EndTime is at-or-before the scheduled shift start, so it must not be merged (via
        // MergeOverlapping) into a touching/overlapping record that runs past shift start.
        context.CanonicalTimeRange = SingleRange(PreStart, ShiftStart); // 2h pre-shift only
        return context;
    }

    [Fact]
    public void NoOTApplicationFiled_FallsBackToSystemAutoComputedOT()
    {
        var context = BuildContext();

        var result = new OverTimeHandlerProcessor(context).Handle(context.CanonicalTimeRange);

        result.TotalMinutes.Should().Be(120); // the 2h pre-shift block, via PreShiftOTHandler
        context.Payload.Ledger.GetByTag("OT", context).TotalMinutes.Should().Be(120);
    }

    [Fact]
    public void OTApplicationFiled_UsesApplicationIntersectedAgainstSystemOT()
    {
        var context = BuildContext();
        ApplyOvertime(context, new OverTimeApplication
        {
            Employee = new Employee(),
            IsManualEntry = false,
            StartTime = new DateTime(2026, 1, 1, 6, 30, 0),
            EndTime = new DateTime(2026, 1, 1, 7, 30, 0), // 1h, fully inside the 2h system OT window
            OverTimeThreshold = 30,
        });

        var result = new OverTimeHandlerProcessor(context).Handle(context.CanonicalTimeRange);

        result.TotalMinutes.Should().Be(60);
        context.Payload.Ledger.GetByTag("OT", context).TotalMinutes.Should().Be(60);
    }

    [Fact]
    public void ResultIsCappedByShiftMaxOvertimeHours()
    {
        var context = BuildContext(maxOvertimeHours: 1); // cap to 60 min, less than the 120-min system OT

        var result = new OverTimeHandlerProcessor(context).Handle(context.CanonicalTimeRange);

        result.TotalMinutes.Should().Be(60);
        // Default policy is UseEarlyClockIn: the kept hour is the one nearest the shift start.
        result.TimeRecords.Single().StartTime.Should().Be(ShiftStart.AddHours(-1));
        result.TimeRecords.Single().EndTime.Should().Be(ShiftStart);
    }

    [Fact]
    public void ComputeUncapped_ReturnsAcceptableOTWithoutTheCapAndWithoutLedgerOTTag()
    {
        var context = BuildContext(maxOvertimeHours: 1);

        var result = new OverTimeHandlerProcessor(context).ComputeUncapped(context.CanonicalTimeRange);

        result.TotalMinutes.Should().Be(120);
        context.Payload.Ledger.GetByTag("OT", context).IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void ApprovedOTWithEarlyTimeIn_PostShiftPolicy_ExcludesPreShiftMinutesBeforeCapping()
    {
        // Approval-only OT (no system OT): the approved 06:00-19:00 block also covers the early
        // time-in, which isn't OT under UsePostShiftWork and must not use up the 2h cap.
        var context = CreateContext(ShiftStart, ShiftEnd, maxWorkingMinutes: 480);
        context.Payload.Data.CurrentShift.WithOT = false;
        context.Payload.Data.CurrentShift.OTRequireTimeIn = false;
        context.Payload.Data.CurrentShift.MaxOvertimeHours = 2;
        context.Payload.Data.CompanyPolicy.OTInclusionPolicy = OvertimeInclusionPolicy.UsePostShiftWork;
        var trc = new TimeRecordCollection
        {
            new TimeRecord(PreStart, ShiftStart), // 2h early time-in
            new TimeRecord(ShiftEnd, ShiftEnd.AddHours(3)), // 3h post-shift
        };
        context.CanonicalTimeRange = TimeRange.Set(trc);
        ApplyOvertime(context, new OverTimeApplication
        {
            Employee = new Employee(),
            IsManualEntry = false,
            StartTime = PreStart,
            EndTime = ShiftEnd.AddHours(3),
            OverTimeThreshold = 30,
        });

        var result = new OverTimeHandlerProcessor(context).Handle(context.CanonicalTimeRange);

        result.TotalMinutes.Should().Be(120);
        result.TimeRecords.Should().OnlyContain(r => r.StartTime >= ShiftEnd);
        result.TimeRecords.Min(r => r.StartTime).Should().Be(ShiftEnd);
        result.TimeRecords.Max(r => r.EndTime).Should().Be(ShiftEnd.AddHours(2));
    }

    [Fact]
    public void NoOTAtAll_ReturnsEmptyAndRecordsEmptyOnTheLedger()
    {
        var context = CreateContext(ShiftStart, ShiftEnd, maxWorkingMinutes: 480);
        context.CanonicalTimeRange = SingleRange(ShiftStart, ShiftEnd); // no pre/post-shift work at all

        var result = new OverTimeHandlerProcessor(context).Handle(context.CanonicalTimeRange);

        result.IsEmpty().Should().BeTrue();
        context.Payload.Ledger.GetByTag("OT", context).IsEmpty().Should().BeTrue();
    }
}
