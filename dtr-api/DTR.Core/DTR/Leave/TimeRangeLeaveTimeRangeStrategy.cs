using System;
using System.Linq;
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
        // 1. Guard against null application and missing time parameters
        if (application == null)
            return TimeRange.Empty;

        // 2. Safe navigation of context graph
        var payload = context?.Payload;
        var data = payload?.Data;
        var ledger = payload?.Ledger;

        if (data?.CurrentAttendance == null || data.CurrentShift == null || ledger == null)
            return TimeRange.Empty;

        // 3. Safely filter virtual leave records
        var virtualLeaveAtt = data.CurrentAttendance
            .Where(x => x != null && x.IsVirtual && x.IsLeave)
            .ToList();

        if (virtualLeaveAtt.Count < 2)
            return TimeRange.Empty;

        // 4. Protect against unsorted attendance lists (prevents inverted/negative time ranges)
        var start = virtualLeaveAtt.Min(x => x.WorkDateTime);
        var end = virtualLeaveAtt.Max(x => x.WorkDateTime);

        if (start >= end)
            return TimeRange.Empty;

        // 5. Guard work hours ledger lookup
        var workHours = ledger.GetByTag("work_time", context);
        if (workHours?.TimeRecords == null)
            return TimeRange.Empty;

        var leaveBlock = new TimeRecordCollection
        {
            new TimeRecord
            {
                StartTime = start,
                EndTime   = end,
                IsVirtual =  true,
                IsLeave = true
            }
        };

        // 6. Safe CapAndCrop and Intersection execution

        //var amBreak = context.Payload.Ledger.GetByTag("morning_break_period", context);
        //var lunchBreak = context.Payload.Ledger.GetByTag("lunch_break_period_use_time", context);
        //var pmBreak = context.Payload.Ledger.GetByTag("pm_break_period", context);
        //var break_period = context.Payload.Ledger.GetByTag("break_period", context);
        //var NoneBreak = context.Payload.Ledger.GetByTag("No_lunch_break_gt_480MinutesShift", context);

        var capped = leaveBlock
            //.Exclude(amBreak.TimeRecords)
            //.Exclude(lunchBreak.TimeRecords)
            //.Exclude(pmBreak.TimeRecords)
            //.Exclude(break_period.TimeRecords)
            //.Exclude(NoneBreak.TimeRecords)
            .CapAndCrop(data.CurrentShift);

        if (capped?.TimeRecords == null)
            return TimeRange.Empty;

        return  capped.TimeRecords.Intersect(workHours.TimeRecords).ToTimeRange();
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
        var maxMinutes = context.Payload.Data.CurrentShift?.MaxWorkingMinutes ?? 0;
        return new TimeRange(Math.Min(application.TotalMinutes, maxMinutes));
    }
}
