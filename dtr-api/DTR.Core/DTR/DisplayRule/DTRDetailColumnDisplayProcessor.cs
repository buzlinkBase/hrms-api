namespace DTR.Core;

public static class DTRDetailColumnDisplayProcessor
{
    public static NightDiffEvaluationResult ComputeNightDiff(EvaluatedColumnResult evaluated, DisplayContext dContext)
    {
        var context = dContext.TimeContext;
        var NightDiffThresholdEvaluator = DutyTypeMapFactory.Create[DayType.NIGHT_DIFF];
        //var NonHoldEval = DutyTypeMapFactory.Create[DayType.NONHOLIDAY];
        var IsND = NightDiffChecker.IsDutyNightDiff(context.Payload.Data.CurrentShift.StartTime, context.Payload.Data.CurrentShift.StartTime);
        TimeRange worktime = context.Payload.Ledger.GetByTag("work_time", context);
        if (!IsND)
        {
            //exclude topup if not ND
            var topup = context.Payload.Ledger.GetByTag("RegularTimeTopUp", context);
            worktime = worktime
              .TimeRecords.Exclude(topup.TimeRecords)
              .ToTimeRange()
              ;
        }
        var NDLH = new ColumnEvaluatorHandler(new NDLHColumnEvaluator(evaluated)).Handle(dContext);
        var NDREGULAR = new ColumnEvaluatorHandler(new NDRegularEvaluator(evaluated)).Handle(dContext);

        return new NightDiffEvaluationResult
        {
            //ND = NightDiffThresholdEvaluator.Evaluate(NightDiffCalculator.Calculate(worktime), context),
            //NDOT = NightDiffThresholdEvaluator.Evaluate(NightDiffCalculator.Calculate(evaluated.OverTime), context),
            Regular = !evaluated.RegWork.IsEmpty() ? NDREGULAR : TimeRange.Empty,
            Rest = !evaluated.RestWork.IsEmpty() ? NDREGULAR : TimeRange.Empty,
            RegOT = NightDiffThresholdEvaluator.Evaluate(NightDiffCalculator.Calculate(evaluated.RegOT), dContext),
            RestOT = NightDiffThresholdEvaluator.Evaluate(NightDiffCalculator.Calculate(evaluated.RestOT), dContext),
            Legal = NDLH,
            Special = NightDiffThresholdEvaluator.Evaluate(NightDiffCalculator.Calculate(evaluated.SPHoliday), dContext),
            LegalOT = NightDiffThresholdEvaluator.Evaluate(NightDiffCalculator.Calculate(evaluated.LHOT), dContext),
            SpecialOT = NightDiffThresholdEvaluator.Evaluate(NightDiffCalculator.Calculate(evaluated.SPOT), dContext),

            RestLegal = NightDiffThresholdEvaluator.Evaluate(NightDiffCalculator.Calculate(evaluated.RestLegal), dContext),
            RestLegalOT = NightDiffThresholdEvaluator.Evaluate(NightDiffCalculator.Calculate(evaluated.RestLegalOT), dContext),
            RestSpecial = NightDiffThresholdEvaluator.Evaluate(NightDiffCalculator.Calculate(evaluated.RestSpecial), dContext),
            RestSpecialOT = NightDiffThresholdEvaluator.Evaluate(NightDiffCalculator.Calculate(evaluated.RestSpecialOT), dContext),
        };
    }

    public static EvaluatedColumnResult DisplayRule(DisplayContext displayContext)
    {
        var context = displayContext.TimeContext;
        var payload = context.Payload;

        var regDayEvaluator = DutyTypeMapFactory.Create[DayType.REGULAR];
        var restDayEvaluator = DutyTypeMapFactory.Create[DayType.RESTDAY];
        var otEvaluator = DutyTypeMapFactory.Create[DayType.REGRESTOVERTIME];
        var legalHolOTEvaluator = DutyTypeMapFactory.Create[DayType.LEGAL_HOLIDAY_OVERTIME];
        var specialHolOTEvaluator = DutyTypeMapFactory.Create[DayType.SPECIAL_HOLIDAY_OVERTIME];
        var restLegalEvaluator = DutyTypeMapFactory.Create[DayType.RESTLEGAL];
        var restSpecialEvaluator = DutyTypeMapFactory.Create[DayType.RESTSPECIAL];
        var restHolOtEvaluator = DutyTypeMapFactory.Create[DayType.REST_HOL_OT];

        var provider = HolidayOTFactory.Create(context, displayContext.PipeLineResult.OT);
        var regularDisplay = new ColumnEvaluatorHandler(new RegularColumnStrategy()).Handle(displayContext);
        var holidayDisplay = new ColumnEvaluatorHandler(new LHColumnEvaluator()).Handle(displayContext);
        var spHolidayDisplay = new ColumnEvaluatorHandler(new SPColumEvaluator()).Handle(displayContext);
        var OverTime = new ColumnEvaluatorHandler(new OTColumnEvaluator()).Handle(displayContext);

        var regularWork = regDayEvaluator.Evaluate(regularDisplay, displayContext);
        var restWork = restDayEvaluator.Evaluate(regularDisplay, displayContext);

        var regOT = otEvaluator.Evaluate(regDayEvaluator.Evaluate(OverTime, displayContext), displayContext);
        var restOT = otEvaluator.Evaluate(restDayEvaluator.Evaluate(OverTime, displayContext), displayContext);

        var legalOT = provider.Calculate(HolidayType.LEGAL);
        var SpecialOT = provider.Calculate(HolidayType.SPECIAL);

        var legalOTResult = legalHolOTEvaluator.Evaluate(legalOT, displayContext);
        var SpecialOTResult = specialHolOTEvaluator.Evaluate(SpecialOT, displayContext);

        var RestLegalOT = restHolOtEvaluator.Evaluate(restLegalEvaluator.Evaluate(legalOT, displayContext), displayContext);
        var RestSpecialOT = restHolOtEvaluator.Evaluate(restSpecialEvaluator.Evaluate(SpecialOT, displayContext), displayContext);

        var evaluated = new EvaluatedColumnResult
        {
            RegWork = regularWork,
            RestWork = restWork,
            OverTime = OverTime,
            LegalHoliday = holidayDisplay,
            SPHoliday = spHolidayDisplay,
            RegOT = regOT,
            RestOT = restOT,
            LHOT = legalOTResult,
            SPOT = SpecialOTResult,
            RestLegal = restLegalEvaluator.Evaluate(displayContext.PipeLineResult.LegalHoliday, displayContext),
            RestLegalOT = RestLegalOT,
            RestSpecial = restSpecialEvaluator.Evaluate(displayContext.PipeLineResult.SpecialHoliday, displayContext),
            RestSpecialOT = RestSpecialOT,
        };
        return evaluated;
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