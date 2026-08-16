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
        if (context.TimeContext.IsRestDay()
            || context.TimeContext.IsLegalHoliday()
            || context.TimeContext.IsSpecialNonWorking())
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

public class RestOverTimeEvaluator : IDutyDayEvaluator
{
    public TimeRange Evaluate(TimeRange range, DisplayContext context)
    {
        if (!context.TimeContext.IsRestDay()
            || context.TimeContext.IsLegalHoliday()
            || context.TimeContext.IsSpecialNonWorking())
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
        if (context.TimeContext.IsRestDay()
            && context.TimeContext.IsLegalHoliday()
            && !context.PipeLineResult.Plus8.IsDoubleHoliday())
        {
            return range;
        }
        return TimeRange.Empty;
    }

}

public class DoubleRestLegalEvaluator : IDutyDayEvaluator
{
    public TimeRange Evaluate(TimeRange range, DisplayContext context)
    {
        if (context.TimeContext.IsRestDay() && context.PipeLineResult.Plus8.IsDoubleHoliday())
        {
            return range;
        }
        return TimeRange.Empty;
    }

}
public class DoubleRestLegalHolidayOTEvaluator : IDutyDayEvaluator
{
    public TimeRange Evaluate(TimeRange range, DisplayContext context)
    {
        if (context.TimeContext.IsRestDay() && context.PipeLineResult.Plus8.IsDoubleHoliday())
        {
            return range;
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

public class RestSpecialHolidayOTEvaluator : IDutyDayEvaluator
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
public class LegalHolidayEvaluator : IDutyDayEvaluator
{
    public TimeRange Evaluate(TimeRange range, DisplayContext context)
    {
        if (!context.TimeContext.IsRestDay()
            && context.TimeContext.IsLegalHoliday()
            && !context.PipeLineResult.Plus8.IsDoubleHoliday())
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
        if (!context.TimeContext.IsRestDay()
            && context.TimeContext.IsLegalHoliday()
            && !context.PipeLineResult.Plus8.IsDoubleHoliday())
        {
            return range;
        }
        return TimeRange.Empty;
    }
}
public class SpecialHolidayEvaluator : IDutyDayEvaluator
{
    public TimeRange Evaluate(TimeRange range, DisplayContext context)
    {
        if (context.TimeContext.IsRestDay() || context.TimeContext.IsSpecialWorking())
        {
            return TimeRange.Empty;
        }
        return context.PipeLineResult.SpecialHoliday;
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

public class RestLegalHolidayOTEvaluator : IDutyDayEvaluator
{
    public TimeRange Evaluate(TimeRange range, DisplayContext context)
    {
        if (context.TimeContext.IsRestDay()
            && context.TimeContext.IsLegalHoliday()
            && !context.PipeLineResult.Plus8.IsDoubleHoliday())
        {
            return range;
        }
        return TimeRange.Empty;
    }
}

public class DoubleLegalHolidayEvaluator : IDutyDayEvaluator
{
    public TimeRange Evaluate(TimeRange range, DisplayContext context)
    {
        if (!context.TimeContext.IsRestDay() && context.PipeLineResult.Plus8.IsDoubleHoliday())
        {
            return range;
        }
        return TimeRange.Empty;
    }
}

public class DoubleLegalHolidayOTEvaluator : IDutyDayEvaluator
{
    public TimeRange Evaluate(TimeRange range, DisplayContext context)
    {
        if (!context.TimeContext.IsRestDay() && context.PipeLineResult.Plus8.IsDoubleHoliday())
        {
            return range;
        }
        return TimeRange.Empty;
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
    public static readonly IDutyDayEvaluator RegularNightDiff =
       new SequentialDutyDayEvaluator(DutyTypeMapFactory.Create[DayType.NIGHT_DIFF], DutyTypeMapFactory.Create[DayType.NONHOLIDAY]);
}

public interface IDutyDayEvaluator
{
    TimeRange Evaluate(TimeRange range, DisplayContext context);
}

