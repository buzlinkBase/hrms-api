namespace DTR.Core;

// One strategy per OT Inclusion policy: decides which OT segments count and, when a cap is
// given, which minutes are kept. A null maxMinutes means no cap — only the policy filter applies.
public interface IOvertimeInclusionStrategy
{
    TimeRange Apply(OvertimeSegments segments, double? maxMinutes);
}

// Pre-shift OT only; over the cap, keep the minutes nearest the shift start.
public class EarlyClockInOvertimeStrategy : IOvertimeInclusionStrategy
{
    public TimeRange Apply(OvertimeSegments segments, double? maxMinutes)
    {
        if (!maxMinutes.HasValue) return segments.PreShift.ToTimeRange();

        // CropFromEnd collects latest-first; restore chronological order like the other strategies.
        var kept = segments.PreShift.CropFromEnd(maxMinutes.Value).TimeRecords.OrderBy(r => r.StartTime);
        return new TimeRecordCollection(kept).ToTimeRange();
    }
}

// Post-shift OT only; over the cap, keep the earliest post-shift minutes.
public class PostShiftWorkOvertimeStrategy : IOvertimeInclusionStrategy
{
    public TimeRange Apply(OvertimeSegments segments, double? maxMinutes)
    {
        return maxMinutes.HasValue
            ? segments.PostShift.CropFromStart(maxMinutes.Value)
            : segments.PostShift.ToTimeRange();
    }
}

// Pre- and post-shift OT; over the cap, fill from post-shift first (earliest minutes), then
// from pre-shift minutes nearest the shift start.
public class AllExcessOvertimeStrategy : IOvertimeInclusionStrategy
{
    public TimeRange Apply(OvertimeSegments segments, double? maxMinutes)
    {
        if (!maxMinutes.HasValue) return Combine(segments.PreShift, segments.PostShift);

        var postShift = segments.PostShift.CropFromStart(maxMinutes.Value);
        var preShift = segments.PreShift.CropFromEnd(maxMinutes.Value - postShift.TotalMinutes);
        return Combine(preShift.TimeRecords, postShift.TimeRecords);
    }

    private static TimeRange Combine(TimeRecordCollection preShift, TimeRecordCollection postShift)
    {
        return new TimeRecordCollection(preShift.Concat(postShift).OrderBy(r => r.StartTime)).ToTimeRange();
    }
}

public static class OvertimeInclusionStrategyFactory
{
    public static IOvertimeInclusionStrategy Create(OvertimeInclusionPolicy policy)
    {
        return policy switch
        {
            OvertimeInclusionPolicy.UseEarlyClockIn => new EarlyClockInOvertimeStrategy(),
            OvertimeInclusionPolicy.UsePostShiftWork => new PostShiftWorkOvertimeStrategy(),
            OvertimeInclusionPolicy.UseAllExcessOver8Hours => new AllExcessOvertimeStrategy(),
            _ => throw new NotImplementedException("OvertimeInclusionStrategyFactory"),
        };
    }
}
