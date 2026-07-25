namespace DTR.Core;

public class RestDayEvaluator : IDutyDayEvaluator
{
    public TimeRange Evaluate(TimeRange range, DisplayContext context)
    => RestDayChecker.IsRestDay(context.TimeContext.Payload) ? range : TimeRange.Empty;
}

public class RegularDayEvaluator : IDutyDayEvaluator
{
    public TimeRange Evaluate(TimeRange range, DisplayContext context)
    {
        if (!RestDayChecker.IsRestDay(context.TimeContext.Payload) && !context.TimeContext.IsHoliday())
        {
            return range;
        }
        return TimeRange.Empty;
    }
}

public class RegRestOverTimeEvaluator : IDutyDayEvaluator
{
    public TimeRange Evaluate(TimeRange range, DisplayContext context)
    {
        var legalOTAdditionalRange = context.TimeContext.Payload.Ledger.GetByTag($"REGULAR_OT_ACTUAL_{HolidayType.LEGAL}", context.TimeContext);
        var specialOTAdditionalRange = context.TimeContext.Payload.Ledger.GetByTag($"REGULAR_OT_ACTUAL_{HolidayType.SPECIAL}", context.TimeContext);
        var additionalRange = legalOTAdditionalRange + specialOTAdditionalRange;
        return new NonHolidayEvaluator().Evaluate(range + additionalRange, context);
    }
}

public class NonHolidayEvaluator : IDutyDayEvaluator
{
    public TimeRange Evaluate(TimeRange range, DisplayContext context)
    {
        return !context.TimeContext.IsHoliday() ? range : TimeRange.Empty;
    }
}

public class RestLegalEvaluator : IDutyDayEvaluator
{
    public TimeRange Evaluate(TimeRange range, DisplayContext context)
    {
        if (context.TimeContext.IsRestDay() && context.TimeContext.IsLegalHoliday())
        {
            return range;
        }
        return  TimeRange.Empty;
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
        if (context.TimeContext.IsRestDay() && context.TimeContext.IsSpecialHoliday())
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
        if (!context.TimeContext.IsRestDay() && context.TimeContext.IsSpecialHoliday())
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



public static class DutyTypeMapFactory
{
    public static readonly Dictionary<DayType, IDutyDayEvaluator> Create = new()
    {
        { DayType.REGULAR, new RegularDayEvaluator()},
        { DayType.RESTDAY, new RestDayEvaluator() },
        { DayType.REGRESTOVERTIME, new RegRestOverTimeEvaluator()},
        { DayType.LEGAL_HOLIDAY_OVERTIME, new LegalHolidayOTEvaluator()},
        { DayType.SPECIAL_HOLIDAY_OVERTIME, new SpecialHolidayOTEvaluator()},
        { DayType.NIGHT_DIFF, new NightDiffEvaluator()},
        { DayType.NONHOLIDAY, new NonHolidayEvaluator()},
        { DayType.RESTLEGAL, new RestLegalEvaluator()},
        { DayType.RESTSPECIAL, new RestSpecialEvaluator()},
        { DayType.REST_HOL_OT, new RestHolidayOTEvaluator()},
    };
}

public interface IDutyDayEvaluator
{
    TimeRange Evaluate(TimeRange range, DisplayContext context);
}

