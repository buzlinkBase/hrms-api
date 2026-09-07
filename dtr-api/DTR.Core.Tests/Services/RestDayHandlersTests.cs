using DTR.Core.Services;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;

namespace DTR.Core.Tests.Services;

/// <summary>
/// OverrideDayOffHandler/SpecificDateOffHandler/FallBackDayOffHandler — the Chain of
/// Responsibility RestDayResolver.ResolveAsync wires up (Override -> SpecificDate -> Fallback).
/// Pure dictionary-driven logic with no TimeContext/DB dependency, so these are tested directly
/// against the handlers rather than through RestDayResolver itself (which needs
/// ChangeRestDayService/RestDayDateService — DB-bound services out of scope here).
/// </summary>
public class RestDayHandlersTests
{
    private static readonly DateOnly Day = new(2026, 1, 5); // a Monday

    private static EmployeeDTRRun Employee(Guid id, params DayName[] restDays) => new()
    {
        Id = id,
        RestDays = restDays.Select(d => new RestDayModel { Id = Guid.NewGuid(), DayName = d }).ToList(),
    };

    // --- OverrideDayOffHandler ---------------------------------------------------------------

    [Fact]
    public void OverrideDayOffHandler_ReplacementEntryForKey_ReturnsReplacementRestDay()
    {
        var employee = Employee(Guid.NewGuid());
        var replacementDate = Day.AddDays(3);
        var changes = new Dictionary<ResDaykey, List<ChangeRestDay>>
        {
            [new ResDaykey(employee.Id, Day)] = new List<ChangeRestDay>
            {
                new ChangeRestDay { EmployeeId = employee.Id, PayrollDate = replacementDate, DayName = DayName.Thursday, State = ChangeSchedState.REPLACEMENT },
            },
        };

        var handler = new OverrideDayOffHandler(changes);
        var result = handler.Handle(employee, Day);

        result.Should().NotBeNull();
        result!.State.Should().Be(ChangeSchedState.REPLACEMENT);
        result.PayrollDate.Should().Be(replacementDate);
        result.DayName.Should().Be(DayName.Thursday);
    }

    [Fact]
    public void OverrideDayOffHandler_NoEntryForKey_FallsThroughToNextHandler()
    {
        var employee = Employee(Guid.NewGuid());
        var handler = new OverrideDayOffHandler(new Dictionary<ResDaykey, List<ChangeRestDay>>());
        var next = new SpecificDateOffHandler(new Dictionary<ResDaykey, RestDayDate?>
        {
            [new ResDaykey(employee.Id, Day)] = new RestDayDate { EmployeeId = employee.Id, PayrollDate = Day },
        });
        handler.SetNextHandler(next);

        var result = handler.Handle(employee, Day);

        result.Should().NotBeNull();
        result!.State.Should().Be(ChangeSchedState.DEFAULT); // SpecificDateOffHandler's outcome, not Override's
    }

    [Fact]
    public void OverrideDayOffHandler_EntryPresentButNotReplacementState_IsFilteredOutAtConstruction()
    {
        var employee = Employee(Guid.NewGuid());
        var changes = new Dictionary<ResDaykey, List<ChangeRestDay>>
        {
            [new ResDaykey(employee.Id, Day)] = new List<ChangeRestDay>
            {
                new ChangeRestDay { EmployeeId = employee.Id, PayrollDate = Day, State = ChangeSchedState.OVERRIDEN },
            },
        };

        var handler = new OverrideDayOffHandler(changes);
        var result = handler.Handle(employee, Day); // no next handler -> null if not applicable

        result.Should().BeNull();
    }

    [Fact]
    public void OverrideDayOffHandler_NoHandlerMatchesAnywhereInChain_ReturnsNull()
    {
        var employee = Employee(Guid.NewGuid());
        var handler = new OverrideDayOffHandler(new Dictionary<ResDaykey, List<ChangeRestDay>>());

        handler.Handle(employee, Day).Should().BeNull();
    }

    // --- SpecificDateOffHandler ---------------------------------------------------------------

    [Fact]
    public void SpecificDateOffHandler_DateInDictionary_ReturnsDefaultRestDayForThatDayOfWeek()
    {
        var employee = Employee(Guid.NewGuid());
        var restDates = new Dictionary<ResDaykey, RestDayDate?>
        {
            [new ResDaykey(employee.Id, Day)] = new RestDayDate { EmployeeId = employee.Id, PayrollDate = Day },
        };

        var handler = new SpecificDateOffHandler(restDates);
        var result = handler.Handle(employee, Day);

        result.Should().NotBeNull();
        result!.State.Should().Be(ChangeSchedState.DEFAULT);
        result.PayrollDate.Should().Be(Day);
        result.DayName.Should().Be((DayName)Day.DayOfWeek);
    }

    [Fact]
    public void SpecificDateOffHandler_DateNotInDictionary_ReturnsNullWithNoNextHandler()
    {
        var employee = Employee(Guid.NewGuid());
        var handler = new SpecificDateOffHandler(new Dictionary<ResDaykey, RestDayDate?>());

        handler.Handle(employee, Day).Should().BeNull();
    }

    // --- FallBackDayOffHandler -----------------------------------------------------------------

    [Fact]
    public void FallBackDayOffHandler_EmployeeHasMatchingWeeklyRestDay_ReturnsDefaultRestDay()
    {
        var employeeId = Guid.NewGuid();
        var employee = Employee(employeeId, (DayName)Day.DayOfWeek);

        var handler = new FallBackDayOffHandler(new Dictionary<ResDaykey, List<ChangeRestDay>>());
        var result = handler.Handle(employee, Day);

        result.Should().NotBeNull();
        result!.State.Should().Be(ChangeSchedState.DEFAULT);
        result.DayName.Should().Be((DayName)Day.DayOfWeek);
    }

    [Fact]
    public void FallBackDayOffHandler_EmployeeHasNoWeeklyRestDays_ReturnsNull()
    {
        var employee = Employee(Guid.NewGuid()); // no RestDays configured
        var handler = new FallBackDayOffHandler(new Dictionary<ResDaykey, List<ChangeRestDay>>());

        handler.Handle(employee, Day).Should().BeNull();
    }

    [Fact]
    public void FallBackDayOffHandler_MatchingRestDayButOverriddenForThisDate_ReturnsNull()
    {
        var employeeId = Guid.NewGuid();
        var employee = Employee(employeeId, (DayName)Day.DayOfWeek);
        var offs = new Dictionary<ResDaykey, List<ChangeRestDay>>
        {
            [new ResDaykey(employeeId, Day)] = new List<ChangeRestDay>
            {
                new ChangeRestDay { EmployeeId = employeeId, PayrollDate = Day, DayName = (DayName)Day.DayOfWeek, State = ChangeSchedState.OVERRIDEN },
            },
        };

        var handler = new FallBackDayOffHandler(offs);
        handler.Handle(employee, Day).Should().BeNull();
    }

    // --- Full chain precedence (mirrors RestDayResolver.ResolveAsync's wiring) ----------------

    [Fact]
    public void FullChain_OverrideWinsOverSpecificDateAndFallback()
    {
        var employeeId = Guid.NewGuid();
        var employee = Employee(employeeId, (DayName)Day.DayOfWeek); // fallback would also match
        var replacementDate = Day.AddDays(2);
        var offs = new Dictionary<ResDaykey, List<ChangeRestDay>>
        {
            [new ResDaykey(employeeId, Day)] = new List<ChangeRestDay>
            {
                new ChangeRestDay { EmployeeId = employeeId, PayrollDate = replacementDate, DayName = DayName.Wednesday, State = ChangeSchedState.REPLACEMENT },
            },
        };
        var specDates = new Dictionary<ResDaykey, RestDayDate?>
        {
            [new ResDaykey(employeeId, Day)] = new RestDayDate { EmployeeId = employeeId, PayrollDate = Day },
        };

        var overrideOff = new OverrideDayOffHandler(offs);
        var specDateOff = new SpecificDateOffHandler(specDates);
        var fallback = new FallBackDayOffHandler(offs);
        overrideOff.SetNextHandler(specDateOff);
        specDateOff.SetNextHandler(fallback);

        var result = overrideOff.Handle(employee, Day);

        result!.State.Should().Be(ChangeSchedState.REPLACEMENT);
        result.PayrollDate.Should().Be(replacementDate);
    }

    [Fact]
    public void FullChain_NoOverrideOrSpecificDate_FallsBackToWeeklyRestDay()
    {
        var employeeId = Guid.NewGuid();
        var employee = Employee(employeeId, (DayName)Day.DayOfWeek);
        var offs = new Dictionary<ResDaykey, List<ChangeRestDay>>();

        var overrideOff = new OverrideDayOffHandler(offs);
        var specDateOff = new SpecificDateOffHandler(new Dictionary<ResDaykey, RestDayDate?>());
        var fallback = new FallBackDayOffHandler(offs);
        overrideOff.SetNextHandler(specDateOff);
        specDateOff.SetNextHandler(fallback);

        var result = overrideOff.Handle(employee, Day);

        result!.State.Should().Be(ChangeSchedState.DEFAULT);
    }

    [Fact]
    public void FullChain_NothingMatchesAnywhere_ReturnsNull()
    {
        var employeeId = Guid.NewGuid();
        var employee = Employee(employeeId); // no weekly rest days at all
        var offs = new Dictionary<ResDaykey, List<ChangeRestDay>>();

        var overrideOff = new OverrideDayOffHandler(offs);
        var specDateOff = new SpecificDateOffHandler(new Dictionary<ResDaykey, RestDayDate?>());
        var fallback = new FallBackDayOffHandler(offs);
        overrideOff.SetNextHandler(specDateOff);
        specDateOff.SetNextHandler(fallback);

        overrideOff.Handle(employee, Day).Should().BeNull();
    }
}
