using Hrms.Core.Services.LeaveConsumption;

namespace hrms.test.LeaveWorkerTests;

/// <summary>
/// LeaveConsumptionCalculator: what a posted DTR charges against leave credits. DTR leave info is
/// in HOURS; the ledger is in DAYS -- raw hours used to be deducted as days (8h = 8 days).
/// </summary>
public class LeaveConsumptionCalculatorTests
{
    private static readonly Guid EmployeeId = Guid.NewGuid();
    private static readonly Guid VacationLeave = Guid.NewGuid();

    private static DailyRecord Day(DateOnly date, double hours, double shiftHours = 8, Guid? leaveId = null, int copies = 1) => new()
    {
        EmployeeId = EmployeeId,
        WorkDate = date,
        ShiftWorkingHour = shiftHours,
        LeavesInfo = Enumerable.Range(0, copies)
            .Select(_ => new DtrLeaveMetaData { LeaveId = leaveId ?? VacationLeave, Hours = hours })
            .ToList(),
    };

    private static LeaveApplication Application(DateOnly from, DateOnly to, DateTime? filedAt = null) => new()
    {
        Id = Guid.NewGuid(),
        EmployeeId = EmployeeId,
        LeaveId = VacationLeave,
        Leave = null!,
        LeaveDateFrom = from,
        LeaveDateTo = to,
        ApprovalStatus = ApprovalStatus.Approved,
        CreatedAt = filedAt ?? new DateTime(2026, 1, 1),
    };

    [Theory]
    [InlineData(8, 8, 1)]
    [InlineData(4, 8, 0.5)]
    [InlineData(10, 8, 1)]      // capped at a full day
    [InlineData(6, 12, 0.5)]    // against that day's own shift length
    [InlineData(4, 0, 0.5)]     // unknown shift length falls back to 8h
    public void ToDays_ConvertsAgainstTheShift(double hours, double shiftHours, double expectedDays)
    {
        LeaveConsumptionCalculator.ToDays(hours, shiftHours).Should().Be((decimal)expectedDays);
    }

    [Fact]
    public void FullLeaveDays_AreChargedInDays_NotHours()
    {
        var app = Application(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 6));

        var result = LeaveConsumptionCalculator.Compute(
            [Day(new DateOnly(2026, 1, 5), 8), Day(new DateOnly(2026, 1, 6), 8)], [app]);

        result.DaysByApplication[app.Id].Should().Be(2m);
    }

    [Fact]
    public void DuplicateLeavesInfoRows_ForTheSameDay_CountOnce()
    {
        var app = Application(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 5));

        var result = LeaveConsumptionCalculator.Compute([Day(new DateOnly(2026, 1, 5), 8, copies: 3)], [app]);

        result.DaysByApplication[app.Id].Should().Be(1m);
    }

    [Fact]
    public void LeaveDays_WithNoApprovedApplication_AreExcluded()
    {
        // The only application passed in doesn't cover Jan 7 -- e.g. the one that did was
        // deleted, declined, cancelled or is still pending, so the caller never loaded it.
        var app = Application(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 5));

        var result = LeaveConsumptionCalculator.Compute(
            [Day(new DateOnly(2026, 1, 5), 8), Day(new DateOnly(2026, 1, 7), 8)], [app]);

        result.DaysByApplication[app.Id].Should().Be(1m);
        result.ExcludedDays.Should().Be(1);
    }

    [Fact]
    public void NoApplicationsAtAll_ChargesNothing()
    {
        var result = LeaveConsumptionCalculator.Compute([Day(new DateOnly(2026, 1, 5), 8)], []);

        result.DaysByApplication.Should().BeEmpty();
        result.ExcludedDays.Should().Be(1);
    }

    [Fact]
    public void OverlappingApplications_ChargeTheDayOnce_ToTheEarliestFiled()
    {
        var first = Application(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 5), new DateTime(2026, 1, 1));
        var second = Application(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 5), new DateTime(2026, 1, 2));

        var result = LeaveConsumptionCalculator.Compute([Day(new DateOnly(2026, 1, 5), 8)], [second, first]);

        result.DaysByApplication.Should().ContainSingle().Which.Should().Be(new KeyValuePair<Guid, decimal>(first.Id, 1m));
    }

    [Fact]
    public void OtherLeaveTypes_OnTheSameDay_DontConsumeThisApplication()
    {
        var app = Application(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 5));

        var result = LeaveConsumptionCalculator.Compute([Day(new DateOnly(2026, 1, 5), 8, leaveId: Guid.NewGuid())], [app]);

        result.DaysByApplication.Should().BeEmpty();
    }
}
