using DTR.Core.Tests.TestSupport;

namespace DTR.Core.Tests.Pipelines;

/// <summary>
/// Full-row integration coverage — mirrors CleanDTRDetailProcessor.MapResultsToDailyRecord's
/// exact production call sequence (dtr-api\DTR.Core\DTR\Processor\CleanDTRDetailProcessor.cs):
/// Regular via NonHolidayDutyTimePipeline, then LegalHoliday/SpecialHoliday via
/// HolidayDutyTimePipeline, all against the SAME TimeContext (so the ledger caching that
/// happens in production — WorkTimePipeline's "work_time" key, HolidayPolicy's
/// holiday_portion_{type}/non_holiday_portion — is exercised exactly as it really runs). No
/// ledger seeding, no shortcuts, real CurrentShift/HolidayProvider/CompanyPolicy config only.
/// This is the definitive proof that a boundary-crossing shift's hours land in the right DTR
/// columns under each HolidayTimeBasis — the original bug (and its fix, in
/// DTR.Core\DTR\DisplayRule\ColumnsViewRule\Evaluators\RestDayEvaluator.cs) lived one layer
/// above this in the display evaluators, but this proves the data those evaluators consume is
/// itself correct all the way from raw shift/attendance/holiday config.
/// </summary>
public class DtrRowIntegrationTests : DtrTestBase
{
    private static readonly DateOnly HolidayDate = new(2026, 1, 2);
    private static readonly DateTime ShiftStart = new(2026, 1, 1, 22, 0, 0);
    private static readonly DateTime ShiftEnd = new(2026, 1, 2, 6, 0, 0); // crosses midnight into the holiday

    private record RowResult(TimeRange Regular, TimeRange LegalHoliday, TimeRange SpecialHoliday);

    private static RowResult RunFullRow(TimeContext context)
    {
        var canonicalRange = context.CanonicalTimeRange;
        // Exact order CleanDTRDetailProcessor uses when building PipeLineResult.
        var regular = new NonHolidayDutyTimePipeline().Apply(context, canonicalRange);
        var legalHoliday = new HolidayDutyTimePipeline().Apply(context, HolidayType.LEGAL, canonicalRange);
        var specialHoliday = new HolidayDutyTimePipeline().Apply(context, HolidayType.SPECIAL, canonicalRange);
        return new RowResult(regular, legalHoliday, specialHoliday);
    }

    [Fact]
    public void BasedOnActualWorkHours_BoundaryCrossingShift_SplitsRegularAndLegalHolidayCorrectly()
    {
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId, Holiday(HolidayType.LEGAL, HolidayDate));
        var context = CreateContext(ShiftStart, ShiftEnd, HolidayTimeBasis.BasedOnActualWorkHours, holidays, employeeId);
        context.Payload.Data.CurrentShift.ShiftDate = HolidayDate;
        context.Payload.Data.CurrentDate = HolidayDate;

        var row = RunFullRow(context);

        row.Regular.TotalMinutes.Should().Be(120); // Jan1 22:00 - Jan2 00:00: the non-holiday remainder
        row.LegalHoliday.TotalMinutes.Should().Be(360); // Jan2 00:00 - 06:00: the actual holiday overlap
        row.SpecialHoliday.IsEmpty().Should().BeTrue();
        // Nothing lost or double-counted across the columns — sums to the full 8-hour shift.
        (row.Regular.TotalMinutes + row.LegalHoliday.TotalMinutes + row.SpecialHoliday.TotalMinutes)
            .Should().Be(480);
    }

    [Fact]
    public void BasedOnTimeInDayType_BoundaryCrossingShift_TreatsTheWholeShiftAsHoliday()
    {
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId, Holiday(HolidayType.LEGAL, HolidayDate));
        var context = CreateContext(ShiftStart, ShiftEnd, HolidayTimeBasis.BasedOnTimeInDayType, holidays, employeeId);
        context.Payload.Data.CurrentShift.ShiftDate = HolidayDate;
        context.Payload.Data.CurrentDate = HolidayDate;

        var row = RunFullRow(context);

        row.Regular.IsEmpty().Should().BeTrue(); // no split under this basis — by design
        row.LegalHoliday.TotalMinutes.Should().Be(480);
        row.SpecialHoliday.IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void NonHolidayShift_AllHoursLandInRegular_NoneInHoliday()
    {
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId, Holiday(HolidayType.LEGAL, HolidayDate)); // present, but never touched
        var context = CreateContext(
            new DateTime(2026, 1, 1, 8, 0, 0), new DateTime(2026, 1, 1, 17, 0, 0),
            HolidayTimeBasis.BasedOnActualWorkHours, holidays, employeeId);

        var row = RunFullRow(context);

        row.Regular.TotalMinutes.Should().Be(540);
        row.LegalHoliday.IsEmpty().Should().BeTrue();
        row.SpecialHoliday.IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void BasedOnActualWorkHours_ShiftEntirelyInsideTheHoliday_AllHoursLandInLegalHoliday()
    {
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId, Holiday(HolidayType.LEGAL, HolidayDate));
        var context = CreateContext(
            new DateTime(2026, 1, 2, 8, 0, 0), new DateTime(2026, 1, 2, 17, 0, 0),
            HolidayTimeBasis.BasedOnActualWorkHours, holidays, employeeId);

        var row = RunFullRow(context);

        row.Regular.IsEmpty().Should().BeTrue();
        row.LegalHoliday.TotalMinutes.Should().Be(540);
    }

    // --- Two succeeding holidays (back-to-back calendar days), BasedOnActualWorkHours --------
    //
    // A shift crossing from one holiday date straight into ANOTHER holiday date (rather than
    // from a plain day into a single holiday, as above) — the day1/day2 split must still land
    // in the right columns, in every Legal/Special combination, with nothing left over in
    // Regular since the whole shift is holiday-covered end to end.
    //
    // Day1 portion (22:00-00:00) = 120 min, Day2 portion (00:00-06:00) = 360 min, shift = 480 min.

    private static TimeContext BuildDoubleHolidayContext(HolidayInfo day1Holiday, HolidayInfo day2Holiday)
    {
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId, day1Holiday, day2Holiday);
        var context = CreateContext(ShiftStart, ShiftEnd, HolidayTimeBasis.BasedOnActualWorkHours, holidays, employeeId);
        context.Payload.Data.CurrentShift.ShiftDate = HolidayDate; // anchored to the shift's end date, same convention as the rest of this file
        context.Payload.Data.CurrentDate = HolidayDate;
        return context;
    }

    [Fact]
    public void BasedOnActualWorkHours_LegalThenSpecial_SplitsBetweenBothHolidayColumns()
    {
        var day1 = new DateOnly(2026, 1, 1);
        var context = BuildDoubleHolidayContext(
            Holiday(HolidayType.LEGAL, day1),
            Holiday(HolidayType.SPECIAL, HolidayDate));

        var row = RunFullRow(context);

        row.Regular.IsEmpty().Should().BeTrue(); // nothing left over -- both days are holidays
        row.LegalHoliday.TotalMinutes.Should().Be(120); // Jan1 22:00-00:00
        row.SpecialHoliday.TotalMinutes.Should().Be(360); // Jan2 00:00-06:00
        (row.Regular.TotalMinutes + row.LegalHoliday.TotalMinutes + row.SpecialHoliday.TotalMinutes)
            .Should().Be(480);
    }

    [Fact]
    public void BasedOnActualWorkHours_SpecialThenLegal_SplitsBetweenBothHolidayColumns()
    {
        var day1 = new DateOnly(2026, 1, 1);
        var context = BuildDoubleHolidayContext(
            Holiday(HolidayType.SPECIAL, day1),
            Holiday(HolidayType.LEGAL, HolidayDate));

        var row = RunFullRow(context);

        row.Regular.IsEmpty().Should().BeTrue();
        row.SpecialHoliday.TotalMinutes.Should().Be(120); // Jan1 22:00-00:00
        row.LegalHoliday.TotalMinutes.Should().Be(360); // Jan2 00:00-06:00
        (row.Regular.TotalMinutes + row.LegalHoliday.TotalMinutes + row.SpecialHoliday.TotalMinutes)
            .Should().Be(480);
    }

    [Fact]
    public void BasedOnActualWorkHours_SpecialThenSpecial_WholeShiftLandsInSpecialHoliday()
    {
        var day1 = new DateOnly(2026, 1, 1);
        var context = BuildDoubleHolidayContext(
            Holiday(HolidayType.SPECIAL, day1),
            Holiday(HolidayType.SPECIAL, HolidayDate));

        var row = RunFullRow(context);

        row.Regular.IsEmpty().Should().BeTrue();
        row.LegalHoliday.IsEmpty().Should().BeTrue();
        row.SpecialHoliday.TotalMinutes.Should().Be(480); // both days' portions combine under the same column
    }

    [Fact]
    public void BasedOnActualWorkHours_LegalThenLegal_WholeShiftLandsInLegalHoliday()
    {
        var day1 = new DateOnly(2026, 1, 1);
        var context = BuildDoubleHolidayContext(
            Holiday(HolidayType.LEGAL, day1),
            Holiday(HolidayType.LEGAL, HolidayDate));

        var row = RunFullRow(context);

        row.Regular.IsEmpty().Should().BeTrue();
        row.SpecialHoliday.IsEmpty().Should().BeTrue();
        row.LegalHoliday.TotalMinutes.Should().Be(480); // both days' portions combine under the same column
    }
}
