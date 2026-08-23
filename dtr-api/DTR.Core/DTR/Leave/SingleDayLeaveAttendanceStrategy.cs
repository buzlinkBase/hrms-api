using Hrms.Domain.Entities;

namespace DTR.Core;

/// <summary>
/// Handles SingleDay leaves. Respects DayFraction:
///   FullDay → full shift virtual pair
///   AM      → start to midpoint
///   PM      → midpoint to end
/// Midpoint is derived from MaxWorkingMinutes so it works for both FIXED and SPLIT shifts.
/// </summary>
public sealed class SingleDayLeaveAttendanceStrategy : ILeaveAttendanceStrategy
{
    public List<Attendance> CreateVirtualAttendance(
        LeaveApplication leave,
        EmployeeDTRRun employee,
        CurrentShift? shift)
    {
        if (shift == null) return [];

        var allowHalfDay = leave.Leave?.AllowHalfDay ?? true;

        return leave.DayFraction switch
        {
            DayFraction.FullDay                          => ShiftAttendanceHelper.FullDayPair(employee, shift, isLeave: true),
            DayFraction.AM when allowHalfDay             => AMHalfPair(employee, shift),
            DayFraction.PM when allowHalfDay             => PMHalfPair(employee, shift),
            DayFraction.AM or DayFraction.PM             => ShiftAttendanceHelper.FullDayPair(employee, shift, isLeave: true),
            _                                            => ShiftAttendanceHelper.FullDayPair(employee, shift, isLeave: true)
        };
    }

    private static List<Attendance> AMHalfPair(EmployeeDTRRun employee, CurrentShift shift)
    {
        var halfEnd = shift.StartTime.AddMinutes(shift.MaxWorkingMinutes / 2);
        return VirtualAttendanceFactory.CreatePair(employee, shift.StartTime, halfEnd, isLeave: true);
    }

    private static List<Attendance> PMHalfPair(EmployeeDTRRun employee, CurrentShift shift)
    {
        var halfStart = shift.EndTime.AddMinutes(-(shift.MaxWorkingMinutes / 2));
        return VirtualAttendanceFactory.CreatePair(employee, halfStart, shift.EndTime, isLeave: true);
    }
}
