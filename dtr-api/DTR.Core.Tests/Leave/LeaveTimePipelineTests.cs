using DTR.Core.Tests.TestSupport;

namespace DTR.Core.Tests.LeaveSubsystem;

/// <summary>
/// LeaveTimePipeline — a thin ledger-cached wrapper around LeavePolicy(IsLeaved()). IsLeaved
/// itself just checks CurrentLeaves != null (always true — DtrTestBase never leaves it null),
/// so this pipeline effectively always delegates to LeavePolicy on a cache miss.
/// </summary>
public class LeaveTimePipelineTests : DtrTestBase
{
    private static readonly DateTime ShiftStart = new(2026, 1, 5, 8, 0, 0);
    private static readonly DateTime ShiftEnd = new(2026, 1, 5, 17, 0, 0);

    private static TimeContext BuildContext()
    {
        var context = CreateContext(ShiftStart, ShiftEnd, maxWorkingMinutes: 540);
        context.Payload.Ledger.RecordByTag("work_time", context, Range(ShiftStart, ShiftEnd));
        return context;
    }

    [Fact]
    public void NoLeaveApplications_ReturnsEmptyAndRecordsOnLeaveTag()
    {
        var context = BuildContext();

        var result = new LeaveTimePipeline().Apply(context, TimeRange.Empty);

        result.IsEmpty().Should().BeTrue();
        context.Payload.Ledger.GetByTag("onleave", context).IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void EligiblePaidApplication_ReturnsItsRangeAndRecordsOnLeaveTag()
    {
        var context = BuildContext();
        var leave = BuildLeaveApplication(DurationType.Partial,
            startTime: new DateTime(2026, 1, 5, 10, 0, 0), endTime: new DateTime(2026, 1, 5, 12, 0, 0));
        ApplyLeave(context, leave);

        var result = new LeaveTimePipeline().Apply(context, TimeRange.Empty);

        result.TotalMinutes.Should().Be(120);
        context.Payload.Ledger.GetByTag("onleave", context).TotalMinutes.Should().Be(120);
    }

    [Fact]
    public void SecondCall_ReturnsCachedResultWithoutRecomputing()
    {
        var context = BuildContext();
        var leave = BuildLeaveApplication(DurationType.Partial,
            startTime: new DateTime(2026, 1, 5, 10, 0, 0), endTime: new DateTime(2026, 1, 5, 12, 0, 0));
        ApplyLeave(context, leave);
        var pipeline = new LeaveTimePipeline();
        var first = pipeline.Apply(context, TimeRange.Empty);

        // A genuine recompute from here would see no applications at all.
        context.Payload.Data.CurrentLeaves = new List<Hrms.Domain.Entities.LeaveApplication>();
        var second = pipeline.Apply(context, TimeRange.Empty);

        second.TotalMinutes.Should().Be(first.TotalMinutes).And.Be(120);
    }
}
