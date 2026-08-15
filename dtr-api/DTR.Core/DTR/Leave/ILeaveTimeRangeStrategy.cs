using Hrms.Domain.Entities;

namespace DTR.Core;

/// <summary>
/// Computes the leave TimeRange (minutes) for a given partial-leave application.
/// Parallel to ILeaveAttendanceStrategy — attendance injection and time classification
/// are separate concerns.
/// </summary>
public interface ILeaveTimeRangeStrategy
{
    TimeRange ComputeTimeRange(LeaveApplication application, TimeContext context);
}
