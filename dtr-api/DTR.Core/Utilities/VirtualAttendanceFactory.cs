using Hrms.Domain.Entities;

namespace DTR.Core;

public static class VirtualAttendanceFactory
{
    public static Attendance Create(EmployeeDTRRun employee, DateTime workDateTime, bool isLeave = false) => new()
    {
        BioId = employee.BioId,
        BranchId = employee.BranchId,
        ClientId = employee.ClientId,
        DepartmentId = employee.DepartmentId,
        OperationAreaId = employee.AreaId,
        EmployeeId = employee.Id,
        WorkDateTime = workDateTime,
        IsVirtual = true,
        IsLeave = isLeave,
    };

    public static List<Attendance> CreatePair(EmployeeDTRRun employee, DateTime start, DateTime end, bool isLeave = false) =>
    [
        Create(employee, start, isLeave),
        Create(employee, end, isLeave),
    ];
}
