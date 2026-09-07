using DTR.Core.DTR.DisplayRule.ColumnsViewRule.Evaluators;

namespace DTR.Core.DTR.DisplayRule.ColumnsViewRule.DisplayRules;

internal class RegularNightdiffRule : IColumnDisplayRule
{
    private readonly EvaluatedColumnResult _evaluated;
    public RegularNightdiffRule(EvaluatedColumnResult evaluated)
    {
        _evaluated = evaluated;
    }
    public TimeRange ApplyRules(DisplayContext context)
    { 
        var regular = _evaluated.RegWork + _evaluated.RestWork;
        var IsND = NightDiffChecker.IsDutyNightDiff(context.TimeContext.Payload.Data.CurrentShift.StartTime, context.TimeContext.Payload.Data.CurrentShift.StartTime);
        var topup = context.TimeContext.Payload.Ledger.GetByTag("RegularTimeTopUp", context.TimeContext);
        if (!IsND)
        {
            regular = regular.TimeRecords
                .Exclude(topup.TimeRecords)
                .ToTimeRange();
        }
        var nd = NightDiffCalculator.Calculate(regular);
        // `regular` (evaluated.RegWork + evaluated.RestWork) is already the fully-resolved,
        // holiday-excluded non-holiday portion by the time it reaches here — RegularDayEvaluator/
        // RestDayEvaluator only ever put actual non-holiday minutes into RegWork/RestWork (Empty
        // outright under BasedOnTimeInDayType on a holiday day; the true non-holiday remainder
        // under BasedOnActualWorkHours on a boundary-crossing shift). Re-zeroing `nd` here via the
        // coarse "does this DAY touch a holiday AT ALL" check (the old
        // CompositeDutyEvaluators.RegularNightDiff/NonHolidayEvaluator gate) incorrectly dropped
        // real night-diff minutes that genuinely fall on the shift's non-holiday date/portion —
        // just a threshold gate is needed, matching every other EvalND-pattern field.
        var nightDiffEvaluator = DutyTypeMapFactory.Create[DayType.NIGHT_DIFF];
        return nightDiffEvaluator.Evaluate(nd, context);
    }
}
