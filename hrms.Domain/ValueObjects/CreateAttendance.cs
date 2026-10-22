namespace Hrms.Domain.ValueObjects;

public record AttendanceFilter : EmployeeFilter
{
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
}

public record AttendanceFilterDate
{
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public Guid? EmployeeId { get; set; }
}

public record CreateAttendance
{
    public DateTime WorkTime { get; set; }
    public Guid EmployeeId { get; set; }
    // Required — why this punch is being manually entered instead of coming from a device.
    // See AttendanceController.ManualEntry.
    public string Remarks { get; set; } = string.Empty;
    // Where the punch happened, when not the employee's own assignment (e.g. relieving at
    // another site that day). Null = the employee's own branch/client/area/department.
    public Guid? BranchId { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? OperationAreaId { get; set; }
    public Guid? DepartmentId { get; set; }
}

public record UpdateAttendance
{
    public Guid Id { get; set; }
    public DateTime WorkTime { get; set; }
    // Required — why this log is being manually edited. See AttendanceController.UpdateAtt.
    public string Remarks { get; set; } = string.Empty;
}

public class UnRegisteredAttendance
{
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
}

public class TagEmployeeRequest
{
    public Guid EmployeeId { get; set; }
    public Guid AttId { get; set; }
}

public class AttendanceModel
{
    public Guid Id { get; set; }
    public int? BioId { get; set; }
    public Guid? EmployeeId { get; set; }
    public string? Name { get; set; }
    public DateTime WorkDateTime { get; set; }
    public string Batch { get; set; } = string.Empty;
    public string LogSource { get; set; } = LOGSOURCE.OTHER.ToString();
    public string? Branch { get; set; }
    public string? Client { get; set; }
    public string? Area { get; set; }
    // Auto-generated context (e.g. Pass Slip's "Pass Slip - {Purpose}") vs. the user-authored
    // reason from manual create/edit/upload — see AttendanceController's manual-entry/PUT/
    // upload-att-log actions.
    public string? LogRemarks { get; set; }
    public string? EditRemarks { get; set; }
    // Set on an edited uploaded/synced log (DTR Attendance Logs view): its saved original
    // (AttendanceOriginalLog) and the time the file/device had, for Restore.
    public Guid? OriginalLogId { get; set; }
    public DateTime? OriginalWorkDateTime { get; set; }
}

// A removed uploaded/synced log whose original is kept (AttendanceOriginalLog) -- listed in the DTR
// Attendance Logs view so it can be restored.
public class RemovedAttendanceLogModel
{
    public Guid Id { get; set; }
    public DateTime WorkDateTime { get; set; }
    public string LogSource { get; set; } = string.Empty;
    public string Batch { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string RemovedBy { get; set; } = string.Empty;
    // UTC.
    public DateTime RemovedAt { get; set; }
}

// One attendance file upload (all UPLOADED logs sharing a BatchCode), for Upload History.
public class UploadBatchModel
{
    public string BatchCode { get; set; } = string.Empty;
    // UTC; null when every log of the batch was removed one by one (only saved originals remain).
    public DateTime? UploadedAt { get; set; }
    public string Remarks { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public int LogCount { get; set; }
    public int EmployeeCount { get; set; }
    public DateTime? FirstLog { get; set; }
    public DateTime? LastLog { get; set; }
    // Logs of this batch a user edited or removed (saved originals).
    public int ChangedCount { get; set; }
}

public record UploadBatchFilter
{
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
}


public class ManualAttModel
{
    public string BatchCode { get; set; }
    public DateTime Date { get; set; }
}

public class CurrentShiftInfo
{
    public TimeShiftType? ShiftType { get; set; }
    //public DateOnly ShiftDate { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}