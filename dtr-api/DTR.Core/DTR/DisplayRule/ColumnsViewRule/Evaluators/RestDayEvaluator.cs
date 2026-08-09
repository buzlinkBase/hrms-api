namespace DTR.Core.DTR.DisplayRule.ColumnsViewRule.Evaluators;

public class RestDayEvaluator : IDutyDayEvaluator
{
    public TimeRange Evaluate(TimeRange range, DisplayContext context)
    {
        if (!context.TimeContext.IsRestDay() ||
            context.TimeContext.IsLegalHoliday() ||
            context.TimeContext.IsSpecialNonWorking())
        {
            return TimeRange.Empty;
        }
        return range;
    }
}
public class RegularDayEvaluator : IDutyDayEvaluator
{
    public TimeRange Evaluate(TimeRange range, DisplayContext context)
    {
        if (context.TimeContext.IsRestDay() ||
              context.TimeContext.IsLegalHoliday() ||
              context.TimeContext.IsSpecialNonWorking())
        {
            return TimeRange.Empty;
        }
        return range;
    }
}
public class RegularOverTimeEvaluator : IDutyDayEvaluator
{
    public TimeRange Evaluate(TimeRange range, DisplayContext context)
    {
        if (context.TimeContext.IsLegalHoliday() ||
            context.TimeContext.IsSpecialNonWorking())
        {
            return TimeRange.Empty;
        }

        // Ensure the actual-basis holiday/OT split has run before reading its remainder.
        // HolidayOTFactory's provider memoizes via the ledger, so calling it here is safe
        // and cheap regardless of whether DisplayRule has already triggered it.
        var provider = HolidayOTFactory.Create(context.TimeContext, context.PipeLineResult.OT);
        provider.Calculate(HolidayType.LEGAL);
        provider.Calculate(HolidayType.SPECIAL);

        var legalOTAdditionalRange = context.TimeContext.Payload.Ledger.GetByTag($"REGULAR_OT_ACTUAL_{HolidayType.LEGAL}", context.TimeContext);
        var specialOTAdditionalRange = context.TimeContext.Payload.Ledger.GetByTag($"REGULAR_OT_ACTUAL_{HolidayType.SPECIAL}", context.TimeContext);
        var additionalRange = legalOTAdditionalRange + specialOTAdditionalRange;
        return range + additionalRange;
    }
}
public class RestLegalEvaluator : IDutyDayEvaluator
{
    public TimeRange Evaluate(TimeRange range, DisplayContext context)
    {
        if (context.TimeContext.IsRestDay() && context.TimeContext.IsLegalHoliday())
        {
            var Plus8 = context.PipeLineResult.Plus8;
            return new TimeRange(Plus8.TotalMinutes + range.TotalMinutes, range.TimeRecords);
        }
        return TimeRange.Empty;
    }
}
public class RestLegalOTEvaluator : IDutyDayEvaluator
{
    public TimeRange Evaluate(TimeRange range, DisplayContext context)
    {
        if (!context.TimeContext.IsRestDay() && !context.TimeContext.IsLegalHoliday())
        {
            return TimeRange.Empty;
        }
        return range;
    }
}
public class RestSpecialEvaluator : IDutyDayEvaluator
{
    public TimeRange Evaluate(TimeRange range, DisplayContext context)
    {
        if (context.TimeContext.IsRestDay() && context.TimeContext.IsSpecialNonWorking())
        {
            return range;
        }
        return TimeRange.Empty;
    }
}
public class LegalHolidayOTEvaluator : IDutyDayEvaluator
{
    public TimeRange Evaluate(TimeRange range, DisplayContext context)
    {
        if (!context.TimeContext.IsRestDay() && context.TimeContext.IsLegalHoliday())
        {
            return range;
        }
        return TimeRange.Empty;
    }
}
public class SpecialHolidayOTEvaluator : IDutyDayEvaluator
{
    public TimeRange Evaluate(TimeRange range, DisplayContext context)
    {
        if (!context.TimeContext.IsRestDay() && context.TimeContext.IsSpecialNonWorking())
        {
            return range;
        }
        return TimeRange.Empty;
    }
}
public class RestHolidayOTEvaluator : IDutyDayEvaluator
{
    public TimeRange Evaluate(TimeRange range, DisplayContext context)
    {
        if (!context.TimeContext.IsRestDay()) return TimeRange.Empty;
        return range;
    }
}
public class NightDiffEvaluator : IDutyDayEvaluator
{
    public TimeRange Evaluate(TimeRange range, DisplayContext context)
    {
        return context.TimeContext.Payload.Data.CompanyPolicy.NightDiffThreshold <= range.TotalMinutes
            ? range : TimeRange.Empty;
    }
}
public class NonHolidayEvaluator : IDutyDayEvaluator
{
    public TimeRange Evaluate(TimeRange range, DisplayContext context)
    {
        if (context.TimeContext.IsHoliday()) return TimeRange.Empty;
        return range;
    }
}
public static class DutyTypeMapFactory
{
    public static readonly Dictionary<DayType, IDutyDayEvaluator> Create = new()
    {
        { DayType.REGULAR, new RegularDayEvaluator()},
        { DayType.RESTDAY, new RestDayEvaluator() },
        { DayType.REGULAR_OT, new RegularOverTimeEvaluator()},
        { DayType.LEGAL_HOLIDAY_OVERTIME, new LegalHolidayOTEvaluator()},
        { DayType.SPECIAL_HOLIDAY_OVERTIME, new SpecialHolidayOTEvaluator()},
        { DayType.NIGHT_DIFF, new NightDiffEvaluator()},
        { DayType.NONHOLIDAY, new NonHolidayEvaluator()},
        { DayType.RESTLEGAL, new RestLegalEvaluator()},
        { DayType.RESTSPECIAL, new RestSpecialEvaluator()},
        { DayType.REST_HOL_OT, new RestHolidayOTEvaluator()},
    };
}
// Decorator: applies two duty-day evaluators in sequence, e.g.
// "keep only regular-day time" then "keep only its overtime portion".
public class SequentialDutyDayEvaluator : IDutyDayEvaluator
{
    private readonly IDutyDayEvaluator _first;
    private readonly IDutyDayEvaluator _second;
    public SequentialDutyDayEvaluator(IDutyDayEvaluator first, IDutyDayEvaluator second)
    {
        _first = first;
        _second = second;
    }
    public TimeRange Evaluate(TimeRange range, DisplayContext context)
	{
        if (!_first.Evaluate(range, context).IsEmpty())
        {
           return _second.Evaluate(range, context);
        }
        return TimeRange.Empty;
    } 
}
// Named, reusable two-step compositions used by DTRDetailColumnDisplayProcessor.
public static class CompositeDutyEvaluators
{
    public static readonly IDutyDayEvaluator RegularOvertime =
        new SequentialDutyDayEvaluator(DutyTypeMapFactory.Create[DayType.REGULAR], DutyTypeMapFactory.Create[DayType.REGULAR_OT]);
    public static readonly IDutyDayEvaluator RestOvertime =
        new SequentialDutyDayEvaluator(DutyTypeMapFactory.Create[DayType.RESTDAY], DutyTypeMapFactory.Create[DayType.REGULAR_OT]);
    public static readonly IDutyDayEvaluator RestLegalOvertime =
        new SequentialDutyDayEvaluator(DutyTypeMapFactory.Create[DayType.RESTLEGAL], DutyTypeMapFactory.Create[DayType.REST_HOL_OT]);
    public static readonly IDutyDayEvaluator RestSpecialOvertime =
        new SequentialDutyDayEvaluator(DutyTypeMapFactory.Create[DayType.RESTSPECIAL], DutyTypeMapFactory.Create[DayType.REST_HOL_OT]);
    public static readonly IDutyDayEvaluator RegularNightDiff =
       new SequentialDutyDayEvaluator(DutyTypeMapFactory.Create[DayType.NIGHT_DIFF], DutyTypeMapFactory.Create[DayType.NONHOLIDAY]);


}
public interface IDutyDayEvaluator
{
    TimeRange Evaluate(TimeRange range, DisplayContext context);
}

