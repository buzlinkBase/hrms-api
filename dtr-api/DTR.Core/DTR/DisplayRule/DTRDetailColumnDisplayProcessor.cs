using DTR.Core.DTR.DisplayRule.ColumnsViewRule.DisplayRules;
using DTR.Core.DTR.DisplayRule.ColumnsViewRule.Evaluators;

namespace DTR.Core;

public static class DutyTypeMapFactory
{
    public static readonly Dictionary<DayType, IDutyDayEvaluator> Create = new()
    {
        { DayType.REGULAR, new RegularDayEvaluator()},
        { DayType.RESTDAY, new RestDayEvaluator() },

        { DayType.REGULAR_OT, new RegularOverTimeEvaluator()},
        { DayType.REST_OT, new RestOverTimeEvaluator()},

        { DayType.LEGAL, new LegalHolidayEvaluator()},
        { DayType.LEGAL_OT, new LegalHolidayOTEvaluator()},

        { DayType.SPECIAL, new  SpecialHolidayEvaluator()},
        { DayType.SPECIAL_OT, new SpecialHolidayOTEvaluator()},

        { DayType.RESTLEGAL, new RestLegalEvaluator()},
        { DayType.RESTLEGAL_OT, new RestLegalHolidayOTEvaluator()},

        { DayType.RESTSPECIAL, new RestSpecialEvaluator()},
        { DayType.RESTSPECIAL_OT, new RestSpecialHolidayOTEvaluator()},

        { DayType.DOUBLE_LEGAL, new DoubleLegalHolidayEvaluator()},
        { DayType.DOUBLE_LEGAL_OT, new DoubleLegalHolidayOTEvaluator()},

        { DayType.RESTDOUBLE_LEGAL, new DoubleRestLegalEvaluator()},
        { DayType.RESTDOUBLE_LEGAL_OT, new DoubleRestLegalHolidayOTEvaluator()},

        { DayType.NIGHT_DIFF, new NightDiffEvaluator()},
        { DayType.NONHOLIDAY, new NonHolidayEvaluator()},

    };
}

public interface IDTRDetailColumnDisplayProcessor
{
    EvaluatedColumnResult DisplayRule(DisplayContext displayContext);
    NightDiffEvaluationResult ComputeNightDiff(EvaluatedColumnResult evaluated, DisplayContext dContext);
}

public class DTRDetailColumnDisplayProcessor : IDTRDetailColumnDisplayProcessor
{
    public NightDiffEvaluationResult ComputeNightDiff(EvaluatedColumnResult evaluated, DisplayContext dContext)
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
            LegalOT = EvalND(evaluated.LegalOT),
            SpecialOT = EvalND(evaluated.SpecialOT),
            RestLegal = EvalND(evaluated.RestLegal),
            RestLegalOT = EvalND(evaluated.RestLegalOT),
            RestSpecial = EvalND(evaluated.RestSpecial),
            RestSpecialOT = EvalND(evaluated.RestSpecialOT),
            DoubleLegalHoliday = EvalND(evaluated.DoubleLegalHoliday),
            DoubleLegalHolidayOT = EvalND(evaluated.DoubleLegalHolidayOT),
            RestDoubleLegal = EvalND(evaluated.RestDoubleLegal),
            RestDoubleLegalOT = EvalND(evaluated.RestDoubleLegalOT),
        };
    }

    public EvaluatedColumnResult DisplayRule(DisplayContext displayContext)
    {
        var evaluators = DutyTypeMapFactory.Create;
        var LegalHolidayRange = new ColumnDisplayEvaluator(new LegalHolidayRule()).Handle(displayContext);
        var regularOT = displayContext.PipeLineResult.OT;
        var regTimeRange = new ColumnDisplayEvaluator(new HolidayPlusRegularRule()).Handle(displayContext);
        var holidayOTProvider = HolidayOTFactory.Create(displayContext.TimeContext, regularOT);
        var legalOT = holidayOTProvider.Calculate(HolidayType.LEGAL);
        var specialOT = holidayOTProvider.Calculate(HolidayType.SPECIAL);
        var legalOTResult = evaluators[DayType.LEGAL_OT].Evaluate(legalOT, displayContext);
        var specialOTResult = evaluators[DayType.SPECIAL_OT].Evaluate(specialOT, displayContext);

        // Rest Day + Holiday Overtime
        return new EvaluatedColumnResult
        {
            RegWork = evaluators[DayType.REGULAR].Evaluate(regTimeRange, displayContext),
            RestWork = evaluators[DayType.RESTDAY].Evaluate(regTimeRange, displayContext),
            OverTime = regularOT,
            LegalHoliday = evaluators[DayType.LEGAL].Evaluate(LegalHolidayRange, displayContext),
            SPHoliday = evaluators[DayType.SPECIAL].Evaluate(displayContext.PipeLineResult.SpecialHoliday, displayContext),
            RegOT = evaluators[DayType.REGULAR_OT].Evaluate(regularOT, displayContext),
            RestOT = evaluators[DayType.REST_OT].Evaluate(regularOT, displayContext),
            LegalOT = legalOTResult,
            SpecialOT = specialOTResult,
            RestLegal = evaluators[DayType.RESTLEGAL].Evaluate(displayContext.PipeLineResult.LegalHoliday, displayContext),
            RestLegalOT = evaluators[DayType.RESTLEGAL_OT].Evaluate(legalOT, displayContext),
            RestSpecial = evaluators[DayType.RESTSPECIAL].Evaluate(displayContext.PipeLineResult.SpecialHoliday, displayContext),
            RestSpecialOT = evaluators[DayType.RESTSPECIAL_OT].Evaluate(specialOT, displayContext),
            DoubleLegalHoliday = evaluators[DayType.DOUBLE_LEGAL].Evaluate(LegalHolidayRange, displayContext),
            DoubleLegalHolidayOT = evaluators[DayType.DOUBLE_LEGAL_OT].Evaluate(legalOT, displayContext),
            RestDoubleLegal = evaluators[DayType.RESTDOUBLE_LEGAL].Evaluate(LegalHolidayRange, displayContext),
            RestDoubleLegalOT = evaluators[DayType.RESTDOUBLE_LEGAL_OT].Evaluate(legalOT, displayContext),
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

    public TimeRange LegalOT { get; set; } = TimeRange.Empty;
    public TimeRange SpecialOT { get; set; } = TimeRange.Empty;

    public TimeRange RestLegal { get; set; } = TimeRange.Empty;
    public TimeRange RestLegalOT { get; set; } = TimeRange.Empty;
    public TimeRange RestSpecial { get; set; } = TimeRange.Empty;
    public TimeRange RestSpecialOT { get; set; } = TimeRange.Empty;

    public TimeRange DoubleLegalHoliday { get; set; } = TimeRange.Empty;
    public TimeRange DoubleLegalHolidayOT { get; set; } = TimeRange.Empty;
    public TimeRange RestDoubleLegal { get; set; } = TimeRange.Empty;
    public TimeRange RestDoubleLegalOT { get; set; } = TimeRange.Empty;


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

    public TimeRange DoubleLegalHoliday { get; set; } = TimeRange.Empty;
    public TimeRange DoubleLegalHolidayOT { get; set; } = TimeRange.Empty;
    public TimeRange RestDoubleLegal { get; set; } = TimeRange.Empty;
    public TimeRange RestDoubleLegalOT { get; set; } = TimeRange.Empty;

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
    public TimeRange Travel { get; set; } = TimeRange.Empty;
}