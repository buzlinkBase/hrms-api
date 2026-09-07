using Hrms.Domain.Entities;
namespace DTR.Core;
/// <summary>
/// StartTime/EndTime partial leave. A virtual attendance pair is already injected by
/// PartialLeaveAttendanceStrategy; this strategy classifies how many of those minutes
/// count as leave by intersecting the declared block with actual work hours.
/// </summary>
public sealed class TimeRangeLeaveTimeRangeStrategy : ILeaveTimeRangeStrategy
{
    public TimeRange ComputeTimeRange(LeaveApplication application, TimeContext context)
    {
        // 2. Safe navigation of context graph
        var payload = context.Payload;
        var data = payload.Data;
        var ledger = payload.Ledger;

        if (data.CurrentAttendance == null || data.CurrentShift == null || ledger == null)
            return TimeRange.Empty;

        // 3. Safely filter virtual leave records
        var virtualLeaveAtt = VirtualTimeComposer
            .SetLeaveAttendance(application, context.Payload.Data.Employee, context.Payload.Data.CurrentShift);
        if (!virtualLeaveAtt.Any()) return TimeRange.Empty;

        // 4. Protect against unsorted attendance lists (prevents inverted/negative time ranges)
        var start = virtualLeaveAtt.Min(x => x.WorkDateTime);
        var end = virtualLeaveAtt.Max(x => x.WorkDateTime);
        if (start >= end) return TimeRange.Empty;

        // 5. Guard work hours ledger lookup
        var workHours = ledger.GetByTag("work_time", context);
        if (workHours?.TimeRecords == null) return TimeRange.Empty;

        var claimedLeaved = context.Payload.Ledger
            .GetByStartWithTag("leave", context);

        var leaveBlock = new TimeRecordCollection
        {
            new TimeRecord
            {
                StartTime = start,
                EndTime   = end,
                IsVirtual =  true,
                IsLeave = true
            }
        }.Exclude(claimedLeaved);
        var capped = leaveBlock
            .CapAndCrop(data.CurrentShift);
        if (capped?.TimeRecords == null)
            return TimeRange.Empty;

        return capped.TimeRecords
            .Intersect(workHours.TimeRecords)
            .ToTimeRange();

    }
}
/// <summary>
/// Hours-only partial leave. Employee clocks in/out normally; no virtual attendance
/// is injected. The leave block is the declared TotalMinutes, capped to the shift max.
/// </summary>
public sealed class ManualEntryLeaveTimeRangeStrategy : ILeaveTimeRangeStrategy
{
    public TimeRange ComputeTimeRange(LeaveApplication application, TimeContext context)
    {
        var shift = context.Payload.Data.CurrentShift;
        if (shift == null) return TimeRange.Empty;

        var minutes = Math.Min(application.TotalMinutes, shift.MaxWorkingMinutes);
        if (minutes <= 0) return TimeRange.Empty;

        // Anchored at shift start, matching PartialLeaveAttendanceStrategy's manual-entry
        // virtual attendance pair (VirtualAttendanceFactory.CreatePair(shift.StartTime,
        // shift.StartTime.AddMinutes(leave.TotalMinutes))) -- the same convention for a
        // duration-only leave with no declared clock time.
        var start = shift.StartTime;
        var end = start.AddMinutes(minutes);
        var records = new TimeRecordCollection
        {
            new TimeRecord { StartTime = start, EndTime = end, IsVirtual = true, IsLeave = true },
        };
        return new TimeRange(minutes, records);
    }
}
