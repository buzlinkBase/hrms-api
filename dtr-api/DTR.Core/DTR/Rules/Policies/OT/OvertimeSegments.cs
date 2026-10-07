namespace DTR.Core;

// Splits an OT range into the two windows the OT handlers treat as OT time:
//  - PreShift:  everything before the scheduled shift start (PreShiftOTHandler's window)
//  - PostShift: everything from the OT start onward (PostShiftOTHandler's window, via
//               AutoComputeOTStartFactory; falls back to the shift end when no OT start is set)
// Minutes in neither window (e.g. between shift start and OT start) are non-OT time and dropped.
public sealed class OvertimeSegments
{
    public TimeRecordCollection PreShift { get; }
    public TimeRecordCollection PostShift { get; }

    private OvertimeSegments(TimeRecordCollection preShift, TimeRecordCollection postShift)
    {
        PreShift = preShift;
        PostShift = postShift;
    }

    public static OvertimeSegments Split(TimeRange otRange, TimeContext context)
    {
        var records = otRange.TimeRecords;
        if (!records.Any()) return new OvertimeSegments(new TimeRecordCollection(), new TimeRecordCollection());

        var shift = context.Payload.Data.CurrentShift;
        var earliestStart = records.Min(r => r.StartTime);
        var latestEnd = records.Max(r => r.EndTime);

        var otStart = AutoComputeOTStartFactory.Create(context).GetOTStartTime() ?? shift.EndTime;
        // Never let the post-shift window reach back before the shift start, so the two segments stay disjoint.
        var postShiftStart = otStart > shift.StartTime ? otStart : shift.StartTime;

        var preShift = records.Exclude(Window(shift.StartTime, latestEnd));
        var postShift = records.Exclude(Window(earliestStart, postShiftStart));
        return new OvertimeSegments(preShift, postShift);
    }

    private static TimeRecordCollection Window(DateTime start, DateTime end)
    {
        return start < end
            ? new TimeRecordCollection { new TimeRecord(start, end) }
            : new TimeRecordCollection();
    }
}
