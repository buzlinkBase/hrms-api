using DTR.Core.DTR.DisplayRule.ColumnsViewRule.Evaluators;

namespace DTR.Core.DTR.DisplayRule.ColumnsViewRule.DisplayRules;

internal class LegalNightDiffRule : IColumnDisplayRule
{
    private readonly EvaluatedColumnResult _evaluated;
    public LegalNightDiffRule(EvaluatedColumnResult evaluated)
    {
        _evaluated = evaluated;
    }

    public TimeRange ApplyRules(DisplayContext context)
    {
        var nightDiffEvaluator = DutyTypeMapFactory.Create[DayType.NIGHT_DIFF];

        var holiday = context.PipeLineResult.LegalHoliday;
        var IsND = NightDiffChecker.IsDutyNightDiff(context.TimeContext.Payload.Data.CurrentShift.StartTime, context.TimeContext.Payload.Data.CurrentShift.StartTime);
        var topup = context.TimeContext.Payload.Ledger.GetByTag("RegularTimeTopUp", context.TimeContext);

        if (new IsHolidaySpec(HolidayType.LEGAL).Not().IsSatisfiedBy(context.TimeContext.CanonicalTimeRange, context.TimeContext))
        {
            return TimeRange.Empty;
        }

        if (new IsShowWorkOnHolidayInRegColumn().IsSatisfiedBy(context))
        {
            TimeRange worktime = context.TimeContext.Payload.Ledger.GetByTag("work_time", context.TimeContext);
            if (!IsND)
            {
                worktime = worktime.TimeRecords.Exclude(topup.TimeRecords)
                    .ToTimeRange();
            }
            var nightDiff = NightDiffCalculator.Calculate(worktime);
            return nightDiffEvaluator.Evaluate(nightDiff, context);
        }
        else
        {
            var nightDiff = NightDiffCalculator.Calculate(_evaluated.LegalHoliday);
            if (!IsND)
            {
                nightDiff = nightDiff.TimeRecords.Exclude(topup.TimeRecords)
                     .ToTimeRange();
            }
            return nightDiffEvaluator.Evaluate(nightDiff, context);
        }
    }
}
