using Hrms.Domain.Entities;

namespace DTR.Core;

/// <summary>
/// Partial leaves (time-range or hours-only) carry their own StartTime/EndTime or TotalMinutes.
/// The employee is expected to clock in/out normally; no virtual attendance is injected.
/// </summary>
public sealed class PartialLeaveAttendanceStrategy : ILeaveAttendanceStrategy
{
    public List<Attendance> CreateVirtualAttendance(
        LeaveApplication leave,
        EmployeeDTRRun employee,
        CurrentShift? shift)
    {
        if (shift == null) return [];
        if (leave.IsManualEntry)
        {
            return VirtualAttendanceFactory.CreatePair(employee, shift.StartTime, shift.StartTime.AddMinutes(leave.TotalMinutes), true);
        }

        if (!(leave.StartTime.HasValue && leave.EndTime.HasValue)) return [];

        var startTime = leave.StartTime.Value;
        var endTime = leave.EndTime.Value;
        return VirtualAttendanceFactory.CreatePair(employee, startTime, endTime, true);
    }
}
