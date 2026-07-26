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
        return CompositeDutyEvaluators.RegularNightDiff.Evaluate(nd, context);

    }
}
