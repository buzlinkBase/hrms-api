
using MessagePack;

namespace Hrms.Domain.ValueObjects;

public partial class CreateTimeShift
{
    public string ShiftName { get; set; } = string.Empty;
    public TimeShiftType ShiftType { get; set; }
    public TimeSpan StartTime { get; set; } = TimeSpan.Zero;
    public TimeSpan EndTime { get; set; } = TimeSpan.Zero;
    public BreakMode WithAMBreak { get; set; } = BreakMode.PAID_BREAK;
    public TimeSpan? AMStartTime { get; set; } = TimeSpan.Zero;
    public TimeSpan? AMEndTime { get; set; } = TimeSpan.Zero;
    public BreakMode WithLunchBreak { get; set; } = BreakMode.UNPAID_BREAK;
    public TimeSpan? LunchStartTime { get; set; } = TimeSpan.Zero;
    public TimeSpan? LunchEndTime { get; set; } = TimeSpan.Zero;
    public BreakMode WithPMBreak { get; set; } = BreakMode.PAID_BREAK;
    public TimeSpan? PMStartTime { get; set; } = TimeSpan.Zero;
    public TimeSpan? PMEndTime { get; set; } = TimeSpan.Zero;
    public double GracePeriodMinutes { get; set; } = 0;
    public double BreakDurationMinutes { get; set; } = 60;
    public bool WithOT { get; set; } = false;
    public bool OTRequireTimeIn { get; set; } = false;
    public TimeSpan OTStart { get; set; }
    public double OverTimeThreshold { get; set; } = 60;
    // Null or 0 = no limit; otherwise the max OT hours creditable for a day on this shift.
    public double? MaxOvertimeHours { get; set; } = null;
    public double MinimumWorkMinutes { get; set; } = 0;
    public double MaxWorkingMinutes { get; set; } = 480;
}

public partial class UpdateTimeShift : CreateTimeShift
{
    public Guid? Id { get; set; }
}

public partial class TimeShiftModel : UpdateTimeShift
{
}