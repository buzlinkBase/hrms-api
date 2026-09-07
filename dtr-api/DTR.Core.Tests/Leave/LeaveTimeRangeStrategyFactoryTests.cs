using DTR.Core.Tests.TestSupport;

namespace DTR.Core.Tests.LeaveSubsystem;

/// <summary>
/// LeaveTimeRangeStrategyFactory — the IsManualEntry-based dispatch is currently commented out
/// in production (see LeaveTimeRangeStrategyFactory.cs), so Create always returns
/// TimeRangeLeaveTimeRangeStrategy regardless of the application. This test pins down that
/// CURRENT behavior — both for a manual-entry and a time-range application — rather than the
/// commented-out intent, so a future re-enable of the dispatch will visibly break these tests
/// instead of silently changing behavior unnoticed.
/// </summary>
public class LeaveTimeRangeStrategyFactoryTests : DtrTestBase
{
    [Fact]
    public void ManualEntryApplication_StillReturnsTimeRangeStrategy()
    {
        var application = BuildLeaveApplication(DurationType.Partial, isManualEntry: true, totalMinutes: 60);

        LeaveTimeRangeStrategyFactory.Create(application).Should().BeOfType<TimeRangeLeaveTimeRangeStrategy>();
    }

    [Fact]
    public void TimeRangeApplication_ReturnsTimeRangeStrategy()
    {
        var application = BuildLeaveApplication(DurationType.Partial, isManualEntry: false);

        LeaveTimeRangeStrategyFactory.Create(application).Should().BeOfType<TimeRangeLeaveTimeRangeStrategy>();
    }
}
