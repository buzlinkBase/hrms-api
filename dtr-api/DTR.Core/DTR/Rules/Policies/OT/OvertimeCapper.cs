namespace DTR.Core;

// Final OT shaping step, applied by OverTimeHandlerProcessor (and by First8HrPolicy after it
// borrows its regular-time top-up) so both OT paths — auto-computed and approval-based — follow
// the same rules:
//  1. Non-OT time is excluded first: only the pre-shift / post-shift windows the OT handlers
//     recognise count (see OvertimeSegments), filtered by the effective OT Inclusion policy.
//  2. The remaining acceptable OT is capped to the shift's Max OT Hours
//     (TimeShift.MaxOvertimeHours — null or 0 means unlimited), keeping the minutes the policy's
//     strategy prioritises (see OvertimeInclusionStrategies).
public static class OvertimeCapper
{
    // Acceptable OT without applying the Max OT Hours cap.
    public static TimeRange ExcludeNonOvertime(TimeRange otRange, TimeContext context)
    {
        return Apply(otRange, context, maxMinutes: null);
    }

    public static TimeRange Cap(TimeRange otRange, TimeContext context)
    {
        return Apply(otRange, context, MaxOvertimeMinutes(context));
    }

    private static TimeRange Apply(TimeRange otRange, TimeContext context, double? maxMinutes)
    {
        if (otRange == null || otRange.IsEmpty()) return otRange ?? TimeRange.Empty;

        var segments = OvertimeSegments.Split(otRange, context);
        var strategy = OvertimeInclusionStrategyFactory.Create(OvertimeInclusionPolicyResolver.Resolve(context));
        return strategy.Apply(segments, maxMinutes);
    }

    private static double? MaxOvertimeMinutes(TimeContext context)
    {
        var maxHours = context.Payload.Data.CurrentShift.MaxOvertimeHours;
        return maxHours is > 0 ? maxHours.Value * 60 : null;
    }
}
