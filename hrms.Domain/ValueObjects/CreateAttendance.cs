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
}

public record UpdateAttendance
{
    public Guid Id { get; set; }
    public DateTime WorkTime { get; set; }
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