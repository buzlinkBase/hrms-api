namespace DTR.Core;

// Caps total OT minutes to the shift's configured Max OT Hours (TimeShift.MaxOvertimeHours)
// — null or 0 means unlimited, matching the Time Shift setup convention. Applied as a final
// step after both OT computation paths (AutoComputedOTHandler and ApprovalBasedOTHandler),
// so the same shift-level ceiling governs OT regardless of whether it was auto-computed or
// manually filed. Keeps the earliest OT minutes and drops anything beyond the cap, mirroring
// how TrimOTForFirst8HrPolicy already trims from the start elsewhere in this pipeline.
public static class OvertimeCapper
{
    public static TimeRange Cap(TimeRange otRange, TimeContext context)
    {
        if (otRange == null || otRange.IsEmpty()) return otRange ?? TimeRange.Empty;

        var maxHours = context.Payload.Data.CurrentShift.MaxOvertimeHours;
        if (maxHours is null || maxHours <= 0) return otRange;

        var maxMinutes = maxHours.Value * 60;
        if (otRange.TotalMinutes <= maxMinutes) return otRange;

        return otRange.TimeRecords.CropFromStart(maxMinutes);
    }
}
