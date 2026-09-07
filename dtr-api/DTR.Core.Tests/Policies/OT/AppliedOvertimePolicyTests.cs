using DTR.Core.Tests.TestSupport;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;

namespace DTR.Core.Tests.Policies.OT;

/// <summary>
/// AppliedOvertimePolicy — resolves a manually-filed OverTimeApplication against either the
/// already-computed system (auto) OT (GetIntersectedOT, when AutoComputeOvertimePolicy has
/// already cached a non-empty value under its own ledger key) or the raw canonical range
/// (CalcAppliedOT, the fallback when no system OT exists yet). Each of manual-entry vs.
/// explicit-StartTime/EndTime entries takes a different code path in both branches. Driven
/// through the public ConditionalPolicyBase.Apply(input, context) with an always-true spec, so
/// only ApplyIfSatisfied's own branching is under test here (IsAppliedOTSpec is covered
/// separately in OTSpecsTests).
/// </summary>
public class AppliedOvertimePolicyTests : DtrTestBase
{
    private static readonly DateTime ShiftStart = new(2026, 1, 1, 8, 0, 0);
    private static readonly DateTime ShiftEnd = new(2026, 1, 1, 16, 0, 0);

    private class AlwaysTrueSpec : IRuleSpecification
    {
        public bool IsSatisfiedBy(TimeRange input, TimeContext context) => true;
    }

    private static TimeRange SingleRange(DateTime start, DateTime end) => Range(start, end);

    private static OverTimeApplication ManualOT(double manualMinutes, double threshold = 60) => new()
    {
        Employee = new Employee(),
        IsManualEntry = true,
        ManualOTMinutes = manualMinutes,
        OverTimeThreshold = threshold,
    };

    private static OverTimeApplication ExplicitOT(DateTime start, DateTime end, double threshold = 60) => new()
    {
        Employee = new Employee(),
        IsManualEntry = false,
        StartTime = start,
        EndTime = end,
        OverTimeThreshold = threshold,
    };

    [Fact]
    public void NoSystemOT_ExplicitTimesOverlappingUsableRange_ReturnsTheIntersection()
    {
        var context = CreateContext(ShiftStart, ShiftEnd, maxWorkingMinutes: 480);
        context.CanonicalTimeRange = SingleRange(new DateTime(2026, 1, 1, 15, 0, 0), new DateTime(2026, 1, 1, 19, 0, 0));
        ApplyOvertime(context, ExplicitOT(new DateTime(2026, 1, 1, 16, 0, 0), new DateTime(2026, 1, 1, 18, 0, 0)));

        var result = new AppliedOvertimePolicy(new AlwaysTrueSpec()).Apply(TimeRange.Empty, context);

        result.TotalMinutes.Should().Be(120);
    }

    [Fact]
    public void NoSystemOT_ManualEntry_CropsFromOTStartTimeWithinUsableRange()
    {
        var context = CreateContext(ShiftStart, ShiftEnd, maxWorkingMinutes: 480);
        // OT start = shift.StartTime + MaxWorkingMinutes(480) + OTTimeCaptureAllowanceMinutes(-30) = 15:30.
        context.CanonicalTimeRange = SingleRange(new DateTime(2026, 1, 1, 16, 0, 0), new DateTime(2026, 1, 1, 18, 30, 0));
        ApplyOvertime(context, ManualOT(manualMinutes: 90));

        var result = new AppliedOvertimePolicy(new AlwaysTrueSpec()).Apply(TimeRange.Empty, context);

        result.TotalMinutes.Should().Be(90);
    }

    [Fact]
    public void NoOTApplicationFiled_ReturnsEmpty()
    {
        var context = CreateContext(ShiftStart, ShiftEnd, maxWorkingMinutes: 480);

        var result = new AppliedOvertimePolicy(new AlwaysTrueSpec()).Apply(TimeRange.Empty, context);

        result.IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void ExplicitOverlapBelowThreshold_ReturnsEmpty()
    {
        var context = CreateContext(ShiftStart, ShiftEnd, maxWorkingMinutes: 480);
        context.CanonicalTimeRange = SingleRange(new DateTime(2026, 1, 1, 15, 0, 0), new DateTime(2026, 1, 1, 19, 0, 0));
        ApplyOvertime(context, ExplicitOT(new DateTime(2026, 1, 1, 17, 45, 0), new DateTime(2026, 1, 1, 18, 0, 0), threshold: 60)); // only 15 min overlap

        var result = new AppliedOvertimePolicy(new AlwaysTrueSpec()).Apply(TimeRange.Empty, context);

        result.IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void SystemOTPresent_ExplicitEntry_IntersectsAgainstSystemOTRegardlessOfThreshold()
    {
        var context = CreateContext(ShiftStart, ShiftEnd, maxWorkingMinutes: 480);
        var systemOT = SingleRange(new DateTime(2026, 1, 1, 16, 0, 0), new DateTime(2026, 1, 1, 19, 0, 0));
        context.Payload.Ledger.Record(TimeRangeLedger.CreateKey<AutoComputeOvertimePolicy>(context), systemOT);
        ApplyOvertime(context, ExplicitOT(new DateTime(2026, 1, 1, 17, 0, 0), new DateTime(2026, 1, 1, 18, 0, 0)));

        var result = new AppliedOvertimePolicy(new AlwaysTrueSpec()).Apply(TimeRange.Empty, context);

        result.TotalMinutes.Should().Be(60);
    }

    [Fact]
    public void SystemOTPresent_ManualEntry_CropsFromTheStartOfSystemOT()
    {
        var context = CreateContext(ShiftStart, ShiftEnd, maxWorkingMinutes: 480);
        var systemOT = SingleRange(new DateTime(2026, 1, 1, 16, 0, 0), new DateTime(2026, 1, 1, 19, 0, 0)); // 180 min
        context.Payload.Ledger.Record(TimeRangeLedger.CreateKey<AutoComputeOvertimePolicy>(context), systemOT);
        ApplyOvertime(context, ManualOT(manualMinutes: 45, threshold: 30));

        var result = new AppliedOvertimePolicy(new AlwaysTrueSpec()).Apply(TimeRange.Empty, context);

        result.TotalMinutes.Should().Be(45);
        result.TimeRecords.Single().StartTime.Should().Be(new DateTime(2026, 1, 1, 16, 0, 0));
    }

    [Fact]
    public void SecondApplyCall_ReturnsCachedResultWithoutRecomputing()
    {
        var context = CreateContext(ShiftStart, ShiftEnd, maxWorkingMinutes: 480);
        context.CanonicalTimeRange = SingleRange(new DateTime(2026, 1, 1, 15, 0, 0), new DateTime(2026, 1, 1, 19, 0, 0));
        ApplyOvertime(context, ExplicitOT(new DateTime(2026, 1, 1, 16, 0, 0), new DateTime(2026, 1, 1, 18, 0, 0)));
        var policy = new AppliedOvertimePolicy(new AlwaysTrueSpec());
        var first = policy.Apply(TimeRange.Empty, context);

        // A fresh recompute against this narrower window would intersect to only 15 minutes —
        // getting the original 120 back proves the ledger cache short-circuited the recompute.
        context.CanonicalTimeRange = SingleRange(new DateTime(2026, 1, 1, 17, 45, 0), new DateTime(2026, 1, 1, 18, 0, 0));
        var second = policy.Apply(TimeRange.Empty, context);

        second.TotalMinutes.Should().Be(first.TotalMinutes).And.Be(120);
    }
}
