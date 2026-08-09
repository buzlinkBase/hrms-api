namespace DTR.Core;

public class LeaveTimePipeline
{
    private readonly TimeContext _context;
    public LeaveTimePipeline(TimeContext context)
    {
        _context = context;
    }
    public TimeRange Apply(TimeRange input)
    {
        if (_context.Payload.Data.CurrentLeave == null) return TimeRange.Empty;
        if (_context.Payload.Data.CurrentLeave.DayType == LeaveDayType.WholeDay)
        {
            return new TimeRange(_context.Payload.Data.CurrentShift.MaxWorkingMinutes);
        }
        if (_context.Payload.Data.CurrentLeave.DayType == LeaveDayType.HalfDay)
        {
            return new TimeRange(_context.Payload.Data.CurrentShift.MaxWorkingMinutes / 2);
        }
        return TimeRange.Empty;
    }
}