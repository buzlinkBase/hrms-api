using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

/// <summary>The employee assignment a manual punch defaults to.</summary>
public sealed record ManualAttendanceEmployee(
    Guid Id, int? BioId, Guid? BranchId, Guid? DepartmentId, Guid? ClientId, Guid? AreaId)
{
    public AttendanceLocation Location => new(BranchId, ClientId, AreaId, DepartmentId);
}

/// <summary>
/// Builds a manual attendance log (Attendance Manual Entry, or a time log added from the DTR):
/// the branch/client/project site/department chosen for the punch, with the blanks filled from
/// the employee's own (see <see cref="AttendanceLocationResolver"/>).
/// </summary>
public static class ManualAttendanceFactory
{
    public static Attendance Create(CreateAttendance entry, ManualAttendanceEmployee employee, string batchCode, Func<Guid, Guid?> branchOfArea)
    {
        var location = AttendanceLocationResolver.Resolve(
            new AttendanceLocation(entry.BranchId, entry.ClientId, entry.OperationAreaId, entry.DepartmentId),
            employee.Location,
            branchOfArea);
        return new Attendance
        {
            BatchCode = batchCode,
            EmployeeId = employee.Id,
            BioId = employee.BioId,
            WorkDateTime = entry.WorkTime,
            BranchId = location.BranchId,
            DepartmentId = location.DepartmentId,
            ClientId = location.ClientId,
            OperationAreaId = location.OperationAreaId,
            DeviceName = "Manual",
            IP = string.Empty,
            Boundary = null,
            LogSource = LOGSOURCE.MANUAL,
            EditRemarks = entry.Remarks,
        };
    }
}
