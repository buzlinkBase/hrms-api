using DTR.Core.Tests.TestSupport;

namespace DTR.Core.Tests.DisplayRules;

/// <summary>
/// DTRDetailColumnDisplayProcessor.ComputeNightDiff — untested until now. Feeds a hand-built
/// EvaluatedColumnResult (isolating this method from the pipeline/DisplayRule layers already
/// covered elsewhere) and checks how each NightDiffEvaluationResult field is actually wired:
///
///   - Regular/Rest both read the SAME RegularNightdiffRule computation
///     (regular = evaluated.RegWork + evaluated.RestWork internally), threshold-gated via
///     DayType.NIGHT_DIFF, and are gated independently on RegWork's/RestWork's own emptiness.
///     RegularNightdiffRule used to also re-zero the result via a coarse "does this DAY touch
///     a holiday at all" check (CompositeDutyEvaluators.RegularNightDiff/NonHolidayEvaluator) —
///     that incorrectly dropped real night-diff minutes on a boundary-crossing
///     BasedOnActualWorkHours shift whose non-holiday portion genuinely fell in night hours
///     (a real production bug: a Sept4-Sept5 night shift with Sept4 Special-holiday showed
///     ND blank for the Sept5 non-holiday remainder even though that remainder was entirely
///     within the night-diff window). Fixed to trust RegWork/RestWork's own value — see
///     RegularNightdiffRule.
///   - RegOT/Special/LegalOT/SpecialOT/RestLegal/RestLegalOT/RestSpecial/RestSpecialOT/
///     DoubleLegalHoliday/DoubleLegalHolidayOT/RestDoubleLegal/RestDoubleLegalOT all go through
///     a uniform EvalND(range) = threshold-gated NightDiffCalculator.Calculate(range) — no
///     holiday gating, no RegularTimeTopUp exclusion, just the raw night-window overlap of
///     whatever EvaluatedColumnResult value is passed in.
///   - Legal is the one field with its own dedicated wiring (LegalNightDiffRule), gated on
///     actually being a Legal holiday day.
/// </summary>
public class DTRDetailColumnDisplayProcessorComputeNightDiffTests : DtrTestBase
{
    private static readonly DateTime ShiftStart = new(2026, 1, 1, 18, 0, 0);
    private static readonly DateTime ShiftEnd = new(2026, 1, 2, 2, 0, 0); // 8h, crosses midnight

    private static DisplayContext BuildContext(
        double nightDiffThreshold = 0,
        Dictionary<Holidaykey, List<HolidayInfo>>? holidays = null,
        Guid? employeeId = null,
        PipeLineResult? pipelineResult = null)
    {
        var empId = employeeId ?? NewEmployeeId();
        var timeContext = CreateContext(ShiftStart, ShiftEnd, HolidayTimeBasis.BasedOnActualWorkHours, holidays, empId);
        timeContext.Payload.Data.CompanyPolicy.NightDiffThreshold = nightDiffThreshold;
        return CreateDisplayContext(timeContext, pipelineResult ?? new PipeLineResult());
    }

    private static readonly DTRDetailColumnDisplayProcessor Processor = new();

    // --- Regular ------------------------------------------------------------------------------

    [Fact]
    public void Regular_RegWorkCrossesNightHours_NonHolidayDay_ReturnsTheOverlap()
    {
        var context = BuildContext();
        var evaluated = new EvaluatedColumnResult { RegWork = Range(ShiftStart, ShiftEnd) };

        var result = Processor.ComputeNightDiff(evaluated, context);

        result.Regular.TotalMinutes.Should().Be(240); // 22:00-02:00 overlap
    }

    [Fact]
    public void Regular_RegWorkEmpty_ReturnsEmpty()
    {
        var context = BuildContext();
        var evaluated = new EvaluatedColumnResult { RegWork = TimeRange.Empty };

        var result = Processor.ComputeNightDiff(evaluated, context);

        result.Regular.IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void Regular_DayTouchesAHoliday_StillReturnsTheOverlap_TrustsRegWorksOwnValue()
    {
        // RegWork is only ever populated with genuine non-holiday minutes by the time it
        // reaches here (RegularDayEvaluator already excludes the holiday portion) — so a
        // holiday-touching day must NOT re-zero real night-diff minutes that fall on RegWork's
        // own (non-holiday) time. This is the regression this session's production bug fix
        // guards against.
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId, Holiday(HolidayType.LEGAL, new DateOnly(2026, 1, 1)));
        var context = BuildContext(holidays: holidays, employeeId: employeeId);
        var evaluated = new EvaluatedColumnResult { RegWork = Range(ShiftStart, ShiftEnd) }; // spans night hours

        var result = Processor.ComputeNightDiff(evaluated, context);

        result.Regular.TotalMinutes.Should().Be(240); // 22:00-02:00 overlap, same as the non-holiday case
    }

    // --- Rest -----------------------------------------------------------------------------------

    [Fact]
    public void Rest_RestWorkCrossesNightHours_NonHolidayDay_ReturnsTheOverlap()
    {
        var context = BuildContext();
        var evaluated = new EvaluatedColumnResult { RestWork = Range(ShiftStart, ShiftEnd) };

        var result = Processor.ComputeNightDiff(evaluated, context);

        result.Rest.TotalMinutes.Should().Be(240);
    }

    [Fact]
    public void Rest_RestWorkEmpty_ReturnsEmpty()
    {
        var context = BuildContext();
        var evaluated = new EvaluatedColumnResult { RestWork = TimeRange.Empty };

        var result = Processor.ComputeNightDiff(evaluated, context);

        result.Rest.IsEmpty().Should().BeTrue();
    }

    // --- Directly-EvalND'd fields (no holiday gating, no topup exclusion) ----------------------

    [Fact]
    public void RegOT_CrossesNightHours_ReturnsTheOverlap()
    {
        var context = BuildContext();
        var ot = Range(new DateTime(2026, 1, 2, 2, 0, 0), new DateTime(2026, 1, 2, 4, 0, 0)); // fully within 00:00-06:00
        var evaluated = new EvaluatedColumnResult { RegOT = ot };

        var result = Processor.ComputeNightDiff(evaluated, context);

        result.RegOT.TotalMinutes.Should().Be(120);
    }

    [Fact]
    public void Special_CrossesNightHours_ReturnsTheOverlap_NoHolidaySetupRequired()
    {
        // EvalND doesn't gate on IsSpecialNonWorking or any holiday classification -- it just
        // computes the night-window overlap of whatever evaluated.SPHoliday already holds.
        var context = BuildContext();
        var special = Range(new DateTime(2026, 1, 1, 20, 0, 0), new DateTime(2026, 1, 1, 23, 0, 0)); // 3h, 1h in-window
        var evaluated = new EvaluatedColumnResult { SPHoliday = special };

        var result = Processor.ComputeNightDiff(evaluated, context);

        result.Special.TotalMinutes.Should().Be(60); // 22:00-23:00
    }

    [Fact]
    public void RestLegal_CrossesNightHours_ReturnsTheOverlap()
    {
        var context = BuildContext();
        var restLegal = Range(new DateTime(2026, 1, 1, 20, 0, 0), new DateTime(2026, 1, 1, 23, 0, 0));
        var evaluated = new EvaluatedColumnResult { RestLegal = restLegal };

        var result = Processor.ComputeNightDiff(evaluated, context);

        result.RestLegal.TotalMinutes.Should().Be(60);
    }

    [Fact]
    public void DoubleLegalHoliday_CrossesNightHours_ReturnsTheOverlap()
    {
        var context = BuildContext();
        var doubleLegal = Range(new DateTime(2026, 1, 1, 20, 0, 0), new DateTime(2026, 1, 1, 23, 0, 0));
        var evaluated = new EvaluatedColumnResult { DoubleLegalHoliday = doubleLegal };

        var result = Processor.ComputeNightDiff(evaluated, context);

        result.DoubleLegalHoliday.TotalMinutes.Should().Be(60);
    }

    [Fact]
    public void DoubleLegalHolidayOT_CrossesNightHours_ReturnsTheOverlap()
    {
        var context = BuildContext();
        var doubleLegalOT = Range(new DateTime(2026, 1, 1, 20, 0, 0), new DateTime(2026, 1, 1, 23, 0, 0));
        var evaluated = new EvaluatedColumnResult { DoubleLegalHolidayOT = doubleLegalOT };

        var result = Processor.ComputeNightDiff(evaluated, context);

        result.DoubleLegalHolidayOT.TotalMinutes.Should().Be(60);
    }

    [Fact]
    public void RestDoubleLegal_CrossesNightHours_ReturnsTheOverlap()
    {
        var context = BuildContext();
        var restDoubleLegal = Range(new DateTime(2026, 1, 1, 20, 0, 0), new DateTime(2026, 1, 1, 23, 0, 0));
        var evaluated = new EvaluatedColumnResult { RestDoubleLegal = restDoubleLegal };

        var result = Processor.ComputeNightDiff(evaluated, context);

        result.RestDoubleLegal.TotalMinutes.Should().Be(60);
    }

    [Fact]
    public void RestDoubleLegalOT_CrossesNightHours_ReturnsTheOverlap()
    {
        var context = BuildContext();
        var restDoubleLegalOT = Range(new DateTime(2026, 1, 1, 20, 0, 0), new DateTime(2026, 1, 1, 23, 0, 0));
        var evaluated = new EvaluatedColumnResult { RestDoubleLegalOT = restDoubleLegalOT };

        var result = Processor.ComputeNightDiff(evaluated, context);

        result.RestDoubleLegalOT.TotalMinutes.Should().Be(60);
    }

    [Fact]
    public void BelowNightDiffThreshold_ReturnsEmpty()
    {
        var context = BuildContext(nightDiffThreshold: 90); // more than the 60-min overlap below
        var special = Range(new DateTime(2026, 1, 1, 20, 0, 0), new DateTime(2026, 1, 1, 23, 0, 0));
        var evaluated = new EvaluatedColumnResult { SPHoliday = special };

        var result = Processor.ComputeNightDiff(evaluated, context);

        result.Special.IsEmpty().Should().BeTrue();
    }

    // --- Legal (its own dedicated LegalNightDiffRule wiring) -----------------------------------

    [Fact]
    public void Legal_LegalHolidayDay_HolPlusRegOff_MatchesTheDirectLegalNightDiffRuleComputation()
    {
        var employeeId = NewEmployeeId();
        var legalDate = new DateOnly(2026, 1, 1);
        var holidays = Holidays(employeeId, Holiday(HolidayType.LEGAL, legalDate));
        var legalHoliday = Range(ShiftStart, ShiftEnd); // full shift classified as legal-holiday time
        var context = BuildContext(
            holidays: holidays, employeeId: employeeId,
            pipelineResult: new PipeLineResult { LegalHoliday = legalHoliday });
        var evaluated = new EvaluatedColumnResult { LegalHoliday = legalHoliday };

        var result = Processor.ComputeNightDiff(evaluated, context);

        result.Legal.TotalMinutes.Should().Be(240); // 22:00-02:00, same overlap math as Regular's
    }

    [Fact]
    public void Legal_NotALegalHolidayDay_ReturnsEmpty()
    {
        var context = BuildContext();
        var evaluated = new EvaluatedColumnResult { LegalHoliday = Range(ShiftStart, ShiftEnd) };

        var result = Processor.ComputeNightDiff(evaluated, context);

        result.Legal.IsEmpty().Should().BeTrue();
    }
}
