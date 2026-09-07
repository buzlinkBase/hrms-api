using DTR.Core.Tests.TestSupport;
using Hrms.Domain.Entities;

namespace DTR.Core.Tests.LeaveSubsystem;

/// <summary>
/// SingleDayLeaveAttendanceStrategy — injects a virtual attendance pair respecting DayFraction:
/// FullDay spans the whole shift, AM/PM span half of it (derived from MaxWorkingMinutes so it
/// works for FIXED and SPLIT shifts alike), falling back to FullDay whenever the leave type
/// doesn't allow half-day (Leave.AllowHalfDay == false).
/// </summary>
public class SingleDayLeaveAttendanceStrategyTests : DtrTestBase
{
    private static readonly DateTime ShiftStart = new(2026, 1, 5, 8, 0, 0);
    private static readonly DateTime ShiftEnd = new(2026, 1, 5, 16, 0, 0); // 480-min FIXED shift

    private static EmployeeDTRRun Employee(Guid id) => new() { Id = id };

    [Fact]
    public void FullDay_FixedShift_ReturnsFullShiftPair()
    {
        var employee = Employee(NewEmployeeId());
        var shift = new CurrentShift { StartTime = ShiftStart, EndTime = ShiftEnd, ShiftType = TimeShiftType.FIXED, MaxWorkingMinutes = 480 };
        var leave = BuildLeaveApplication(DurationType.SingleDay, dayFraction: DayFraction.FullDay);

        var result = new SingleDayLeaveAttendanceStrategy().CreateVirtualAttendance(leave, employee, shift);

        result.Should().HaveCount(2);
        result[0].WorkDateTime.Should().Be(ShiftStart);
        result[1].WorkDateTime.Should().Be(ShiftEnd);
        result.Should().OnlyContain(a => a.IsLeave && a.IsVirtual);
    }

    [Fact]
    public void FullDay_SplitShift_UsesStartTimePlusMaxWorkingMinutes()
    {
        var employee = Employee(NewEmployeeId());
        var shift = new CurrentShift { StartTime = ShiftStart, EndTime = ShiftEnd, ShiftType = TimeShiftType.SPLIT, MaxWorkingMinutes = 300 };
        var leave = BuildLeaveApplication(DurationType.SingleDay, dayFraction: DayFraction.FullDay);

        var result = new SingleDayLeaveAttendanceStrategy().CreateVirtualAttendance(leave, employee, shift);

        result[0].WorkDateTime.Should().Be(ShiftStart);
        result[1].WorkDateTime.Should().Be(ShiftStart.AddMinutes(300));
    }

    [Fact]
    public void AM_HalfDayAllowed_ReturnsStartToMidpoint()
    {
        var employee = Employee(NewEmployeeId());
        var shift = new CurrentShift { StartTime = ShiftStart, EndTime = ShiftEnd, ShiftType = TimeShiftType.FIXED, MaxWorkingMinutes = 480 };
        var leave = BuildLeaveApplication(DurationType.SingleDay, dayFraction: DayFraction.AM, allowHalfDay: true);

        var result = new SingleDayLeaveAttendanceStrategy().CreateVirtualAttendance(leave, employee, shift);

        result[0].WorkDateTime.Should().Be(ShiftStart);
        result[1].WorkDateTime.Should().Be(ShiftStart.AddMinutes(240));
    }

    [Fact]
    public void PM_HalfDayAllowed_ReturnsMidpointToEnd()
    {
        var employee = Employee(NewEmployeeId());
        var shift = new CurrentShift { StartTime = ShiftStart, EndTime = ShiftEnd, ShiftType = TimeShiftType.FIXED, MaxWorkingMinutes = 480 };
        var leave = BuildLeaveApplication(DurationType.SingleDay, dayFraction: DayFraction.PM, allowHalfDay: true);

        var result = new SingleDayLeaveAttendanceStrategy().CreateVirtualAttendance(leave, employee, shift);

        result[0].WorkDateTime.Should().Be(ShiftEnd.AddMinutes(-240));
        result[1].WorkDateTime.Should().Be(ShiftEnd);
    }

    [Fact]
    public void AM_HalfDayNotAllowedByLeaveType_FallsBackToFullDay()
    {
        var employee = Employee(NewEmployeeId());
        var shift = new CurrentShift { StartTime = ShiftStart, EndTime = ShiftEnd, ShiftType = TimeShiftType.FIXED, MaxWorkingMinutes = 480 };
        var leave = BuildLeaveApplication(DurationType.SingleDay, dayFraction: DayFraction.AM, allowHalfDay: false);

        var result = new SingleDayLeaveAttendanceStrategy().CreateVirtualAttendance(leave, employee, shift);

        result[0].WorkDateTime.Should().Be(ShiftStart);
        result[1].WorkDateTime.Should().Be(ShiftEnd);
    }

    [Fact]
    public void PM_HalfDayNotAllowedByLeaveType_FallsBackToFullDay()
    {
        var employee = Employee(NewEmployeeId());
        var shift = new CurrentShift { StartTime = ShiftStart, EndTime = ShiftEnd, ShiftType = TimeShiftType.FIXED, MaxWorkingMinutes = 480 };
        var leave = BuildLeaveApplication(DurationType.SingleDay, dayFraction: DayFraction.PM, allowHalfDay: false);

        var result = new SingleDayLeaveAttendanceStrategy().CreateVirtualAttendance(leave, employee, shift);

        result[0].WorkDateTime.Should().Be(ShiftStart);
        result[1].WorkDateTime.Should().Be(ShiftEnd);
    }

    [Fact]
    public void NullLeaveNavigation_AllowHalfDayDefaultsToTrue()
    {
        // Leave?.AllowHalfDay ?? true -- a missing Leave navigation shouldn't silently force FullDay.
        var employee = Employee(NewEmployeeId());
        var shift = new CurrentShift { StartTime = ShiftStart, EndTime = ShiftEnd, ShiftType = TimeShiftType.FIXED, MaxWorkingMinutes = 480 };
        var leave = BuildLeaveApplication(DurationType.SingleDay, dayFraction: DayFraction.AM);
        leave.Leave = null!;

        var result = new SingleDayLeaveAttendanceStrategy().CreateVirtualAttendance(leave, employee, shift);

        result[1].WorkDateTime.Should().Be(ShiftStart.AddMinutes(240)); // AM half, not full day
    }

    [Fact]
    public void NullShift_ReturnsEmpty()
    {
        var employee = Employee(NewEmployeeId());
        var leave = BuildLeaveApplication(DurationType.SingleDay);

        new SingleDayLeaveAttendanceStrategy().CreateVirtualAttendance(leave, employee, null).Should().BeEmpty();
    }
}
