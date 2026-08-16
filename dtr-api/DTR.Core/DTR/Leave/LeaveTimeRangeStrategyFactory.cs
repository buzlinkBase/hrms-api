using Hrms.Domain.Entities;

namespace DTR.Core;

/// <summary>
/// Selects the correct ILeaveTimeRangeStrategy for a partial leave application.
/// IsManualEntry is the discriminator: the employee declared total hours (manual)
/// vs. declared a specific time window (time-range).
/// </summary>
public static class LeaveTimeRangeStrategyFactory
{
    private static readonly ILeaveTimeRangeStrategy _manual    = new ManualEntryLeaveTimeRangeStrategy();
    private static readonly ILeaveTimeRangeStrategy _timeRange = new TimeRangeLeaveTimeRangeStrategy();

    public static ILeaveTimeRangeStrategy Create(LeaveApplication application) =>
        application.IsManualEntry ? _manual : _timeRange;
}
