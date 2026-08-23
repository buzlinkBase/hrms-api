using Hrms.Domain.Entities;

namespace DTR.Core;

public interface ILeaveAttendanceStrategy
{
    List<Attendance> CreateVirtualAttendance(
        LeaveApplication leave,
        EmployeeDTRRun employee,
        CurrentShift? shift);
}
