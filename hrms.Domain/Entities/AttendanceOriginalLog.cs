using BuzlinkRepository;
using NetTopologySuite.Geometries;

namespace Hrms.Domain.Entities;

// An UPLOADED or SYNC attendance log as the file/device had it, kept when a user edits or removes
// the log (see AttendanceOriginalLogService). Two jobs: Restore puts it back, and while it exists a
// re-upload or device re-sync of the same punch is skipped (AttendanceService.AddRangeAsync), so
// the user's change sticks. Deleting the whole upload batch removes these too, so the file can be
// imported again from scratch. Hard delete like Attendance itself: once restored it's gone.
[DisableSoftDelete]
public class AttendanceOriginalLog : BaseEntity, IOptionalEmployeeOwned
{
    public ATTENDANCE_ORIGINAL_ACTION Action { get; set; }
    // The live log while EDITED; null once the log is removed (DELETED). Not a foreign key:
    // attendance logs are hard-deleted.
    public Guid? AttendanceId { get; set; }

    // ── The original log ──
    public int? BioId { get; set; }
    public Guid? EmployeeId { get; set; }
    public DateTime WorkDateTime { get; set; }
    public LOGSOURCE LogSource { get; set; }
    public string BatchCode { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string IP { get; set; } = string.Empty;
    public Guid? BranchId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? OperationAreaId { get; set; }
    public Guid? UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int Workstate { get; set; }
    public string Verifycode { get; set; } = string.Empty;
    public string LogRemarks { get; set; } = string.Empty;
    public string EditRemarks { get; set; } = string.Empty;
    public Polygon? Boundary { get; set; }

    // ── Who changed it ──
    // The user's reason for the edit (its remarks) or removal.
    public string Reason { get; set; } = string.Empty;
    public Guid? ActedByUserId { get; set; }
    public string ActedByName { get; set; } = string.Empty;
    // UTC.
    public DateTime ActedAt { get; set; }

    public static AttendanceOriginalLog From(Attendance log) => new()
    {
        BioId = log.BioId,
        EmployeeId = log.EmployeeId,
        WorkDateTime = log.WorkDateTime,
        LogSource = log.LogSource,
        BatchCode = log.BatchCode,
        DeviceName = log.DeviceName,
        IP = log.IP,
        BranchId = log.BranchId,
        DepartmentId = log.DepartmentId,
        ClientId = log.ClientId,
        OperationAreaId = log.OperationAreaId,
        UserId = log.UserId,
        UserName = log.UserName,
        Workstate = log.Workstate,
        Verifycode = log.Verifycode,
        LogRemarks = log.LogRemarks,
        EditRemarks = log.EditRemarks,
        Boundary = log.Boundary,
    };

    public Attendance ToAttendance() => new()
    {
        BioId = BioId,
        EmployeeId = EmployeeId,
        WorkDateTime = WorkDateTime,
        LogSource = LogSource,
        BatchCode = BatchCode,
        DeviceName = DeviceName,
        IP = IP,
        BranchId = BranchId,
        DepartmentId = DepartmentId,
        ClientId = ClientId,
        OperationAreaId = OperationAreaId,
        UserId = UserId,
        UserName = UserName,
        Workstate = Workstate,
        Verifycode = Verifycode,
        LogRemarks = LogRemarks,
        EditRemarks = EditRemarks,
        Boundary = Boundary,
    };
}
