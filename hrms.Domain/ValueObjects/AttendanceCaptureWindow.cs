namespace Hrms.Domain.ValueObjects;

// The attendance logs a shift owns: from its earliest allowed clock-in (the company's
// TimeInAllowance, e.g. -120 = up to 2h before the shift start) up to, but not including, the
// next shift's earliest allowed clock-in. Split shifts have no fixed start to measure against, so
// they get no allowance. Shared by the DTR (DTR.Core AttendanceProvider) and the DTR Attendance
// Logs view (AttendanceService.GetAllLogsInRange) so both always work from the same logs.
public readonly record struct AttendanceCaptureWindow(DateTime From, DateTime To)
{
    public static AttendanceCaptureWindow For(TimeShiftType? shiftType, DateTime shiftStart, DateTime nextShiftStart, double timeInAllowanceMinutes)
    {
        var allowance = shiftType == TimeShiftType.SPLIT ? 0 : timeInAllowanceMinutes;
        return new AttendanceCaptureWindow(shiftStart.AddMinutes(allowance), nextShiftStart.AddMinutes(allowance));
    }

    public bool Contains(DateTime time) => time >= From && time < To;
}
