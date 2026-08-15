using Hrms.Domain.Entities;

namespace DTR.Core;

/// <summary>
/// Shared helper — maps TimeShiftType to a full-day virtual attendance pair.
/// Used by both SingleDay (FullDay fraction) and MultiDay strategies.
/// </summary>
internal static class ShiftAttendanceHelper
{
    public static List<Attendance> FullDayPair(EmployeeDTRRun employee, CurrentShift shift, bool isLeave = false) =>
        shift.ShiftType switch
        {
            TimeShiftType.FIXED => VirtualAttendanceFactory.CreatePair(employee, shift.StartTime, shift.EndTime, isLeave),
            TimeShiftType.SPLIT => VirtualAttendanceFactory.CreatePair(employee, shift.StartTime, shift.StartTime.AddMinutes(shift.MaxWorkingMinutes), isLeave),
            _ => []
        };
}
