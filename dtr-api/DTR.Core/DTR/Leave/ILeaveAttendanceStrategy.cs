using Hrms.Domain.Entities;

namespace DTR.Core;

public interface ILeaveAttendanceStrategy
{
    List<Attendance> CreateVirtualAttendance(
        List<Attendance> existing,
        LeaveApplication leave,
        EmployeeDTRRun employee,
        CurrentShift? shift);
}
