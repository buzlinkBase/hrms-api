namespace DTR.Core.Tests.LeaveSubsystem;

/// <summary>
/// LeaveAttendanceStrategyFactory — dispatches by DurationType. Partial is both the explicit
/// case and the fallback for any unmapped value.
/// </summary>
public class LeaveAttendanceStrategyFactoryTests
{
    [Fact]
    public void SingleDay_ReturnsSingleDayStrategy()
    {
        LeaveAttendanceStrategyFactory.Create(DurationType.SingleDay).Should().BeOfType<SingleDayLeaveAttendanceStrategy>();
    }

    [Fact]
    public void MultiDay_ReturnsMultiDayStrategy()
    {
        LeaveAttendanceStrategyFactory.Create(DurationType.MultiDay).Should().BeOfType<MultiDayLeaveAttendanceStrategy>();
    }

    [Fact]
    public void Partial_ReturnsPartialStrategy()
    {
        LeaveAttendanceStrategyFactory.Create(DurationType.Partial).Should().BeOfType<PartialLeaveAttendanceStrategy>();
    }

    [Fact]
    public void UnmappedValue_FallsBackToPartialStrategy()
    {
        LeaveAttendanceStrategyFactory.Create((DurationType)999).Should().BeOfType<PartialLeaveAttendanceStrategy>();
    }

    [Fact]
    public void SameDurationType_ReturnsTheSameCachedInstance()
    {
        // The factory caches one instance per strategy rather than constructing fresh each call.
        LeaveAttendanceStrategyFactory.Create(DurationType.SingleDay)
            .Should().BeSameAs(LeaveAttendanceStrategyFactory.Create(DurationType.SingleDay));
    }
}
