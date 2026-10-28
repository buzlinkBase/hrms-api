using DTR.Core.Tests.TestSupport;

namespace DTR.Core.Tests.Pipelines;

/// <summary>
/// Full-row proof that a localized special holiday only reaches employees at one of its project
/// sites: the same shift on the same date lands in the Special Holiday column for a covered site
/// and in Regular for anyone else. Same call sequence as DtrRowIntegrationTests (the one
/// CleanDTRDetailProcessor uses); the day's site is the CreateContext areaId.
/// </summary>
public class LocalizedHolidayDtrRowTests : DtrTestBase
{
    private static readonly DateOnly HolidayDate = new(2026, 9, 9);
    private static readonly DateTime ShiftStart = new(2026, 9, 9, 8, 0, 0);
    private static readonly DateTime ShiftEnd = new(2026, 9, 9, 17, 0, 0);

    // Fixed ids, readable in failures.
    private static readonly Guid Cebu = new("00000000-0000-0000-0000-00000000ceb0");
    private static readonly Guid Mandaue = new("00000000-0000-0000-0000-0000000a0de0");
    private static readonly Guid Manila = new("00000000-0000-0000-0000-00000000a1a0");

    private record RowResult(TimeRange Regular, TimeRange LegalHoliday, TimeRange SpecialHoliday);

    private RowResult RunRow(HolidayInfo holiday, Guid? employeeSite, HolidayTimeBasis basis)
    {
        var employeeId = NewEmployeeId();
        holiday.EmployeeId = employeeId;
        var context = CreateContext(ShiftStart, ShiftEnd, basis, Holidays(employeeId, holiday), employeeId, employeeSite);
        context.Payload.Data.CurrentShift.ShiftDate = HolidayDate;
        context.Payload.Data.CurrentDate = HolidayDate;

        var range = context.CanonicalTimeRange;
        // Exact order CleanDTRDetailProcessor uses when building PipeLineResult.
        var regular = new NonHolidayDutyTimePipeline().Apply(context, range);
        var legal = new HolidayDutyTimePipeline().Apply(context, HolidayType.LEGAL, range);
        var special = new HolidayDutyTimePipeline().Apply(context, HolidayType.SPECIAL, range);
        return new RowResult(regular, legal, special);
    }

    private static void ShouldBeTheSpecialHoliday(RowResult row)
    {
        row.SpecialHoliday.TotalMinutes.Should().Be(540);
        row.Regular.IsEmpty().Should().BeTrue();
        row.LegalHoliday.IsEmpty().Should().BeTrue();
    }

    private static void ShouldBeAnOrdinaryDay(RowResult row)
    {
        row.Regular.TotalMinutes.Should().Be(540);
        row.SpecialHoliday.IsEmpty().Should().BeTrue();
        row.LegalHoliday.IsEmpty().Should().BeTrue();
    }

    [Theory]
    [InlineData(HolidayTimeBasis.BasedOnTimeInDayType)]
    [InlineData(HolidayTimeBasis.BasedOnActualWorkHours)]
    public void LocalizedSpecial_EmployeeAtACoveredSite_GetsTheHoliday(HolidayTimeBasis basis)
    {
        var holiday = Holiday(HolidayType.SPECIAL, HolidayDate, areaIds: [Cebu, Mandaue]);

        ShouldBeTheSpecialHoliday(RunRow(holiday, Mandaue, basis));
    }

    [Theory]
    [InlineData(HolidayTimeBasis.BasedOnTimeInDayType)]
    [InlineData(HolidayTimeBasis.BasedOnActualWorkHours)]
    public void LocalizedSpecial_EmployeeAtAnotherSite_WorksAnOrdinaryDay(HolidayTimeBasis basis)
    {
        var holiday = Holiday(HolidayType.SPECIAL, HolidayDate, areaIds: [Cebu, Mandaue]);

        ShouldBeAnOrdinaryDay(RunRow(holiday, Manila, basis));
    }

    [Theory]
    [InlineData(HolidayTimeBasis.BasedOnTimeInDayType)]
    [InlineData(HolidayTimeBasis.BasedOnActualWorkHours)]
    public void LocalizedSpecial_EmployeeWithNoSite_WorksAnOrdinaryDay(HolidayTimeBasis basis)
    {
        var holiday = Holiday(HolidayType.SPECIAL, HolidayDate, areaIds: [Cebu, Mandaue]);

        ShouldBeAnOrdinaryDay(RunRow(holiday, null, basis));
    }

    [Theory]
    [InlineData(HolidayTimeBasis.BasedOnTimeInDayType)]
    [InlineData(HolidayTimeBasis.BasedOnActualWorkHours)]
    public void NationwideSpecial_AppliesToEveryone(HolidayTimeBasis basis)
    {
        ShouldBeTheSpecialHoliday(RunRow(Holiday(HolidayType.SPECIAL, HolidayDate), Manila, basis));
        ShouldBeTheSpecialHoliday(RunRow(Holiday(HolidayType.SPECIAL, HolidayDate), null, basis));
    }

    [Theory]
    [InlineData(HolidayTimeBasis.BasedOnTimeInDayType)]
    [InlineData(HolidayTimeBasis.BasedOnActualWorkHours)]
    public void Legal_IgnoresAnySiteList_AndAppliesToEveryone(HolidayTimeBasis basis)
    {
        var holiday = Holiday(HolidayType.LEGAL, HolidayDate, areaIds: [Cebu]);

        var row = RunRow(holiday, Manila, basis);

        row.LegalHoliday.TotalMinutes.Should().Be(540);
        row.Regular.IsEmpty().Should().BeTrue();
        row.SpecialHoliday.IsEmpty().Should().BeTrue();
    }
}
