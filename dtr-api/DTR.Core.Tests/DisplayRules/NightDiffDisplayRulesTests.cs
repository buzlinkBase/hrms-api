using DTR.Core.DTR.DisplayRule;
using DTR.Core.DTR.DisplayRule.ColumnsViewRule.DisplayRules;
using DTR.Core.Tests.TestSupport;

namespace DTR.Core.Tests.DisplayRules;

/// <summary>
/// RegularNightdiffRule/LegalNightDiffRule — computes the night-diff portion of the already-
/// evaluated regular/holiday work ranges, subtracts any "RegularTimeTopUp" ledger amount, then
/// gates the result through NightDiffEvaluator (threshold) + NonHolidayEvaluator (day must not
/// touch a holiday) for the Regular rule, or through the LEGAL DayType.NIGHT_DIFF evaluator for
/// the Legal rule.
///
/// Both rules compute `IsND` via
/// NightDiffChecker.IsDutyNightDiff(shift.StartTime, shift.StartTime) — start compared against
/// itself, never shift.EndTime. Split() returns empty whenever end &lt;= start, so this always
/// evaluates false regardless of the shift's actual hours: the `!IsND` (exclude-topup) branch
/// runs unconditionally in both rules today. That looks like a latent copy/paste bug (probably
/// meant EndTime), but fixing it is out of scope here — these tests document and pin down the
/// CURRENT behavior only.
/// </summary>
public class NightDiffDisplayRulesTests : DtrTestBase
{
    private static readonly DateTime ShiftStart = new(2026, 1, 5, 18, 0, 0); // 6 PM
    private static readonly DateTime ShiftEnd = new(2026, 1, 6, 2, 0, 0);    // 2 AM -> 22:00-02:00 is ND (4h)
    private static readonly DateOnly Day = new(2026, 1, 5);

    private static DisplayContext BuildContext(
        TimeRange? legalHoliday = null, HolidayType? holidayType = null, bool isHolPlusReg = false)
    {
        var employeeId = NewEmployeeId();
        var holidays = holidayType.HasValue
            ? Holidays(employeeId, Holiday(holidayType.Value, Day))
            : null;
        var timeContext = CreateContext(ShiftStart, ShiftEnd, holidays: holidays, employeeId: employeeId);
        timeContext.Payload.Data.CompanyPolicy.IsHolPlusReg = isHolPlusReg;

        return new DisplayContext
        {
            TimeContext = timeContext,
            PipeLineResult = new PipeLineResult { LegalHoliday = legalHoliday ?? TimeRange.Empty },
        };
    }

    // --- RegularNightdiffRule ------------------------------------------------------------

    [Fact]
    public void RegularNightdiffRule_RegWorkCrossingNightHours_ReturnsTheNightPortion()
    {
        var regWork = Range(ShiftStart, ShiftEnd);
        var context = BuildContext();
        var evaluated = new EvaluatedColumnResult { RegWork = regWork, RestWork = TimeRange.Empty };

        var result = new RegularNightdiffRule(evaluated).ApplyRules(context);

        result.TotalMinutes.Should().Be(240); // 22:00-02:00
    }

    [Fact]
    public void RegularNightdiffRule_RegWorkEntirelyDaytime_ReturnsEmpty()
    {
        var daytimeStart = new DateTime(2026, 1, 5, 8, 0, 0);
        var daytimeEnd = new DateTime(2026, 1, 5, 17, 0, 0);
        var regWork = Range(daytimeStart, daytimeEnd);
        var context = BuildContext();
        var evaluated = new EvaluatedColumnResult { RegWork = regWork, RestWork = TimeRange.Empty };

        var result = new RegularNightdiffRule(evaluated).ApplyRules(context);

        result.IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void RegularNightdiffRule_DayTouchesAHoliday_NonHolidayGateReturnsEmpty()
    {
        var regWork = Range(ShiftStart, ShiftEnd);
        var context = BuildContext(holidayType: HolidayType.LEGAL);
        var evaluated = new EvaluatedColumnResult { RegWork = regWork, RestWork = TimeRange.Empty };

        var result = new RegularNightdiffRule(evaluated).ApplyRules(context);

        result.IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void RegularNightdiffRule_RegularTimeTopUpOverlappingTheNightPortion_IsExcluded()
    {
        var regWork = Range(ShiftStart, ShiftEnd);
        var context = BuildContext();
        var evaluated = new EvaluatedColumnResult { RegWork = regWork, RestWork = TimeRange.Empty };

        // Exclude the first hour of the night window (22:00-23:00) via the topup ledger tag.
        var topupStart = new DateTime(2026, 1, 5, 22, 0, 0);
        var topup = Range(topupStart, topupStart.AddHours(1));
        context.TimeContext.Payload.Ledger.RecordByTag("RegularTimeTopUp", context.TimeContext, topup);

        var result = new RegularNightdiffRule(evaluated).ApplyRules(context);

        result.TotalMinutes.Should().Be(180); // 240 - 60
    }

    [Fact]
    public void RegularNightdiffRule_IncludesRestWorkAlongsideRegWork()
    {
        var restWork = Range(ShiftStart, ShiftEnd);
        var context = BuildContext();
        var evaluated = new EvaluatedColumnResult { RegWork = TimeRange.Empty, RestWork = restWork };

        var result = new RegularNightdiffRule(evaluated).ApplyRules(context);

        result.TotalMinutes.Should().Be(240);
    }

    // --- LegalNightDiffRule --------------------------------------------------------------

    [Fact]
    public void LegalNightDiffRule_NotALegalHolidayDay_ReturnsEmpty()
    {
        var context = BuildContext();
        var evaluated = new EvaluatedColumnResult();

        var result = new LegalNightDiffRule(evaluated).ApplyRules(context);

        result.IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void LegalNightDiffRule_HolPlusRegOff_UsesEvaluatedLegalHolidayRange()
    {
        var legalHoliday = Range(ShiftStart, ShiftEnd);
        var context = BuildContext(legalHoliday: legalHoliday, holidayType: HolidayType.LEGAL, isHolPlusReg: false);
        var evaluated = new EvaluatedColumnResult { LegalHoliday = legalHoliday };

        var result = new LegalNightDiffRule(evaluated).ApplyRules(context);

        result.TotalMinutes.Should().Be(240); // 22:00-02:00 portion of the legal-holiday range
    }

    [Fact]
    public void LegalNightDiffRule_HolPlusRegOn_UsesWorkTimeLedgerInstead()
    {
        var context = BuildContext(holidayType: HolidayType.LEGAL, isHolPlusReg: true);
        var evaluated = new EvaluatedColumnResult();
        var workTime = Range(ShiftStart, ShiftEnd);
        context.TimeContext.Payload.Ledger.RecordByTag("work_time", context.TimeContext, workTime);

        var result = new LegalNightDiffRule(evaluated).ApplyRules(context);

        result.TotalMinutes.Should().Be(240);
    }
}
