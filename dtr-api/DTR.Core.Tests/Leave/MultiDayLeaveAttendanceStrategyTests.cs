using DTR.Core.Tests.TestSupport;
using Hrms.Domain.Entities;

namespace DTR.Core.Tests.LeaveSubsystem;

/// <summary>
/// MultiDayLeaveAttendanceStrategy — every day in a multi-day leave is treated as a full
/// working day, unconditionally, regardless of DayFraction.
/// </summary>
public class MultiDayLeaveAttendanceStrategyTests : DtrTestBase
{
    private static readonly DateTime ShiftStart = new(2026, 1, 5, 8, 0, 0);
    private static readonly DateTime ShiftEnd = new(2026, 1, 5, 16, 0, 0);

    [Fact]
    public void FixedShift_ReturnsFullShiftPair_RegardlessOfDayFraction()
    {
        var employee = new EmployeeDTRRun { Id = NewEmployeeId() };
        var shift = new CurrentShift { StartTime = ShiftStart, EndTime = ShiftEnd, ShiftType = TimeShiftType.FIXED, MaxWorkingMinutes = 480 };
        var leave = BuildLeaveApplication(DurationType.MultiDay, dayFraction: DayFraction.AM); // fraction is irrelevant here

        var result = new MultiDayLeaveAttendanceStrategy().CreateVirtualAttendance(leave, employee, shift);

        result.Should().HaveCount(2);
        result[0].WorkDateTime.Should().Be(ShiftStart);
        result[1].WorkDateTime.Should().Be(ShiftEnd);
        result.Should().OnlyContain(a => a.IsLeave);
    }

    [Fact]
    public void SplitShift_UsesStartTimePlusMaxWorkingMinutes()
    {
        var employee = new EmployeeDTRRun { Id = NewEmployeeId() };
        var shift = new CurrentShift { StartTime = ShiftStart, EndTime = ShiftEnd, ShiftType = TimeShiftType.SPLIT, MaxWorkingMinutes = 300 };
        var leave = BuildLeaveApplication(DurationType.MultiDay);

        var result = new MultiDayLeaveAttendanceStrategy().CreateVirtualAttendance(leave, employee, shift);

        result[1].WorkDateTime.Should().Be(ShiftStart.AddMinutes(300));
    }

    [Fact]
    public void NullShift_ReturnsEmpty()
    {
        var employee = new EmployeeDTRRun { Id = NewEmployeeId() };
        var leave = BuildLeaveApplication(DurationType.MultiDay);

        new MultiDayLeaveAttendanceStrategy().CreateVirtualAttendance(leave, employee, null).Should().BeEmpty();
    }
}
