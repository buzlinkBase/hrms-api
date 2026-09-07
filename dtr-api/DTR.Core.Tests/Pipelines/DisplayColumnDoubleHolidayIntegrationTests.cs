using DTR.Core.Tests.TestSupport;

namespace DTR.Core.Tests.Pipelines;

/// <summary>
/// Extends DtrRowIntegrationTests one layer up — past the raw Regular/LegalHoliday/
/// SpecialHoliday pipeline split (already proven correct there) and into the actual DTR
/// DISPLAY columns (RegWork/RestWork/LegalHoliday/SPHoliday/RestLegal/RestSpecial), mirroring
/// CleanDTRDetailProcessor.MapResultsToDailyRecord's full sequence: Regular + Holiday(LEGAL) +
/// Holiday(SPECIAL) + Plus8 pipelines, then DTRDetailColumnDisplayProcessor.DisplayRule. Covers
/// rest-day combinations (RestLegal, RestSpecial), a Special-Working (paid, not premium) day,
/// and holiday/rest ordering across a boundary-crossing shift.
/// </summary>
public class DisplayColumnDoubleHolidayIntegrationTests : DtrTestBase
{
    private static readonly DateOnly Day1 = new(2026, 1, 1);
    private static readonly DateOnly Day2 = new(2026, 1, 2); // the shift's anchor date (ShiftDate/CurrentDate) in every scenario below
    private static readonly DateTime ShiftStart = new(2026, 1, 1, 22, 0, 0);
    private static readonly DateTime ShiftEnd = new(2026, 1, 2, 6, 0, 0); // 8h, crosses midnight: Day1 portion 120m, Day2 portion 360m

    private static EvaluatedColumnResult RunFullDisplayRow(TimeContext context)
    {
        var canonicalRange = context.CanonicalTimeRange;
        var pipelineResult = new PipeLineResult
        {
            Regular = new NonHolidayDutyTimePipeline().Apply(context, canonicalRange),
            LegalHoliday = new HolidayDutyTimePipeline().Apply(context, HolidayType.LEGAL, canonicalRange),
            SpecialHoliday = new HolidayDutyTimePipeline().Apply(context, HolidayType.SPECIAL, canonicalRange),
            Plus8 = new HolidayPlus8TimePipeline().Apply(context, canonicalRange),
        };
        var displayContext = new DisplayContext { TimeContext = context, PipeLineResult = pipelineResult };
        return new DTRDetailColumnDisplayProcessor().DisplayRule(displayContext);
    }

    private static TimeContext BuildContext(params HolidayInfo[] holidayInfos)
    {
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId, holidayInfos);
        var context = CreateContext(ShiftStart, ShiftEnd, HolidayTimeBasis.BasedOnActualWorkHours, holidays, employeeId);
        context.Payload.Data.CurrentShift.ShiftDate = Day2;
        context.Payload.Data.CurrentDate = Day2;
        return context;
    }

    [Fact]
    public void RestDayPlusLegalHoliday_SplitsAcrossRestWorkAndRestLegalColumns()
    {
        // Day1 = Legal Holiday (non-working), Day2 (the rest-day anchor) has no holiday of its own.
        var context = BuildContext(Holiday(HolidayType.LEGAL, Day1));
        MarkAsRestDay(context); // marks Day2 (CurrentDate) as the employee's rest day

        var row = RunFullDisplayRow(context);

        row.RegWork.IsEmpty().Should().BeTrue(); // rest day -> never Regular
        row.RestWork.TotalMinutes.Should().Be(360); // Day2's non-holiday remainder
        row.RestLegal.TotalMinutes.Should().Be(120); // Day1's legal-holiday portion
        row.LegalHoliday.IsEmpty().Should().BeTrue(); // routed to RestLegal instead, not double-counted
        row.SPHoliday.IsEmpty().Should().BeTrue();
        row.RestSpecial.IsEmpty().Should().BeTrue();
        (row.RestWork.TotalMinutes + row.RestLegal.TotalMinutes).Should().Be(480);
    }

    [Fact]
    public void RestDayPlusSpecialHoliday_OnTheAnchorDate_SplitsAcrossRestWorkAndRestSpecialColumns()
    {
        // The Special holiday sits on Day2 -- the same date CurrentDate is anchored to, so
        // IsSpecialNonWorking's CurrentDate-match requirement is satisfied. Day1 is plain.
        var context = BuildContext(Holiday(HolidayType.SPECIAL, Day2, HolidayWorkType.NonWorking));
        MarkAsRestDay(context);

        var row = RunFullDisplayRow(context);

        row.RegWork.IsEmpty().Should().BeTrue();
        row.RestWork.TotalMinutes.Should().Be(120); // Day1's non-holiday remainder
        row.RestSpecial.TotalMinutes.Should().Be(360); // Day2's special-holiday portion
        row.SPHoliday.IsEmpty().Should().BeTrue(); // routed to RestSpecial instead
        (row.RestWork.TotalMinutes + row.RestSpecial.TotalMinutes).Should().Be(480);
    }

    [Fact]
    public void RestDayPlusSpecialHoliday_OnTheNonAnchorDate_SplitsAcrossRestWorkAndRestSpecialColumns()
    {
        // Same shift as the previous test, but the Special holiday is on Day1 while CurrentDate
        // stays anchored to Day2 (this file's convention, matching every pre-existing test in
        // this project). GetCurrentSpecialHoliday already picks whichever date's HolidayInfo is
        // relevant under BasedOnActualWorkHours -- IsSpecialNonWorking no longer re-requires a
        // CurrentDate match on top of that, so a Special holiday on the shift's non-anchor date
        // is now correctly recognized (see CommonExtensions.IsSpecialNonWorking).
        var context = BuildContext(Holiday(HolidayType.SPECIAL, Day1, HolidayWorkType.NonWorking));
        MarkAsRestDay(context);

        var row = RunFullDisplayRow(context);

        row.RestSpecial.TotalMinutes.Should().Be(120); // Day1's special-holiday portion
        row.SPHoliday.IsEmpty().Should().BeTrue(); // rest day -> routed to RestSpecial, not SPHoliday
        row.RestWork.TotalMinutes.Should().Be(360); // Day2's own non-holiday remainder
        (row.RestWork.TotalMinutes + row.RestSpecial.TotalMinutes).Should().Be(480);
    }

    [Fact]
    public void LegalThenSpecial_NonRestDay_SplitsAcrossLegalHolidayAndSPHolidayColumns()
    {
        // Both days are holidays and neither is a rest day (the DtrRowIntegrationTests
        // equivalent, one level up). LegalHolidayEvaluator doesn't gate on IsSpecialNonWorking,
        // so the LegalHoliday column always correctly showed Day1's portion. SpecialHolidayEvaluator
        // used to unconditionally exclude any day that was ALSO a legal holiday at all (shift-wide),
        // zeroing Day2's genuine 360-minute Special-holiday portion even though it falls on a
        // different calendar date -- fixed to trust PipeLineResult.SpecialHoliday's own
        // emptiness instead, the same way RegularDayEvaluator/RestDayEvaluator already did.
        var context = BuildContext(
            Holiday(HolidayType.LEGAL, Day1),
            Holiday(HolidayType.SPECIAL, Day2, HolidayWorkType.NonWorking));

        var row = RunFullDisplayRow(context);

        row.RegWork.IsEmpty().Should().BeTrue();
        row.LegalHoliday.TotalMinutes.Should().Be(120); // Day1's portion
        row.SPHoliday.TotalMinutes.Should().Be(360); // Day2's portion
        (row.LegalHoliday.TotalMinutes + row.SPHoliday.TotalMinutes).Should().Be(480);
    }

    [Fact]
    public void SpecialWorkingHoliday_OnTheAnchorDate_RegularColumnPullsFromSpecialHolidayPipeline()
    {
        // Day2 (anchor) is a Special "Working" holiday -- HolidayPlusRegularRule substitutes
        // PipeLineResult.SpecialHoliday in for what RegularDayEvaluator receives as `range`
        // whenever IsSpecialWorking() is true. Day1 is plain, non-holiday.
        var context = BuildContext(Holiday(HolidayType.SPECIAL, Day2, HolidayWorkType.Working));

        var row = RunFullDisplayRow(context);

        row.RestWork.IsEmpty().Should().BeTrue(); // not a rest day
        row.RegWork.TotalMinutes.Should().Be(360); // Day2's special-working portion, routed through Regular
        row.SPHoliday.IsEmpty().Should().BeTrue(); // SpecialHolidayEvaluator requires IsSpecialNonWorking -- Working doesn't qualify
    }
}
