using DTR.Core.DTR.DisplayRule.ColumnsViewRule.Evaluators;
using DTR.Core.Tests.TestSupport;

namespace DTR.Core.Tests.Evaluators;

/// <summary>
/// The remaining DayType evaluators in RestDayEvaluator.cs not covered by
/// RegularDayEvaluatorTests — each is a simple day-classification gate over IsRestDay()/
/// IsLegalHoliday()/IsSpecialNonWorking()/Plus8.IsDoubleHoliday(), returning `range` unchanged
/// (or PipeLineResult.SpecialHoliday, for SpecialHolidayEvaluator, which ignores `range`
/// entirely) whenever the gate passes, Empty otherwise. Together these evaluators are meant to
/// be mutually exclusive per day (see DTRDetailColumnDisplayProcessor's doc comments) — the
/// "yields to" test names below describe that overall design, not any direct delegation
/// between the classes themselves.
/// </summary>
public class RemainingEvaluatorsTests : DtrTestBase
{
    private static readonly DateTime DayStart = new(2026, 1, 2, 8, 0, 0);
    private static readonly DateTime DayEnd = new(2026, 1, 2, 17, 0, 0);
    private static readonly DateOnly Day = new(2026, 1, 2);
    private static readonly TimeRange SomeRange = new(60);

    private static DisplayContext BuildContext(
        HolidayType? holidayType = null,
        bool isRestDay = false,
        bool isDoubleHoliday = false,
        TimeRange? specialHolidayPipelineValue = null)
    {
        var employeeId = NewEmployeeId();
        var holidays = holidayType.HasValue
            ? Holidays(employeeId, Holiday(holidayType.Value, Day))
            : null;
        var timeContext = CreateContext(DayStart, DayEnd, HolidayTimeBasis.BasedOnTimeInDayType, holidays, employeeId);
        timeContext.Payload.Data.CurrentShift.ShiftDate = Day;
        timeContext.Payload.Data.CurrentDate = Day;
        if (isRestDay) MarkAsRestDay(timeContext);

        var plus8 = new TimeRange(0);
        if (isDoubleHoliday) plus8.SetMetaData("HolidayCount", 2);

        return CreateDisplayContext(timeContext, new PipeLineResult
        {
            Plus8 = plus8,
            SpecialHoliday = specialHolidayPipelineValue ?? TimeRange.Empty,
        });
    }

    // --- RestLegalEvaluator / RestLegalHolidayOTEvaluator (identical gate) -----------------

    [Fact]
    public void RestLegalEvaluator_RestDayPlusLegalHoliday_NotDouble_ReturnsRange()
    {
        var context = BuildContext(HolidayType.LEGAL, isRestDay: true);
        new RestLegalEvaluator().Evaluate(SomeRange, context).Should().Be(SomeRange);
    }

    [Fact]
    public void RestLegalEvaluator_NotARestDay_ReturnsEmpty()
    {
        var context = BuildContext(HolidayType.LEGAL, isRestDay: false);
        new RestLegalEvaluator().Evaluate(SomeRange, context).IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void RestLegalEvaluator_RestDayPlusDoubleLegalHoliday_YieldsToDoubleEvaluator()
    {
        var context = BuildContext(HolidayType.LEGAL, isRestDay: true, isDoubleHoliday: true);
        new RestLegalEvaluator().Evaluate(SomeRange, context).IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void RestLegalHolidayOTEvaluator_SameGateAsRestLegalEvaluator()
    {
        var context = BuildContext(HolidayType.LEGAL, isRestDay: true);
        new RestLegalHolidayOTEvaluator().Evaluate(SomeRange, context).Should().Be(SomeRange);
    }

    // --- DoubleRestLegalEvaluator / DoubleRestLegalHolidayOTEvaluator ----------------------

    [Fact]
    public void DoubleRestLegalEvaluator_RestDayPlusDoubleHoliday_ReturnsRange()
    {
        var context = BuildContext(HolidayType.LEGAL, isRestDay: true, isDoubleHoliday: true);
        new DoubleRestLegalEvaluator().Evaluate(SomeRange, context).Should().Be(SomeRange);
    }

    [Fact]
    public void DoubleRestLegalEvaluator_NotDoubleHoliday_ReturnsEmpty()
    {
        var context = BuildContext(HolidayType.LEGAL, isRestDay: true, isDoubleHoliday: false);
        new DoubleRestLegalEvaluator().Evaluate(SomeRange, context).IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void DoubleRestLegalHolidayOTEvaluator_SameGate()
    {
        var context = BuildContext(HolidayType.LEGAL, isRestDay: true, isDoubleHoliday: true);
        new DoubleRestLegalHolidayOTEvaluator().Evaluate(SomeRange, context).Should().Be(SomeRange);
    }

    // --- RestLegalOTEvaluator (rest OR legal holiday) --------------------------------------

    [Fact]
    public void RestLegalOTEvaluator_RestDayOnly_ReturnsRange()
    {
        var context = BuildContext(isRestDay: true);
        new RestLegalOTEvaluator().Evaluate(SomeRange, context).Should().Be(SomeRange);
    }

    [Fact]
    public void RestLegalOTEvaluator_LegalHolidayOnly_ReturnsRange()
    {
        var context = BuildContext(HolidayType.LEGAL, isRestDay: false);
        new RestLegalOTEvaluator().Evaluate(SomeRange, context).Should().Be(SomeRange);
    }

    [Fact]
    public void RestLegalOTEvaluator_NeitherRestNorLegalHoliday_ReturnsEmpty()
    {
        var context = BuildContext();
        new RestLegalOTEvaluator().Evaluate(SomeRange, context).IsEmpty().Should().BeTrue();
    }

    // --- RestSpecialEvaluator / RestSpecialHolidayOTEvaluator ------------------------------

    [Fact]
    public void RestSpecialEvaluator_RestDayPlusSpecialNonWorking_ReturnsRange()
    {
        var context = BuildContext(HolidayType.SPECIAL, isRestDay: true);
        new RestSpecialEvaluator().Evaluate(SomeRange, context).Should().Be(SomeRange);
    }

    [Fact]
    public void RestSpecialEvaluator_NotARestDay_ReturnsEmpty()
    {
        var context = BuildContext(HolidayType.SPECIAL, isRestDay: false);
        new RestSpecialEvaluator().Evaluate(SomeRange, context).IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void RestSpecialHolidayOTEvaluator_SameGate()
    {
        var context = BuildContext(HolidayType.SPECIAL, isRestDay: true);
        new RestSpecialHolidayOTEvaluator().Evaluate(SomeRange, context).Should().Be(SomeRange);
    }

    // --- LegalHolidayEvaluator / LegalHolidayOTEvaluator -----------------------------------

    [Fact]
    public void LegalHolidayEvaluator_NonRestLegalHoliday_NotDouble_ReturnsRange()
    {
        var context = BuildContext(HolidayType.LEGAL, isRestDay: false);
        new LegalHolidayEvaluator().Evaluate(SomeRange, context).Should().Be(SomeRange);
    }

    [Fact]
    public void LegalHolidayEvaluator_RestDay_YieldsToRestLegalEvaluator()
    {
        var context = BuildContext(HolidayType.LEGAL, isRestDay: true);
        new LegalHolidayEvaluator().Evaluate(SomeRange, context).IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void LegalHolidayEvaluator_DoubleHoliday_YieldsToDoubleLegalHolidayEvaluator()
    {
        var context = BuildContext(HolidayType.LEGAL, isRestDay: false, isDoubleHoliday: true);
        new LegalHolidayEvaluator().Evaluate(SomeRange, context).IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void LegalHolidayOTEvaluator_SameGate()
    {
        var context = BuildContext(HolidayType.LEGAL, isRestDay: false);
        new LegalHolidayOTEvaluator().Evaluate(SomeRange, context).Should().Be(SomeRange);
    }

    // --- SpecialHolidayEvaluator / SpecialHolidayOTEvaluator -------------------------------

    [Fact]
    public void SpecialHolidayEvaluator_NonRestSpecialNonWorkingDay_ReturnsPipelineSpecialHoliday()
    {
        var specialValue = new TimeRange(90);
        var context = BuildContext(HolidayType.SPECIAL, isRestDay: false, specialHolidayPipelineValue: specialValue);

        new SpecialHolidayEvaluator().Evaluate(SomeRange, context).Should().Be(specialValue); // ignores `range`
    }

    [Fact]
    public void SpecialHolidayEvaluator_RestDay_ReturnsEmpty()
    {
        var context = BuildContext(HolidayType.SPECIAL, isRestDay: true);
        new SpecialHolidayEvaluator().Evaluate(SomeRange, context).IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void SpecialHolidayOTEvaluator_NonRestSpecialNonWorkingDay_ReturnsRange()
    {
        var context = BuildContext(HolidayType.SPECIAL, isRestDay: false);
        new SpecialHolidayOTEvaluator().Evaluate(SomeRange, context).Should().Be(SomeRange);
    }

    // --- DoubleLegalHolidayEvaluator / DoubleLegalHolidayOTEvaluator -----------------------

    [Fact]
    public void DoubleLegalHolidayEvaluator_NonRestDoubleHoliday_ReturnsRange()
    {
        var context = BuildContext(HolidayType.LEGAL, isRestDay: false, isDoubleHoliday: true);
        new DoubleLegalHolidayEvaluator().Evaluate(SomeRange, context).Should().Be(SomeRange);
    }

    [Fact]
    public void DoubleLegalHolidayEvaluator_RestDay_YieldsToDoubleRestLegalEvaluator()
    {
        var context = BuildContext(HolidayType.LEGAL, isRestDay: true, isDoubleHoliday: true);
        new DoubleLegalHolidayEvaluator().Evaluate(SomeRange, context).IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void DoubleLegalHolidayOTEvaluator_SameGate()
    {
        var context = BuildContext(HolidayType.LEGAL, isRestDay: false, isDoubleHoliday: true);
        new DoubleLegalHolidayOTEvaluator().Evaluate(SomeRange, context).Should().Be(SomeRange);
    }

    // --- NightDiffEvaluator -----------------------------------------------------------------

    [Fact]
    public void NightDiffEvaluator_MeetsThreshold_ReturnsRange()
    {
        var context = BuildContext();
        context.TimeContext.Payload.Data.CompanyPolicy.NightDiffThreshold = 30;

        new NightDiffEvaluator().Evaluate(new TimeRange(60), context).TotalMinutes.Should().Be(60);
    }

    [Fact]
    public void NightDiffEvaluator_BelowThreshold_ReturnsEmpty()
    {
        var context = BuildContext();
        context.TimeContext.Payload.Data.CompanyPolicy.NightDiffThreshold = 60;

        new NightDiffEvaluator().Evaluate(new TimeRange(30), context).IsEmpty().Should().BeTrue();
    }

    // --- NonHolidayEvaluator -----------------------------------------------------------------

    [Fact]
    public void NonHolidayEvaluator_NoHoliday_ReturnsRange()
    {
        var context = BuildContext();
        new NonHolidayEvaluator().Evaluate(SomeRange, context).Should().Be(SomeRange);
    }

    [Fact]
    public void NonHolidayEvaluator_HolidayTouchingDay_ReturnsEmpty()
    {
        var context = BuildContext(HolidayType.LEGAL);
        new NonHolidayEvaluator().Evaluate(SomeRange, context).IsEmpty().Should().BeTrue();
    }
}
