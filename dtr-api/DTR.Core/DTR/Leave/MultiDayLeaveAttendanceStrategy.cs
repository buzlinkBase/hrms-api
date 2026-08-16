using Hrms.Domain.Entities;

namespace DTR.Core;

/// <summary>
/// Handles MultiDay leaves. Each day in the range is always a full working day,
/// so DayFraction is irrelevant — a full shift pair is injected unconditionally.
/// </summary>
public sealed class MultiDayLeaveAttendanceStrategy : ILeaveAttendanceStrategy
{
    public List<Attendance> CreateVirtualAttendance(
        List<Attendance> existing,
        LeaveApplication leave,
        EmployeeDTRRun employee,
        CurrentShift? shift)
    {
        if (shift == null) return [];
        return ShiftAttendanceHelper.FullDayPair(employee, shift, isLeave: true);
    }
}
