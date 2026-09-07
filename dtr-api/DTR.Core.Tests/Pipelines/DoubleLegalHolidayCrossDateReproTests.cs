using DTR.Core.Tests.TestSupport;

namespace DTR.Core.Tests.Pipelines;

/// <summary>
/// "Double Holiday" in this codebase means TWO distinct Legal-holiday records stacked on the
/// SAME calendar date (HolidayPlus8TimePipeline's HolidayCount metadata > 1 -- see
/// CommonExtensions.IsDoubleHoliday/IsDoubleLegalHoliday), not two different holiday types on
/// two different dates (that combination is already covered by DtrRowIntegrationTests /
/// DisplayColumnDoubleHolidayIntegrationTests, which are misleadingly named but actually test
/// back-to-back single holidays). This file exercises the REAL double-legal-holiday path end to
/// end through DailyRecordBuilder, mirroring SpecialHolidayCrossDateReproTests:
///   - a same-day double-legal-holiday shift with no cross-date remainder
///   - the rest-day equivalent (RestDoubleLegal)
///   - a boundary-crossing shift where the double-legal-holiday date is the shift's anchor date
///     but the shift trails into a following plain (non-holiday) date -- checking both that the
///     Double Legal columns correctly claim only the holiday date's portion, and that the
///     genuine non-holiday remainder's night-diff still computes correctly (the same class of
///     bug RegularNightdiffRule had before this session's fix).
/// </summary>
public class DoubleLegalHolidayCrossDateReproTests : DtrTestBase
{
    private static readonly DateOnly HolidayDate = new(2026, 9, 4);

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

    [Fact]
    public void SameDayShift_TwoLegalHolidaysOnTheSameDate_WholeShiftLandsInDoubleLegalColumn()
    {
        // Both HolidayInfo entries land on the same PayrollDate -> HolidayPlus8TimePipeline's
        // HolidayCount metadata = 2 -> Plus8.IsDoubleHoliday() = true. Shift doesn't cross
        // midnight, so there's no non-holiday remainder to worry about here -- just confirming
        // the plain LegalHoliday column steps aside for DoubleLegal.
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId,
            Holiday(HolidayType.LEGAL, HolidayDate),
            Holiday(HolidayType.LEGAL, HolidayDate));
        var shiftStart = new DateTime(2026, 9, 4, 8, 0, 0);
        var shiftEnd = new DateTime(2026, 9, 4, 17, 0, 0); // 9h, entirely daytime -> no ND involved
        var context = CreateContext(shiftStart, shiftEnd, HolidayTimeBasis.BasedOnActualWorkHours, holidays, employeeId, maxWorkingMinutes: 480);
        context.Payload.Data.CurrentShift.ShiftDate = HolidayDate;
        context.Payload.Data.CurrentDate = HolidayDate;
        context.CanonicalTimeRange = Range(shiftStart, shiftEnd);

        var record = RunFullRow(context);

        record.LegalHolHours.Should().Be(0); // stepped aside for DoubleLegal
        record.RegularNetHours.Should().Be(0);
        record.RegularNDHours.Should().Be(0);
        (record.DoubleLegalHours + record.DoubleLegalNDHours).Should().BeApproximately(8.0, 0.01);
    }

    [Fact]
    public void SameDayShift_TwoLegalHolidaysOnTheSameDate_RestDay_WholeShiftLandsInRestDoubleLegalColumn()
    {
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId,
            Holiday(HolidayType.LEGAL, HolidayDate),
            Holiday(HolidayType.LEGAL, HolidayDate));
        var shiftStart = new DateTime(2026, 9, 4, 8, 0, 0);
        var shiftEnd = new DateTime(2026, 9, 4, 17, 0, 0);
        var context = CreateContext(shiftStart, shiftEnd, HolidayTimeBasis.BasedOnActualWorkHours, holidays, employeeId, maxWorkingMinutes: 480);
        context.Payload.Data.CurrentShift.ShiftDate = HolidayDate;
        context.Payload.Data.CurrentDate = HolidayDate;
        context.CanonicalTimeRange = Range(shiftStart, shiftEnd);
        MarkAsRestDay(context);

        var record = RunFullRow(context);

        record.RestDayHours.Should().Be(0);
        record.RestLegalDayHours.Should().Be(0); // stepped aside for RestDoubleLegal
        (record.RestDoubleLegalHours + record.RestDoubleLegalNDHours).Should().BeApproximately(8.0, 0.01);
    }

    [Fact]
    public void ActualWorkHours_CrossDateShift_DoubleLegalHolidayOnAnchorDate_NonHolidayRemainderKeepsItsOwnCorrectND()
    {
        // Same 19:00 Sept4 -> 04:00 Sept5 shape as the production ND bug repro
        // (SpecialHolidayCrossDateReproTests), but Sept4 carries a double legal holiday instead
        // of a single special holiday. The non-holiday remainder that spills onto Sept5
        // (00:00-04:00, entirely inside the 22:00-06:00 night-diff window) must still show up
        // correctly under Regular -- specifically under RegularNDHours, not silently zeroed --
        // exactly the same regression RegularNightdiffRule's fix guards against, just reached
        // through the double-holiday path instead of the single-Special path this time.
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId,
            Holiday(HolidayType.LEGAL, HolidayDate),
            Holiday(HolidayType.LEGAL, HolidayDate));
        var shiftStart = new DateTime(2026, 9, 4, 19, 0, 0);
        var shiftEnd = new DateTime(2026, 9, 5, 4, 0, 0);
        var context = CreateContext(shiftStart, shiftEnd, HolidayTimeBasis.BasedOnActualWorkHours, holidays, employeeId, maxWorkingMinutes: 480);
        context.Payload.Data.CurrentShift.ShiftDate = HolidayDate;
        context.Payload.Data.CurrentDate = HolidayDate;
        context.CanonicalTimeRange = Range(shiftStart, shiftEnd); // punched exactly on time

        var record = RunFullRow(context);

        record.LegalHolHours.Should().Be(0); // stepped aside for DoubleLegal throughout
        record.RegularNDHours.Should().BeGreaterThan(0); // the regression this test guards against
        (record.RegularNetHours + record.RegularNDHours + record.DoubleLegalHours + record.DoubleLegalNDHours)
            .Should().BeApproximately(8.0, 0.01); // the full 480-min capped shift, nothing lost or doubled
    }

    [Fact]
    public void ActualWorkHours_CrossDateShift_DoubleLegalHolidayOnAnchorDate_RestDay_NonHolidayRemainderKeepsItsOwnCorrectND()
    {
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId,
            Holiday(HolidayType.LEGAL, HolidayDate),
            Holiday(HolidayType.LEGAL, HolidayDate));
        var shiftStart = new DateTime(2026, 9, 4, 19, 0, 0);
        var shiftEnd = new DateTime(2026, 9, 5, 4, 0, 0);
        var context = CreateContext(shiftStart, shiftEnd, HolidayTimeBasis.BasedOnActualWorkHours, holidays, employeeId, maxWorkingMinutes: 480);
        context.Payload.Data.CurrentShift.ShiftDate = HolidayDate;
        context.Payload.Data.CurrentDate = HolidayDate;
        context.CanonicalTimeRange = Range(shiftStart, shiftEnd);
        MarkAsRestDay(context);

        var record = RunFullRow(context);

        record.RestLegalDayHours.Should().Be(0); // stepped aside for RestDoubleLegal
        record.RestDayNDHours.Should().BeGreaterThan(0); // the Rest counterpart of the same regression
        (record.RestDayHours + record.RestDayNDHours + record.RestDoubleLegalHours + record.RestDoubleLegalNDHours)
            .Should().BeApproximately(8.0, 0.01);
    }

    // --- Double holiday on the FIRST portion, single/no holiday on the REMAINING portion ------
    //
    // Plus8.IsDoubleHoliday() is a single shift-level flag: HolidayPlus8TimePipeline reads
    // GetHolidayDuringDate(LEGAL, employee, CurrentShift.ShiftDate) -- the shift's ANCHOR date
    // (Sept4) only. It says nothing about Sept5. So which column ultimately claims Sept5's
    // minutes depends entirely on what TYPE of holiday (if any) actually sits on Sept5, not on
    // the double-holiday flag itself -- these tests pin down each of the three shapes.

    private static readonly DateOnly NextDay = HolidayDate.AddDays(1); // Sept5

    [Fact]
    public void ActualWorkHours_DoubleLegalOnDay1_SingleLegalOnDay2_SameTypeCombinesUnderDoubleLegal()
    {
        // Day2's single Legal holiday is the SAME type as Day1's double -- HolidayDutyTimePipeline
        // doesn't split same-type portions by date (see the Special+Special repro earlier in this
        // file's sibling), so PipeLineResult.LegalHoliday already holds BOTH days' portions
        // combined. Since Plus8.IsDoubleHoliday() is true (driven by Day1 alone), the plain
        // LegalHoliday column steps aside for the WHOLE combined range, not just Day1's share --
        // Day2's single-legal minutes ride along under DoubleLegal too. Documenting this as the
        // actual (architecturally consistent, not per-date-aware) behavior rather than assuming
        // Day2 should split out into plain LegalHol.
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId,
            Holiday(HolidayType.LEGAL, HolidayDate),
            Holiday(HolidayType.LEGAL, HolidayDate),
            Holiday(HolidayType.LEGAL, NextDay));
        var shiftStart = new DateTime(2026, 9, 4, 19, 0, 0);
        var shiftEnd = new DateTime(2026, 9, 5, 4, 0, 0);
        var context = CreateContext(shiftStart, shiftEnd, HolidayTimeBasis.BasedOnActualWorkHours, holidays, employeeId, maxWorkingMinutes: 480);
        context.Payload.Data.CurrentShift.ShiftDate = HolidayDate;
        context.Payload.Data.CurrentDate = HolidayDate;
        context.CanonicalTimeRange = Range(shiftStart, shiftEnd);

        var record = RunFullRow(context);

        record.LegalHolHours.Should().Be(0); // stepped aside for DoubleLegal, both days included
        record.RegularNetHours.Should().Be(0);
        record.RegularNDHours.Should().Be(0);
        (record.DoubleLegalHours + record.DoubleLegalNDHours).Should().BeApproximately(8.0, 0.01); // both days combined
    }

    [Fact]
    public void ActualWorkHours_DoubleLegalOnDay1_SingleSpecialOnDay2_SplitsAcrossDoubleLegalAndSpecialHoliday()
    {
        // Day2's holiday is a DIFFERENT type (Special) -- HolidayDutyTimePipeline keeps LEGAL and
        // SPECIAL portions in separate ledger claims, so PipeLineResult.LegalHoliday holds only
        // Day1's portion (DoubleLegal's source range) and PipeLineResult.SpecialHoliday holds
        // only Day2's. SpecialHolidayEvaluator's "also a legal holiday" guard checks
        // IsLegalHoliday() (true here, since Day1/ShiftDate is Legal) but that's an unconditional
        // exclusion only when specialHoliday.IsEmpty() -- it isn't here, so Day2's Special portion
        // still comes through untouched by Day1's double-legal status.
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId,
            Holiday(HolidayType.LEGAL, HolidayDate),
            Holiday(HolidayType.LEGAL, HolidayDate),
            Holiday(HolidayType.SPECIAL, NextDay, HolidayWorkType.NonWorking));
        var shiftStart = new DateTime(2026, 9, 4, 19, 0, 0);
        var shiftEnd = new DateTime(2026, 9, 5, 4, 0, 0);
        var context = CreateContext(shiftStart, shiftEnd, HolidayTimeBasis.BasedOnActualWorkHours, holidays, employeeId, maxWorkingMinutes: 480);
        context.Payload.Data.CurrentShift.ShiftDate = HolidayDate;
        context.Payload.Data.CurrentDate = HolidayDate;
        context.CanonicalTimeRange = Range(shiftStart, shiftEnd);

        var record = RunFullRow(context);

        record.LegalHolHours.Should().Be(0); // stepped aside for DoubleLegal
        record.RegularNetHours.Should().Be(0);
        record.RegularNDHours.Should().Be(0);
        (record.DoubleLegalHours + record.DoubleLegalNDHours).Should().BeGreaterThan(0); // Day1's portion
        (record.SpecialHolHours + record.SpecialHolNightDiffHours).Should().BeGreaterThan(0); // Day2's portion
        (record.DoubleLegalHours + record.DoubleLegalNDHours + record.SpecialHolHours + record.SpecialHolNightDiffHours)
            .Should().BeApproximately(8.0, 0.01); // the full capped shift, split correctly by date/type
    }

    [Fact]
    public void ActualWorkHours_DoubleLegalOnDay1_SingleSpecialOnDay2_RestDay_SplitsAcrossRestDoubleLegalAndRestSpecial()
    {
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId,
            Holiday(HolidayType.LEGAL, HolidayDate),
            Holiday(HolidayType.LEGAL, HolidayDate),
            Holiday(HolidayType.SPECIAL, NextDay, HolidayWorkType.NonWorking));
        var shiftStart = new DateTime(2026, 9, 4, 19, 0, 0);
        var shiftEnd = new DateTime(2026, 9, 5, 4, 0, 0);
        var context = CreateContext(shiftStart, shiftEnd, HolidayTimeBasis.BasedOnActualWorkHours, holidays, employeeId, maxWorkingMinutes: 480);
        context.Payload.Data.CurrentShift.ShiftDate = HolidayDate;
        context.Payload.Data.CurrentDate = HolidayDate;
        context.CanonicalTimeRange = Range(shiftStart, shiftEnd);
        MarkAsRestDay(context);

        var record = RunFullRow(context);

        record.RestLegalDayHours.Should().Be(0); // stepped aside for RestDoubleLegal
        record.RestDayHours.Should().Be(0);
        (record.RestDoubleLegalHours + record.RestDoubleLegalNDHours).Should().BeGreaterThan(0); // Day1's portion
        (record.RestSpecialDayHours + record.RestSpecialDayNDHours).Should().BeGreaterThan(0); // Day2's portion
        (record.RestDoubleLegalHours + record.RestDoubleLegalNDHours + record.RestSpecialDayHours + record.RestSpecialDayNDHours)
            .Should().BeApproximately(8.0, 0.01);
    }

    [Fact]
    public void ActualWorkHours_DoubleLegalOnDay1_NoHolidayOnDay2_RegularRemainderIsNotDoubleCounted()
    {
        // The explicit "single OR no holiday" cross-date shape's "no holiday" arm, restated here
        // alongside its single-holiday siblings for a direct side-by-side (the ND-focused version
        // of this same shape already exists above as
        // ActualWorkHours_CrossDateShift_DoubleLegalHolidayOnAnchorDate_NonHolidayRemainderKeepsItsOwnCorrectND).
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId,
            Holiday(HolidayType.LEGAL, HolidayDate),
            Holiday(HolidayType.LEGAL, HolidayDate));
        var shiftStart = new DateTime(2026, 9, 4, 19, 0, 0);
        var shiftEnd = new DateTime(2026, 9, 5, 4, 0, 0);
        var context = CreateContext(shiftStart, shiftEnd, HolidayTimeBasis.BasedOnActualWorkHours, holidays, employeeId, maxWorkingMinutes: 480);
        context.Payload.Data.CurrentShift.ShiftDate = HolidayDate;
        context.Payload.Data.CurrentDate = HolidayDate;
        context.CanonicalTimeRange = Range(shiftStart, shiftEnd);

        var record = RunFullRow(context);

        (record.DoubleLegalHours + record.DoubleLegalNDHours).Should().BeGreaterThan(0); // Day1's portion
        (record.RegularNetHours + record.RegularNDHours).Should().BeGreaterThan(0); // Day2's genuine non-holiday remainder
        (record.DoubleLegalHours + record.DoubleLegalNDHours + record.RegularNetHours + record.RegularNDHours)
            .Should().BeApproximately(8.0, 0.01);
    }

    // --- The REVERSE shape: no/single holiday on the FIRST portion, double on the REMAINING ---
    //
    // Plus8.IsDoubleHoliday() only ever checks GetHolidayDuringDate(LEGAL, employee,
    // CurrentShift.ShiftDate) -- the shift's ANCHOR date. When Day1 (the anchor) has no legal
    // holiday of its own, Plus8 never even looks at Day2, so it reports IsDoubleHoliday()=false
    // for the whole row regardless of how many legal-holiday records actually sit on Day2.
    // That does NOT lose Day2's minutes, though: evaluated.LegalHoliday/NightDiff.Legal are
    // computed via IsHolidaySpec's ActualWorkEvaluator, which checks
    // GetHolidayDuringShift(..., CurrentShift) -- both the shift's start AND end dates -- so
    // Day2's real legal-holiday minutes still land correctly under the PLAIN LegalHol/RestLegal
    // columns. The net effect: a double-legal-holiday date that isn't the DTR row's anchor date
    // gets paid at plain single-Legal-Holiday rate, not Double-Legal rate, purely because of
    // where Plus8 looks. Documented here as the actual (anchor-scoped, not a lost-minutes bug)
    // behavior, verified by execution rather than assumed.

    [Fact]
    public void ActualWorkHours_NoHolidayOnDay1_DoubleLegalOnDay2_MinutesLandInPlainLegalHol_NotDoubleLegal()
    {
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId,
            Holiday(HolidayType.LEGAL, NextDay),
            Holiday(HolidayType.LEGAL, NextDay));
        var shiftStart = new DateTime(2026, 9, 4, 19, 0, 0);
        var shiftEnd = new DateTime(2026, 9, 5, 4, 0, 0);
        var context = CreateContext(shiftStart, shiftEnd, HolidayTimeBasis.BasedOnActualWorkHours, holidays, employeeId, maxWorkingMinutes: 480);
        context.Payload.Data.CurrentShift.ShiftDate = HolidayDate; // anchor = Day1 (no holiday)
        context.Payload.Data.CurrentDate = HolidayDate;
        context.CanonicalTimeRange = Range(shiftStart, shiftEnd);

        var record = RunFullRow(context);

        (record.DoubleLegalHours + record.DoubleLegalNDHours).Should().Be(0); // double status not detected off-anchor
        (record.RegularNetHours + record.RegularNDHours).Should().BeGreaterThan(0); // Day1's genuine non-holiday portion
        (record.LegalHolHours + record.LegalHolNightDiffHours).Should().BeGreaterThan(0); // Day2's minutes, NOT lost
        (record.RegularNetHours + record.RegularNDHours + record.LegalHolHours + record.LegalHolNightDiffHours)
            .Should().BeApproximately(8.0, 0.01);
    }

    [Fact]
    public void ActualWorkHours_NoHolidayOnDay1_DoubleLegalOnDay2_RestDay_MinutesLandInRestLegal_NotRestDoubleLegal()
    {
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId,
            Holiday(HolidayType.LEGAL, NextDay),
            Holiday(HolidayType.LEGAL, NextDay));
        var shiftStart = new DateTime(2026, 9, 4, 19, 0, 0);
        var shiftEnd = new DateTime(2026, 9, 5, 4, 0, 0);
        var context = CreateContext(shiftStart, shiftEnd, HolidayTimeBasis.BasedOnActualWorkHours, holidays, employeeId, maxWorkingMinutes: 480);
        context.Payload.Data.CurrentShift.ShiftDate = HolidayDate;
        context.Payload.Data.CurrentDate = HolidayDate;
        context.CanonicalTimeRange = Range(shiftStart, shiftEnd);
        MarkAsRestDay(context);

        var record = RunFullRow(context);

        (record.RestDoubleLegalHours + record.RestDoubleLegalNDHours).Should().Be(0);
        (record.RestDayHours + record.RestDayNDHours).Should().BeGreaterThan(0); // Day1's genuine rest-day portion
        (record.RestLegalDayHours + record.RestLegalDayNDHours).Should().BeGreaterThan(0); // Day2's minutes, NOT lost
        (record.RestDayHours + record.RestDayNDHours + record.RestLegalDayHours + record.RestLegalDayNDHours)
            .Should().BeApproximately(8.0, 0.01);
    }

    [Fact]
    public void ActualWorkHours_SingleSpecialOnDay1_DoubleLegalOnDay2_SplitsAcrossSpecialHolidayAndPlainLegalHol()
    {
        // Day1's holiday is a different type (Special) than Day2's (Legal), so they stay in
        // separate pipeline claims -- SPHoliday correctly gets Day1's portion regardless of
        // Plus8, since Plus8/DoubleLegal is purely a Legal-holiday concept and never touches the
        // Special column either way.
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId,
            Holiday(HolidayType.SPECIAL, HolidayDate, HolidayWorkType.NonWorking),
            Holiday(HolidayType.LEGAL, NextDay),
            Holiday(HolidayType.LEGAL, NextDay));
        var shiftStart = new DateTime(2026, 9, 4, 19, 0, 0);
        var shiftEnd = new DateTime(2026, 9, 5, 4, 0, 0);
        var context = CreateContext(shiftStart, shiftEnd, HolidayTimeBasis.BasedOnActualWorkHours, holidays, employeeId, maxWorkingMinutes: 480);
        context.Payload.Data.CurrentShift.ShiftDate = HolidayDate;
        context.Payload.Data.CurrentDate = HolidayDate;
        context.CanonicalTimeRange = Range(shiftStart, shiftEnd);

        var record = RunFullRow(context);

        (record.DoubleLegalHours + record.DoubleLegalNDHours).Should().Be(0); // still not detected, anchor (Day1) has no Legal holiday at all
        (record.SpecialHolHours + record.SpecialHolNightDiffHours).Should().BeGreaterThan(0); // Day1's portion
        (record.LegalHolHours + record.LegalHolNightDiffHours).Should().BeGreaterThan(0); // Day2's portion, plain Legal not Double
        (record.SpecialHolHours + record.SpecialHolNightDiffHours + record.LegalHolHours + record.LegalHolNightDiffHours)
            .Should().BeApproximately(8.0, 0.01);
    }
}
