namespace Hrms.Domain.ValueObjects;

public class SetEmployeeFixedScheduleDay
{
    public Guid TimeShiftId { get; set; }
}

public class EmployeeFixedScheduleModel
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public DayName DayName { get; set; }
    public Guid TimeShiftId { get; set; }
    public string? TimeShiftName { get; set; }
    public TimeShiftType ShiftType { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
}
