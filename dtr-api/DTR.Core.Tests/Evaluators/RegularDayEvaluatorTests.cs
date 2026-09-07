using DTR.Core.DTR.DisplayRule.ColumnsViewRule.Evaluators;
using DTR.Core.Tests.TestSupport;

namespace DTR.Core.Tests.Evaluators;

/// <summary>
/// RegularDayEvaluator/RestDayEvaluator/RegularOverTimeEvaluator/RestOverTimeEvaluator — the
/// classes whose day-level IsLegalHoliday()/IsSpecialNonWorking() gate used to discard an
/// already-correctly-computed non-holiday remainder for a boundary-crossing shift under
/// BasedOnActualWorkHours. `range` here always plays the role of what
/// DTRDetailColumnDisplayProcessor.DisplayRule actually passes in production
/// (HolidayPlusRegularRule's/PipeLineResult.OT's output) — see HolidayPlusRegularRuleTests for
/// confirmation that regTimeRange really is pipeline.Regular in the normal (non-IsHolPlusReg)
/// case this fix targets.
/// </summary>
public class RegularDayEvaluatorTests : DtrTestBase
{
    private static readonly DateOnly HolidayDate = new(2026, 1, 2);
    private static readonly DateTime ShiftStart = new(2026, 1, 1, 22, 0, 0);
    private static readonly DateTime ShiftEnd = new(2026, 1, 2, 6, 0, 0);

    private static DisplayContext BuildCrossingShiftDisplayContext(
        HolidayTimeBasis basis, TimeRange regularRange, bool markRestDay = false)
    {
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId, Holiday(HolidayType.LEGAL, HolidayDate));
        var timeContext = CreateContext(ShiftStart, ShiftEnd, basis, holidays, employeeId);
        timeContext.Payload.Data.CurrentShift.ShiftDate = HolidayDate;
        timeContext.Payload.Data.CurrentDate = HolidayDate;
        if (markRestDay) MarkAsRestDay(timeContext);

        return CreateDisplayContext(timeContext, new PipeLineResult { Regular = regularRange });
    }

    // --- RegularDayEvaluator -------------------------------------------------------------

    [Fact]
    public void RegularDayEvaluator_NonHolidayDay_ReturnsRangeUnchanged()
    {
        var employeeId = NewEmployeeId();
        var timeContext = CreateContext(
            new DateTime(2026, 1, 1, 8, 0, 0), new DateTime(2026, 1, 1, 17, 0, 0),
            HolidayTimeBasis.BasedOnTimeInDayType, employeeId: employeeId);
        var range = timeContext.CanonicalTimeRange;
        var displayContext = CreateDisplayContext(timeContext, new PipeLineResult { Regular = range });

        var result = new RegularDayEvaluator().Evaluate(range, displayContext);

        result.TotalMinutes.Should().Be(540);
    }

    [Fact]
    public void RegularDayEvaluator_RestDay_ReturnsEmpty()
    {
        var range = Range(ShiftStart, ShiftEnd);
        var displayContext = BuildCrossingShiftDisplayContext(HolidayTimeBasis.BasedOnActualWorkHours, range, markRestDay: true);

        var result = new RegularDayEvaluator().Evaluate(range, displayContext);

        result.IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void RegularDayEvaluator_BasedOnTimeInDayType_BoundaryCrossingDay_StaysZero()
    {
        // TimeInDayTypeProvider always zeroes the non-holiday portion — pipeline.Regular is
        // genuinely Empty here, matching the pre-fix, still-correct behavior for this basis.
        var displayContext = BuildCrossingShiftDisplayContext(HolidayTimeBasis.BasedOnTimeInDayType, TimeRange.Empty);

        var result = new RegularDayEvaluator().Evaluate(TimeRange.Empty, displayContext);

        result.IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void RegularDayEvaluator_BasedOnActualWorkHours_BoundaryCrossingDay_KeepsTheNonHolidayRemainder()
    {
        // This is the fix: pipeline.Regular already holds the genuine 2-hour non-holiday
        // remainder (Jan1 22:00-Jan2 00:00) for this boundary-crossing shift — it must survive
        // instead of being zeroed out just because the day also touches the Jan2 holiday.
        var nonHolidayRemainder = Range(ShiftStart, new DateTime(2026, 1, 2, 0, 0, 0));
        var displayContext = BuildCrossingShiftDisplayContext(HolidayTimeBasis.BasedOnActualWorkHours, nonHolidayRemainder);

        var result = new RegularDayEvaluator().Evaluate(nonHolidayRemainder, displayContext);

        result.TotalMinutes.Should().Be(120);
    }

    // --- RestDayEvaluator ------------------------------------------------------------------

    [Fact]
    public void RestDayEvaluator_NotARestDay_ReturnsEmpty()
    {
        var range = Range(ShiftStart, ShiftEnd);
        var displayContext = BuildCrossingShiftDisplayContext(HolidayTimeBasis.BasedOnActualWorkHours, range, markRestDay: false);

        var result = new RestDayEvaluator().Evaluate(range, displayContext);

        result.IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void RestDayEvaluator_BasedOnActualWorkHours_BoundaryCrossingRestDay_KeepsTheNonHolidayRemainder()
    {
        var nonHolidayRemainder = Range(ShiftStart, new DateTime(2026, 1, 2, 0, 0, 0));
        var displayContext = BuildCrossingShiftDisplayContext(HolidayTimeBasis.BasedOnActualWorkHours, nonHolidayRemainder, markRestDay: true);

        var result = new RestDayEvaluator().Evaluate(nonHolidayRemainder, displayContext);

        result.TotalMinutes.Should().Be(120);
    }

    [Fact]
    public void RestDayEvaluator_BasedOnTimeInDayType_BoundaryCrossingRestDay_StaysZero()
    {
        var displayContext = BuildCrossingShiftDisplayContext(HolidayTimeBasis.BasedOnTimeInDayType, TimeRange.Empty, markRestDay: true);

        var result = new RestDayEvaluator().Evaluate(TimeRange.Empty, displayContext);

        result.IsEmpty().Should().BeTrue();
    }

    // --- RegularOverTimeEvaluator / RestOverTimeEvaluator -----------------------------------

    private static DisplayContext BuildCrossingOTDisplayContext(HolidayTimeBasis basis, TimeRange ot, bool markRestDay = false)
    {
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId, Holiday(HolidayType.LEGAL, HolidayDate));
        var timeContext = CreateContext(ShiftStart, ShiftEnd, basis, holidays, employeeId);
        timeContext.Payload.Data.CurrentShift.ShiftDate = HolidayDate;
        timeContext.Payload.Data.CurrentDate = HolidayDate;
        if (markRestDay) MarkAsRestDay(timeContext);

        return CreateDisplayContext(timeContext, new PipeLineResult { OT = ot });
    }

    [Fact]
    public void RegularOverTimeEvaluator_NonHolidayDay_ReturnsOTUnchanged()
    {
        var employeeId = NewEmployeeId();
        var timeContext = CreateContext(
            new DateTime(2026, 1, 1, 17, 0, 0), new DateTime(2026, 1, 1, 19, 0, 0),
            HolidayTimeBasis.BasedOnActualWorkHours, employeeId: employeeId);
        var ot = timeContext.CanonicalTimeRange;
        var displayContext = CreateDisplayContext(timeContext, new PipeLineResult { OT = ot });

        var result = new RegularOverTimeEvaluator().Evaluate(ot, displayContext);

        result.TotalMinutes.Should().Be(120);
    }

    [Fact]
    public void RegularOverTimeEvaluator_BasedOnTimeInDayType_BoundaryCrossingOT_StaysZero()
    {
        var ot = Range(ShiftStart, new DateTime(2026, 1, 2, 2, 0, 0)); // 4 hours, crosses midnight
        var displayContext = BuildCrossingOTDisplayContext(HolidayTimeBasis.BasedOnTimeInDayType, ot);

        var result = new RegularOverTimeEvaluator().Evaluate(ot, displayContext);

        result.IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void RegularOverTimeEvaluator_BasedOnActualWorkHours_BoundaryCrossingOT_KeepsTheNonHolidayRemainder()
    {
        var ot = Range(ShiftStart, new DateTime(2026, 1, 2, 2, 0, 0)); // 4 hours: 2 before midnight, 2 after
        var displayContext = BuildCrossingOTDisplayContext(HolidayTimeBasis.BasedOnActualWorkHours, ot);

        var result = new RegularOverTimeEvaluator().Evaluate(ot, displayContext);

        result.TotalMinutes.Should().Be(120); // the 2 hours before midnight, not the holiday's 2
    }

    [Fact]
    public void RestOverTimeEvaluator_BasedOnActualWorkHours_BoundaryCrossingRestOT_KeepsTheNonHolidayRemainder()
    {
        var ot = Range(ShiftStart, new DateTime(2026, 1, 2, 2, 0, 0));
        var displayContext = BuildCrossingOTDisplayContext(HolidayTimeBasis.BasedOnActualWorkHours, ot, markRestDay: true);

        var result = new RestOverTimeEvaluator().Evaluate(ot, displayContext);

        result.TotalMinutes.Should().Be(120);
    }

    [Fact]
    public void RestOverTimeEvaluator_NotARestDay_ReturnsEmpty()
    {
        var ot = Range(ShiftStart, new DateTime(2026, 1, 2, 2, 0, 0));
        var displayContext = BuildCrossingOTDisplayContext(HolidayTimeBasis.BasedOnActualWorkHours, ot, markRestDay: false);

        var result = new RestOverTimeEvaluator().Evaluate(ot, displayContext);

        result.IsEmpty().Should().BeTrue();
    }
}
