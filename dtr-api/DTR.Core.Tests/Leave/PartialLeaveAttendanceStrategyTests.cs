using DTR.Core.Tests.TestSupport;
using Hrms.Domain.Entities;

namespace DTR.Core.Tests.LeaveSubsystem;

/// <summary>
/// PartialLeaveAttendanceStrategy — IsManualEntry discriminates between an hours-only
/// declaration (virtual pair from shift start, spanning TotalMinutes) and an explicit
/// StartTime/EndTime window (virtual pair at exactly those times).
/// </summary>
public class PartialLeaveAttendanceStrategyTests : DtrTestBase
{
    private static readonly DateTime ShiftStart = new(2026, 1, 5, 8, 0, 0);
    private static readonly DateTime ShiftEnd = new(2026, 1, 5, 16, 0, 0);

    private static CurrentShift Shift() => new() { StartTime = ShiftStart, EndTime = ShiftEnd, ShiftType = TimeShiftType.FIXED, MaxWorkingMinutes = 480 };
    private static EmployeeDTRRun Employee() => new() { Id = NewEmployeeId() };

    [Fact]
    public void ManualEntry_ReturnsPairFromShiftStartSpanningTotalMinutes()
    {
        var leave = BuildLeaveApplication(DurationType.Partial, isManualEntry: true, totalMinutes: 90);

        var result = new PartialLeaveAttendanceStrategy().CreateVirtualAttendance(leave, Employee(), Shift());

        result.Should().HaveCount(2);
        result[0].WorkDateTime.Should().Be(ShiftStart);
        result[1].WorkDateTime.Should().Be(ShiftStart.AddMinutes(90));
        result.Should().OnlyContain(a => a.IsLeave);
    }

    [Fact]
    public void ExplicitTimeRange_NotManualEntry_ReturnsPairAtThoseExactTimes()
    {
        var start = new DateTime(2026, 1, 5, 10, 0, 0);
        var end = new DateTime(2026, 1, 5, 12, 30, 0);
        var leave = BuildLeaveApplication(DurationType.Partial, isManualEntry: false, startTime: start, endTime: end);

        var result = new PartialLeaveAttendanceStrategy().CreateVirtualAttendance(leave, Employee(), Shift());

        result[0].WorkDateTime.Should().Be(start);
        result[1].WorkDateTime.Should().Be(end);
    }

    [Fact]
    public void NotManualEntry_MissingStartOrEndTime_ReturnsEmpty()
    {
        var leave = BuildLeaveApplication(DurationType.Partial, isManualEntry: false, startTime: new DateTime(2026, 1, 5, 10, 0, 0), endTime: null);

        new PartialLeaveAttendanceStrategy().CreateVirtualAttendance(leave, Employee(), Shift()).Should().BeEmpty();
    }

    [Fact]
    public void NullShift_ReturnsEmpty()
    {
        var leave = BuildLeaveApplication(DurationType.Partial, isManualEntry: true, totalMinutes: 60);

        new PartialLeaveAttendanceStrategy().CreateVirtualAttendance(leave, Employee(), null).Should().BeEmpty();
    }
}
