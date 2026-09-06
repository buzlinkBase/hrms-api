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