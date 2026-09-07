using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;

namespace DTR.Core.Tests.Services;

/// <summary>
/// OverrideHolidayHandler/FallBackHolidayHandler — the Chain of Responsibility HolidayHandler
/// composes (override wins, fallback reads the base holiday calendar). Pure dictionary/list
/// logic with no TimeContext/DB dependency, mirroring RestDayHandlersTests' approach for the
/// day-off equivalent.
/// </summary>
public class HolidayHandlersTests
{
    private static readonly DateOnly Day = new(2026, 1, 5);

    private static HolidayModel HolidayModel(Guid id, DateOnly date, HolidayType type = HolidayType.LEGAL, bool isPaid = true) => new()
    {
        Id = id,
        HolDate = date,
        HolType = type,
        WorkType = HolidayWorkType.NonWorking,
        IsPaid = isPaid,
    };

    // --- OverrideHolidayHandler ---------------------------------------------------------------

    [Fact]
    public void OverrideHolidayHandler_TrackedHolidayWithReplacementChange_ReturnsReplacementInfo()
    {
        var employee = new EmployeeDTRRun { Id = Guid.NewGuid() };
        var holidayId = Guid.NewGuid();
        var holidays = new List<HolidayModel> { HolidayModel(holidayId, Day, HolidayType.LEGAL) };
        var changes = new Dictionary<Holidaykey, List<ChangeHoliday>>
        {
            [new Holidaykey(employee.Id, Day)] = new List<ChangeHoliday>
            {
                new ChangeHoliday
                {
                    HolidayId = holidayId,
                    EmployeeId = employee.Id,
                    PayrollDate = Day,
                    State = ChangeSchedState.REPLACEMENT,
                    Holiday = new Holiday { HolType = HolidayType.SPECIAL, WorkType = HolidayWorkType.Working, IsPaid = false },
                },
            },
        };

        var handler = new OverrideHolidayHandler(holidays, changes);
        var result = handler.Handle(employee, Day);

        result.Should().ContainSingle();
        result[0].State.Should().Be(ChangeSchedState.REPLACEMENT);
        result[0].HolType.Should().Be(HolidayType.SPECIAL); // from the replacement's Holiday, not the tracked one
        result[0].IsPaid.Should().BeFalse();
    }

    [Fact]
    public void OverrideHolidayHandler_ChangeReferencesAnUntrackedHoliday_IsFilteredOut()
    {
        var employee = new EmployeeDTRRun { Id = Guid.NewGuid() };
        var trackedId = Guid.NewGuid();
        var untrackedId = Guid.NewGuid();
        var holidays = new List<HolidayModel> { HolidayModel(trackedId, Day) };
        var changes = new Dictionary<Holidaykey, List<ChangeHoliday>>
        {
            [new Holidaykey(employee.Id, Day)] = new List<ChangeHoliday>
            {
                new ChangeHoliday { HolidayId = untrackedId, EmployeeId = employee.Id, PayrollDate = Day, State = ChangeSchedState.REPLACEMENT, Holiday = new Holiday() },
            },
        };

        var handler = new OverrideHolidayHandler(holidays, changes);
        handler.Handle(employee, Day).Should().BeEmpty();
    }

    [Fact]
    public void OverrideHolidayHandler_NoChangesForKey_FallsThroughToFallback()
    {
        var employee = new EmployeeDTRRun { Id = Guid.NewGuid() };
        var holidayId = Guid.NewGuid();
        var holidays = new List<HolidayModel> { HolidayModel(holidayId, Day, HolidayType.LEGAL) };
        var changes = new Dictionary<Holidaykey, List<ChangeHoliday>>();

        var overrideHandler = new OverrideHolidayHandler(holidays, changes);
        var fallback = new FallBackHolidayHandler(holidays, changes);
        overrideHandler.SetNextHandler(fallback);

        var result = overrideHandler.Handle(employee, Day);

        result.Should().ContainSingle();
        result[0].State.Should().Be(ChangeSchedState.DEFAULT);
        result[0].HolidayId.Should().Be(holidayId);
    }

    // --- FallBackHolidayHandler ----------------------------------------------------------------

    [Fact]
    public void FallBackHolidayHandler_HolidayOnDate_ReturnsDefaultInfo()
    {
        var employee = new EmployeeDTRRun { Id = Guid.NewGuid() };
        var holidayId = Guid.NewGuid();
        var holidays = new List<HolidayModel> { HolidayModel(holidayId, Day, HolidayType.SPECIAL, isPaid: false) };

        var handler = new FallBackHolidayHandler(holidays, new Dictionary<Holidaykey, List<ChangeHoliday>>());
        var result = handler.Handle(employee, Day);

        result.Should().ContainSingle();
        result[0].State.Should().Be(ChangeSchedState.DEFAULT);
        result[0].HolType.Should().Be(HolidayType.SPECIAL);
        result[0].IsPaid.Should().BeFalse();
        result[0].EmployeeId.Should().Be(employee.Id);
    }

    [Fact]
    public void FallBackHolidayHandler_NoHolidayOnDate_ReturnsEmpty()
    {
        var employee = new EmployeeDTRRun { Id = Guid.NewGuid() };
        var holidays = new List<HolidayModel> { HolidayModel(Guid.NewGuid(), Day.AddDays(1)) };

        var handler = new FallBackHolidayHandler(holidays, new Dictionary<Holidaykey, List<ChangeHoliday>>());
        handler.Handle(employee, Day).Should().BeEmpty();
    }

    [Fact]
    public void FallBackHolidayHandler_HolidayOverriddenForThisEmployee_IsExcluded()
    {
        var employee = new EmployeeDTRRun { Id = Guid.NewGuid() };
        var holidayId = Guid.NewGuid();
        var holidays = new List<HolidayModel> { HolidayModel(holidayId, Day) };
        var changes = new Dictionary<Holidaykey, List<ChangeHoliday>>
        {
            [new Holidaykey(employee.Id, Day)] = new List<ChangeHoliday>
            {
                new ChangeHoliday { HolidayId = holidayId, EmployeeId = employee.Id, PayrollDate = Day, State = ChangeSchedState.OVERRIDEN, Holiday = new Holiday() },
            },
        };

        var handler = new FallBackHolidayHandler(holidays, changes);
        handler.Handle(employee, Day).Should().BeEmpty();
    }

    // --- Chain precedence ------------------------------------------------------------------

    [Fact]
    public void Chain_OverrideWinsOverFallbackWhenBothWouldMatch()
    {
        var employee = new EmployeeDTRRun { Id = Guid.NewGuid() };
        var holidayId = Guid.NewGuid();
        var holidays = new List<HolidayModel> { HolidayModel(holidayId, Day, HolidayType.LEGAL) };
        var changes = new Dictionary<Holidaykey, List<ChangeHoliday>>
        {
            [new Holidaykey(employee.Id, Day)] = new List<ChangeHoliday>
            {
                new ChangeHoliday { HolidayId = holidayId, EmployeeId = employee.Id, PayrollDate = Day, State = ChangeSchedState.REPLACEMENT, Holiday = new Holiday { HolType = HolidayType.SPECIAL } },
            },
        };

        var overrideHandler = new OverrideHolidayHandler(holidays, changes);
        var fallback = new FallBackHolidayHandler(holidays, changes);
        overrideHandler.SetNextHandler(fallback);

        var result = overrideHandler.Handle(employee, Day);

        result[0].State.Should().Be(ChangeSchedState.REPLACEMENT);
        result[0].HolType.Should().Be(HolidayType.SPECIAL);
    }

    [Fact]
    public void Chain_NoOverride_FallsBackToBaseCalendarHoliday()
    {
        var employee = new EmployeeDTRRun { Id = Guid.NewGuid() };
        var holidayId = Guid.NewGuid();
        var holidays = new List<HolidayModel> { HolidayModel(holidayId, Day, HolidayType.LEGAL) };
        var changes = new Dictionary<Holidaykey, List<ChangeHoliday>>();

        var overrideHandler = new OverrideHolidayHandler(holidays, changes);
        var fallback = new FallBackHolidayHandler(holidays, changes);
        overrideHandler.SetNextHandler(fallback);

        var result = overrideHandler.Handle(employee, Day);

        result[0].State.Should().Be(ChangeSchedState.DEFAULT);
        result[0].HolType.Should().Be(HolidayType.LEGAL);
    }

    [Fact]
    public void Chain_NothingMatchesAnywhere_ReturnsEmptyList()
    {
        var employee = new EmployeeDTRRun { Id = Guid.NewGuid() };
        var overrideHandler = new OverrideHolidayHandler(new List<HolidayModel>(), new Dictionary<Holidaykey, List<ChangeHoliday>>());
        var fallback = new FallBackHolidayHandler(new List<HolidayModel>(), new Dictionary<Holidaykey, List<ChangeHoliday>>());
        overrideHandler.SetNextHandler(fallback);

        overrideHandler.Handle(employee, Day).Should().BeEmpty();
    }
}
