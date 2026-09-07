using DTR.Core.Tests.TestSupport;
using Hrms.Core.Services;
using Hrms.Domain.Entities;

namespace DTR.Core.Tests.LeaveSubsystem;

/// <summary>
/// LeaveApplicationProvider.GetApplications — filters an employee's raw leave applications to
/// those covering the queried date, then returns a PER-DAY clone with LeaveDateFrom/To pinned
/// to that date and StartTime/EndTime's time-of-day components re-applied onto it (so a
/// multi-day range with partial times gets the same daily window on every day it spans). The
/// isCross check re-derives whether EndTime spilled past the ORIGINAL LeaveDateTo, so a clone
/// for an earlier day within the range doesn't wrongly inherit that spillover.
/// </summary>
public class LeaveApplicationProviderTests
{
    private static Guid NewEmployeeId() => Guid.NewGuid();

    private static LeaveApplicationProvider BuildProvider(Guid employeeId, params LeaveApplication[] applications)
    {
        var employee = new EmployeeDTRRun { Id = employeeId };
        var dict = new Dictionary<Leavekey, List<LeaveApplication>>
        {
            [new Leavekey(employeeId)] = applications.ToList(),
        };
        return new LeaveApplicationProvider(dict, employee);
    }

    private static LeaveApplication RawApplication(
        DateOnly from, DateOnly to, DateTime? startTime = null, DateTime? endTime = null) => new()
    {
        Id = Guid.NewGuid(),
        LeaveId = Guid.NewGuid(),
        Leave = new Leave { Description = "Test" },
        LeaveDateFrom = from,
        LeaveDateTo = to,
        StartTime = startTime,
        EndTime = endTime,
        PayType = PayType.WithPay,
    };

    [Fact]
    public void DateWithinRange_ReturnsAClonePinnedToThatDate()
    {
        var employeeId = NewEmployeeId();
        var app = RawApplication(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 7));
        var provider = BuildProvider(employeeId, app);

        var result = provider.GetApplications(new DateOnly(2026, 1, 6));

        result.Should().ContainSingle();
        result[0].LeaveDateFrom.Should().Be(new DateOnly(2026, 1, 6));
        result[0].LeaveDateTo.Should().Be(new DateOnly(2026, 1, 6));
        result[0].Id.Should().Be(app.Id); // same underlying application, just re-dated
    }

    [Fact]
    public void DateOutsideRange_ReturnsEmpty()
    {
        var employeeId = NewEmployeeId();
        var app = RawApplication(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 7));
        var provider = BuildProvider(employeeId, app);

        provider.GetApplications(new DateOnly(2026, 1, 10)).Should().BeEmpty();
    }

    [Fact]
    public void NoApplicationsForEmployee_ReturnsEmpty()
    {
        var provider = BuildProvider(NewEmployeeId()); // nothing seeded for this employee

        provider.GetApplications(new DateOnly(2026, 1, 5)).Should().BeEmpty();
    }

    [Fact]
    public void PartialTimes_AreRemappedOntoTheQueriedDate_NonCrossCase()
    {
        var employeeId = NewEmployeeId();
        var app = RawApplication(
            new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 7),
            startTime: new DateTime(2026, 1, 5, 9, 0, 0),
            endTime: new DateTime(2026, 1, 5, 17, 0, 0));
        var provider = BuildProvider(employeeId, app);

        var result = provider.GetApplications(new DateOnly(2026, 1, 6)); // day 2 of the range

        result[0].StartTime.Should().Be(new DateTime(2026, 1, 6, 9, 0, 0));
        result[0].EndTime.Should().Be(new DateTime(2026, 1, 6, 17, 0, 0));
    }

    [Fact]
    public void PartialTimes_CrossingIntoTheNextCalendarDay_EndTimeLandsOnQueriedDatePlusOne()
    {
        var employeeId = NewEmployeeId();
        // EndTime's own date (Jan 6) is after the application's LeaveDateTo (Jan 5) -> isCross.
        var app = RawApplication(
            new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 5),
            startTime: new DateTime(2026, 1, 5, 22, 0, 0),
            endTime: new DateTime(2026, 1, 6, 2, 0, 0));
        var provider = BuildProvider(employeeId, app);

        var result = provider.GetApplications(new DateOnly(2026, 1, 5));

        result[0].StartTime.Should().Be(new DateTime(2026, 1, 5, 22, 0, 0));
        result[0].EndTime.Should().Be(new DateTime(2026, 1, 6, 2, 0, 0));
    }

    [Fact]
    public void NoStartOrEndTime_RemainsNull()
    {
        var employeeId = NewEmployeeId();
        var app = RawApplication(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 5)); // SingleDay/MultiDay style, no partial times
        var provider = BuildProvider(employeeId, app);

        var result = provider.GetApplications(new DateOnly(2026, 1, 5));

        result[0].StartTime.Should().BeNull();
        result[0].EndTime.Should().BeNull();
    }

    [Fact]
    public void MultipleApplicationsCoveringTheSameDate_ReturnsAllOfThem()
    {
        var employeeId = NewEmployeeId();
        var appA = RawApplication(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 5));
        var appB = RawApplication(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 5));
        var provider = BuildProvider(employeeId, appA, appB);

        provider.GetApplications(new DateOnly(2026, 1, 5)).Should().HaveCount(2);
    }
}
