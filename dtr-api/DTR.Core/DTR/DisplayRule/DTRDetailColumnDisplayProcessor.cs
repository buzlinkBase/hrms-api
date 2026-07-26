using DTR.Core.DTR.DisplayRule.ColumnsViewRule.DisplayRules;
using DTR.Core.DTR.DisplayRule.ColumnsViewRule.Evaluators;

namespace DTR.Core;

public static class DTRDetailColumnDisplayProcessor
{
    public static NightDiffEvaluationResult ComputeNightDiff(EvaluatedColumnResult evaluated, DisplayContext dContext)
    {
        var nightDiffThresholdEvaluator = DutyTypeMapFactory.Create[DayType.NIGHT_DIFF];
        var ndlh = new ColumnDisplayEvaluator(new LegalNightDiffRule(evaluated)).Handle(dContext);
        var ndRegular = new ColumnDisplayEvaluator(new RegularNightdiffRule(evaluated)).Handle(dContext);
        TimeRange EvalND(TimeRange range) => nightDiffThresholdEvaluator.Evaluate(NightDiffCalculator.Calculate(range), dContext);
        return new NightDiffEvaluationResult
        {
            Regular = !evaluated.RegWork.IsEmpty() ? ndRegular : TimeRange.Empty,
            Rest = !evaluated.RestWork.IsEmpty() ? ndRegular : TimeRange.Empty,
            RegOT = EvalND(evaluated.RegOT),
            RestOT = EvalND(evaluated.RestOT),
            Legal = ndlh,
            Special = EvalND(evaluated.SPHoliday),
            LegalOT = EvalND(evaluated.LHOT),
            SpecialOT = EvalND(evaluated.SPOT),
            RestLegal = EvalND(evaluated.RestLegal),
            RestLegalOT = EvalND(evaluated.RestLegalOT),
            RestSpecial = EvalND(evaluated.RestSpecial),
            RestSpecialOT = EvalND(evaluated.RestSpecialOT)
        };
    }

    public static EvaluatedColumnResult DisplayRule(DisplayContext displayContext)
    {
        var evaluators = DutyTypeMapFactory.Create;
        var OtProvider = HolidayOTFactory.Create(displayContext.TimeContext, displayContext.PipeLineResult.OT);
        // Context-based calculations
        var regTimeRange = new ColumnDisplayEvaluator(new HolidayToRegularRule()).Handle(displayContext);
        var holidaTimeRange = new ColumnDisplayEvaluator(new HolidayPlusAutoTimeCreditRule()).Handle(displayContext);
        var spHolidayDisplay = new ColumnDisplayEvaluator(new SpecialHolidayDisplayRule()).Handle(displayContext);
        var overTime = displayContext.PipeLineResult.OT;

        // Regular & Rest Work
        var regularWork = evaluators[DayType.REGULAR].Evaluate(regTimeRange, displayContext);
        var restWork = evaluators[DayType.RESTDAY].Evaluate(regTimeRange, displayContext);

        // Overtime
        var regOT = CompositeDutyEvaluators.RegularOvertime.Evaluate(overTime, displayContext);
        var restOT = CompositeDutyEvaluators.RestOvertime.Evaluate(overTime, displayContext);

        // Holidays
        var legalOT = OtProvider.Calculate(HolidayType.LEGAL);
        var specialOT = OtProvider.Calculate(HolidayType.SPECIAL);
        var legalOTResult = evaluators[DayType.LEGAL_HOLIDAY_OVERTIME].Evaluate(legalOT, displayContext);
        var specialOTResult = evaluators[DayType.SPECIAL_HOLIDAY_OVERTIME].Evaluate(specialOT, displayContext);

        // Rest Day + Holiday Overtime
        var restLegalOT = CompositeDutyEvaluators.RestLegalOvertime.Evaluate(legalOT, displayContext);
        var restSpecialOT = CompositeDutyEvaluators.RestSpecialOvertime.Evaluate(specialOT, displayContext);

        return new EvaluatedColumnResult
        {
            RegWork = regularWork,
            RestWork = restWork,
            OverTime = overTime,
            LegalHoliday = holidaTimeRange,
            SPHoliday = spHolidayDisplay,
            RegOT = regOT,
            RestOT = restOT,
            LHOT = legalOTResult,
            SPOT = specialOTResult,
            RestLegal = evaluators[DayType.RESTLEGAL].Evaluate(displayContext.PipeLineResult.LegalHoliday, displayContext),
            RestLegalOT = restLegalOT,
            RestSpecial = evaluators[DayType.RESTSPECIAL].Evaluate(displayContext.PipeLineResult.SpecialHoliday, displayContext),
            RestSpecialOT = restSpecialOT
        };
    }
}

public class EvaluatedColumnResult
{
    public TimeRange RegWork { get; set; } = TimeRange.Empty;
    public TimeRange RestWork { get; set; } = TimeRange.Empty;
    public TimeRange LegalHoliday { get; set; } = TimeRange.Empty;
    public TimeRange SPHoliday { get; set; } = TimeRange.Empty;

    public TimeRange OverTime { get; set; } = TimeRange.Empty;
    public TimeRange RegOT { get; set; } = TimeRange.Empty;
    public TimeRange RestOT { get; set; } = TimeRange.Empty;

    public TimeRange LHOT { get; set; } = TimeRange.Empty;
    public TimeRange SPOT { get; set; } = TimeRange.Empty;

    public TimeRange RestLegal { get; set; } = TimeRange.Empty;
    public TimeRange RestLegalOT { get; set; } = TimeRange.Empty;
    public TimeRange RestSpecial { get; set; } = TimeRange.Empty;
    public TimeRange RestSpecialOT { get; set; } = TimeRange.Empty;

}
public class NightDiffEvaluationResult
{
    public TimeRange Regular { get; set; } = TimeRange.Empty;
    public TimeRange RegOT { get; set; } = TimeRange.Empty;
    public TimeRange Rest { get; set; } = TimeRange.Empty;
    public TimeRange RestOT { get; set; } = TimeRange.Empty;

    public TimeRange Legal { get; set; } = TimeRange.Empty;
    public TimeRange LegalOT { get; set; } = TimeRange.Empty;
    public TimeRange Special { get; set; } = TimeRange.Empty;
    public TimeRange SpecialOT { get; set; } = TimeRange.Empty;

    public TimeRange RestLegal { get; set; } = TimeRange.Empty;
    public TimeRange RestLegalOT { get; set; } = TimeRange.Empty;
    public TimeRange RestSpecial { get; set; } = TimeRange.Empty;
    public TimeRange RestSpecialOT { get; set; } = TimeRange.Empty;

}
public class PipeLineResult
{
    public TimeRange Regular { get; set; } = TimeRange.Empty;
    public TimeRange LegalHoliday { get; set; } = TimeRange.Empty;
    public TimeRange SpecialHoliday { get; set; } = TimeRange.Empty;
    public TimeRange Plus8 { get; set; } = TimeRange.Empty;
    public TimeRange OT { get; set; } = TimeRange.Empty;
    public TimeRange Late { get; set; } = TimeRange.Empty;
    public TimeRange UT { get; set; } = TimeRange.Empty;
    public TimeRange Overbreak { get; set; } = TimeRange.Empty;
    public TimeRange Leave { get; set; } = TimeRange.Empty;
}