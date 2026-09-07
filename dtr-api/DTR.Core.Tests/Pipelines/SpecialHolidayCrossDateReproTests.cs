using DTR.Core.Tests.TestSupport;

namespace DTR.Core.Tests.Pipelines;

/// <summary>
/// Reproduces a real production report: a night shift starting on a Special (Non-Working)
/// holiday and ending the next (non-holiday) calendar day, with CurrentDate/ShiftDate anchored
/// to the shift's START date (2026-09-04, the holiday) rather than the END date — the opposite
/// anchor convention from every other test in this project, but the one the real DTR row used
/// (DTR DATE column showed 2026-09-04). Runs the exact CleanDTRDetailProcessor sequence
/// (all pipelines -> DisplayRule -> ComputeNightDiff -> DailyRecordBuilder.Build) to see the
/// actual DTRDetailModel fields the UI renders, not just the raw pipeline split. Originally
/// caught RegularDayEvaluator double-counting PipeLineResult.SpecialHoliday into RegWork (fixed
/// — see RegularDayEvaluator.Evaluate); these tests now guard against that regressing across
/// both HolidayTimeBasis settings and both the plain-workday and rest-day work types.
/// </summary>
public class SpecialHolidayCrossDateReproTests : DtrTestBase
{
    private static readonly DateOnly HolidayDate = new(2026, 9, 4); // Special Non-Working
    private static readonly DateTime ShiftStart = new(2026, 9, 4, 19, 0, 0);
    private static readonly DateTime ShiftEnd = new(2026, 9, 5, 4, 0, 0); // scheduled 9h span
    private static readonly DateTime ActualStart = new(2026, 9, 4, 19, 12, 0); // 12 min late
    private static readonly DateTime ActualEnd = new(2026, 9, 5, 5, 0, 0); // 1h past scheduled end

    private static DTRDetailModel RunFullRow(TimeContext context)
    {
        var canonicalRange = context.CanonicalTimeRange;
        var pipelineResult = new PipeLineResult
        {
            Regular = new NonHolidayDutyTimePipeline().Apply(context, canonicalRange),
            LegalHoliday = new HolidayDutyTimePipeline().Apply(context, HolidayType.LEGAL, canonicalRange),
            SpecialHoliday = new HolidayDutyTimePipeline().Apply(context, HolidayType.SPECIAL, canonicalRange),
            Plus8 = new HolidayPlus8TimePipeline().Apply(context, canonicalRange),
            OT = new OverTimePipeline().Apply(context, canonicalRange),
            Late = new LateTimePipeline().Apply(context, canonicalRange),
            UT = new UndertimeTimePipeline().Apply(context, canonicalRange),
            Overbreak = new OverbreaktimePipeline().Apply(context, canonicalRange),
            Leave = new LeaveTimePipeline().Apply(context, canonicalRange),
            Travel = new TravelPipeline().Apply(context, canonicalRange),
        };

        var displayContext = new DisplayContext { TimeContext = context, PipeLineResult = pipelineResult };
        var displayProcessor = new DTRDetailColumnDisplayProcessor();
        var evaluated = displayProcessor.DisplayRule(displayContext);
        var nightDiff = displayProcessor.ComputeNightDiff(evaluated, displayContext);
        var workType = new WorkTypeResolver().Resolve(context);
        return new DailyRecordBuilder().Build(context, pipelineResult, evaluated, nightDiff, workType);
    }

    private static TimeContext BuildContext(HolidayTimeBasis basis, bool markRestDay)
    {
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId, Holiday(HolidayType.SPECIAL, HolidayDate, HolidayWorkType.NonWorking));
        var context = CreateContext(ShiftStart, ShiftEnd, basis, holidays, employeeId, maxWorkingMinutes: 480);
        // Anchor to the START date, matching the real DTR row (DTR DATE = 2026-09-04).
        context.Payload.Data.CurrentShift.ShiftDate = HolidayDate;
        context.Payload.Data.CurrentDate = HolidayDate;
        context.CanonicalTimeRange = Range(ActualStart, ActualEnd); // 12 min late in, 1h past scheduled end
        if (markRestDay) MarkAsRestDay(context);
        return context;
    }

    private static TimeContext BuildContextBothDaysSpecial(bool markRestDay)
    {
        var employeeId = NewEmployeeId();
        var nextDay = HolidayDate.AddDays(1);
        var holidays = Holidays(
            employeeId,
            Holiday(HolidayType.SPECIAL, HolidayDate, HolidayWorkType.NonWorking),
            Holiday(HolidayType.SPECIAL, nextDay, HolidayWorkType.NonWorking));
        var context = CreateContext(ShiftStart, ShiftEnd, HolidayTimeBasis.BasedOnActualWorkHours, holidays, employeeId, maxWorkingMinutes: 480);
        context.Payload.Data.CurrentShift.ShiftDate = HolidayDate;
        context.Payload.Data.CurrentDate = HolidayDate;
        context.CanonicalTimeRange = Range(ActualStart, ActualEnd);
        if (markRestDay) MarkAsRestDay(context);
        return context;
    }

    [Fact]
    public void ActualWorkHours_BothSept4AndSept5AreSpecialNonWorking_WholeClaimedShiftLandsInSpecial()
    {
        // IsSpecialNonWorking() == true covering BOTH calendar dates the shift touches (not
        // just one of them, like every other test in this file). Since both segments are the
        // SAME holiday type, HolidayDutyTimePipeline(SPECIAL) combines them into one
        // continuous holiday_portion_SPECIAL claim spanning both dates -- there's no leftover
        // "remaining hour" to route to Regular/RestWork/anywhere else, because there's no
        // non-holiday date left in the shift at all. The two segments (Sept4's 19:12-24:00
        // portion + Sept5's 00:00-03:12 portion, up to the 480-min work_time cap) sum to
        // exactly the shift's total claimed work time, all under Special.
        var context = BuildContextBothDaysSpecial(markRestDay: false);

        var record = RunFullRow(context);

        record.RegularNetHours.Should().Be(0);
        record.RestDayHours.Should().Be(0);
        (record.SpecialHolHours + record.SpecialHolNightDiffHours).Should().BeApproximately(8.0, 0.01); // full 480 min
    }

    [Fact]
    public void ActualWorkHours_BothDaysSpecialNonWorking_RestDay_WholeClaimedShiftLandsInRestSpecial()
    {
        var context = BuildContextBothDaysSpecial(markRestDay: true);

        var record = RunFullRow(context);

        record.RestDayHours.Should().Be(0);
        record.RegularNetHours.Should().Be(0);
        (record.RestSpecialDayHours + record.RestSpecialDayNDHours).Should().BeApproximately(8.0, 0.01);
    }

    [Fact]
    public void ActualWorkHours_PlainWorkday_PostShiftOTFallsEntirelyOnTheNonHolidayDay()
    {
        // Same shift, but with post-shift OT enabled so the trailing 108 minutes past the
        // 480-min work_time cap (03:12-05:00 on Sept5, entirely non-holiday territory) becomes
        // real OT instead of just being dropped. Checks that RegOT correctly claims all of it
        // and SpecialOT/LegalOT correctly claim none of it -- the OT-column counterpart to the
        // HRS-column split already covered above, going through HolidayOTFactory instead of
        // HolidayPlusRegularRule/RegularDayEvaluator. That 108-min OT window (03:12-05:00) also
        // falls entirely within the 22:00-06:00 night-diff band, so it shows up under
        // RegularNDOTHours rather than RegularOTHours -- the same HRS-vs-ND split the real DTR
        // row's OT/ND-OT columns showed (OT blank, ND-OT populated).
        var context = BuildContext(HolidayTimeBasis.BasedOnActualWorkHours, markRestDay: false);
        context.Payload.Data.CompanyPolicy.OTInclusionPolicy = OvertimeInclusionPolicy.UsePostShiftWork;
        context.Payload.Data.CurrentShift.OTRequireTimeIn = false; // simpler FixedOT start-time path

        var record = RunFullRow(context);

        record.RegularOTHours.Should().Be(0);
        record.RegularNDOTHours.Should().BeApproximately(1.8, 0.01); // 108 min, all on Sept5, all in night-diff hours
        record.SpecialHolOTHours.Should().Be(0);
        record.SpecialHolNightDiffOTHours.Should().Be(0);
        (record.RegularOTHours + record.RegularNDOTHours + record.SpecialHolOTHours + record.SpecialHolNightDiffOTHours)
            .Should().BeApproximately(1.8, 0.01); // the full 108-min overflow, nothing lost or doubled
    }

    [Fact]
    public void ActualWorkHours_PlainWorkday_PartitionsRegularAndSpecialCorrectly()
    {
        var context = BuildContext(HolidayTimeBasis.BasedOnActualWorkHours, markRestDay: false);

        var record = RunFullRow(context);

        // 480 min claimed work time split Sept4-holiday (288) / Sept5-non-holiday (192). The
        // non-holiday remainder (00:00-03:12 Sept5) falls entirely inside the 22:00-06:00
        // night-diff window, so it all lands under RegularNDHours, not RegularNetHours.
        record.RegularNetHours.Should().Be(0);
        record.RegularNDHours.Should().BeApproximately(3.2, 0.01); // 192 min, all night-diff
        (record.SpecialHolHours + record.SpecialHolNightDiffHours).Should().BeApproximately(4.8, 0.01); // 288 min
        (record.RegularNetHours + record.RegularNDHours + record.SpecialHolHours + record.SpecialHolNightDiffHours)
            .Should().BeApproximately(8.0, 0.01); // the full 480 min, nothing lost or doubled
        record.RestDayHours.Should().Be(0);
        record.RestSpecialDayHours.Should().Be(0);
    }

    [Fact]
    public void ActualWorkHours_RestDay_PartitionsRestWorkAndRestSpecialCorrectly()
    {
        // Same shift, but the employee's weekly rest day lands on this anchor date (Sept4) --
        // work type routes through RestWork/RestSpecial instead of RegWork/SPHoliday, but the
        // underlying 288/192 split must still hold.
        var context = BuildContext(HolidayTimeBasis.BasedOnActualWorkHours, markRestDay: true);

        var record = RunFullRow(context);

        record.RegularNetHours.Should().Be(0);
        record.SpecialHolHours.Should().Be(0);
        // Same 00:00-03:12 Sept5 remainder, entirely inside the night-diff window -> all under
        // RestDayNDHours, none under RestDayHours.
        record.RestDayHours.Should().Be(0);
        record.RestDayNDHours.Should().BeApproximately(3.2, 0.01); // 192 min, all night-diff
        (record.RestSpecialDayHours + record.RestSpecialDayNDHours).Should().BeApproximately(4.8, 0.01); // 288 min
        (record.RestDayHours + record.RestDayNDHours + record.RestSpecialDayHours + record.RestSpecialDayNDHours)
            .Should().BeApproximately(8.0, 0.01);
    }

    [Fact]
    public void TimeInDayType_PlainWorkday_TreatsTheWholeShiftAsHoliday()
    {
        // "Full shift treated as holiday based on shift start day" -- ShiftDate (Sept4, the
        // holiday) alone decides the whole shift's classification; there is no split by design
        // under this basis (TimeInDayTypeProvider always zeroes non_holiday_portion).
        var context = BuildContext(HolidayTimeBasis.BasedOnTimeInDayType, markRestDay: false);

        var record = RunFullRow(context);

        record.RegularNetHours.Should().Be(0);
        (record.SpecialHolHours + record.SpecialHolNightDiffHours).Should().BeApproximately(8.0, 0.01); // the full 480 min
    }

    [Fact]
    public void TimeInDayType_RestDay_TreatsTheWholeShiftAsRestPlusSpecialHoliday()
    {
        var context = BuildContext(HolidayTimeBasis.BasedOnTimeInDayType, markRestDay: true);

        var record = RunFullRow(context);

        record.RestDayHours.Should().Be(0);
        (record.RestSpecialDayHours + record.RestSpecialDayNDHours).Should().BeApproximately(8.0, 0.01);
    }

    [Fact]
    public void ActualWorkHours_RegularNightDiff_IsNotZeroedByTheHolidayTouchingTheShift()
    {
        // Exact real-world repro: 19:00 Sept4 (Special Non-Working) -> 04:00 Sept5, punched
        // right on the shift's own scheduled times (no lateness/undertime to complicate the
        // split), 8h max working minutes. The non-holiday remainder that lands on Sept5
        // (00:00-04:00, entirely inside the 22:00-06:00 night-diff window) was incorrectly
        // showing RegularNDHours=0 in production, because RegularNightdiffRule re-zeroed its
        // whole result via the coarse "day touches a holiday" gate regardless of how much of
        // RegWork actually fell in night hours. Fixed in RegularNightdiffRule — this pins the
        // corrected behavior: the ND-window portion of the genuine non-holiday remainder must
        // show up under Regular's ND column, not vanish.
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId, Holiday(HolidayType.SPECIAL, HolidayDate, HolidayWorkType.NonWorking));
        var shiftStart = new DateTime(2026, 9, 4, 19, 0, 0);
        var shiftEnd = new DateTime(2026, 9, 5, 4, 0, 0);
        var context = CreateContext(shiftStart, shiftEnd, HolidayTimeBasis.BasedOnActualWorkHours, holidays, employeeId, maxWorkingMinutes: 480);
        context.Payload.Data.CurrentShift.ShiftDate = HolidayDate;
        context.Payload.Data.CurrentDate = HolidayDate;
        context.CanonicalTimeRange = Range(shiftStart, shiftEnd); // punched exactly on time, no late/OT

        var record = RunFullRow(context);

        // Whatever the exact HRS/ND split turns out to be, the non-holiday remainder's ND
        // portion must not be silently dropped: RegularNetHours + RegularNDHours must equal the
        // full non-holiday remainder, and RegularNDHours specifically must be > 0 since that
        // remainder falls entirely inside 22:00-06:00.
        record.RegularNDHours.Should().BeGreaterThan(0);
        (record.RegularNetHours + record.RegularNDHours + record.SpecialHolHours + record.SpecialHolNightDiffHours)
            .Should().BeApproximately(8.0, 0.01); // the full 480-min capped shift, nothing lost
    }
}
