namespace DTR.Core.DTR.DisplayRule.ColumnsViewRule.Evaluators;

public class RestDayEvaluator : IDutyDayEvaluator
{
    public TimeRange Evaluate(TimeRange range, DisplayContext context)
    {
        if (!context.TimeContext.IsRestDay())
        {
            return TimeRange.Empty;
        }
        // A day touching a Legal/Special holiday is normally excluded entirely — except
        // under BasedOnActualWorkHours, where `range` (pipeline.Regular, via
        // NonHolidayDutyTimePipeline) already holds only the portion of a boundary-crossing
        // shift that does NOT overlap the holiday. Trust that value instead of re-deriving
        // from the coarse "does this shift touch the holiday at all" booleans, which would
        // otherwise discard a real, already-correctly-computed remainder. Under
        // BasedOnTimeInDayType, `range` is always Empty here (TimeInDayTypeProvider always
        // zeroes the non-holiday portion), so this reduces to the original all-or-nothing
        // behavior unchanged.
        if ((context.TimeContext.IsLegalHoliday() || context.TimeContext.IsSpecialNonWorking())
            && range.IsEmpty())
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
        if (context.TimeContext.IsRestDay())
        {
            return TimeRange.Empty;
        }
        // See RestDayEvaluator's comment — same reasoning, mirrored for a non-rest day.
        if ((context.TimeContext.IsLegalHoliday() || context.TimeContext.IsSpecialNonWorking())
            && range.IsEmpty())
        {
            return TimeRange.Empty;
        }

        var WorkingHoliday = context.PipeLineResult.SpecialHoliday;
        return range + WorkingHoliday;

    }
}

public class RegularOverTimeEvaluator : IDutyDayEvaluator
{
    public TimeRange Evaluate(TimeRange range, DisplayContext context)
    {
        if (context.TimeContext.IsRestDay())
        {
            return TimeRange.Empty;
        }
        if (!context.TimeContext.IsLegalHoliday() && !context.TimeContext.IsSpecialNonWorking())
        {
            return range;
        }

        // Boundary-crossing shift: `range` (pipeline.OT) is the whole shift's unsplit OT, so
        // it can't be added to the remainder below (that would double-count the non-holiday
        // portion). ActualDayOTHourProvider records the isolated non-holiday OT remainder
        // under BasedOnActualWorkHours only when the OT genuinely overlaps the holiday — the
        // ledger key is never written under BasedOnTimeInDayType, so additionalRange stays
        // Empty there, preserving the original all-or-nothing behavior for that basis.
        var provider = HolidayOTFactory.Create(context.TimeContext, context.PipeLineResult.OT);
        provider.Calculate(HolidayType.LEGAL);
        provider.Calculate(HolidayType.SPECIAL);

        var legalOTAdditionalRange = context.TimeContext.Payload.Ledger.GetByTag($"REGULAR_OT_ACTUAL_{HolidayType.LEGAL}", context.TimeContext);
        var specialOTAdditionalRange = context.TimeContext.Payload.Ledger.GetByTag($"REGULAR_OT_ACTUAL_{HolidayType.SPECIAL}", context.TimeContext);
        return legalOTAdditionalRange + specialOTAdditionalRange;
    }
}

public class RestOverTimeEvaluator : IDutyDayEvaluator
{
    public TimeRange Evaluate(TimeRange range, DisplayContext context)
    {
        if (!context.TimeContext.IsRestDay())
        {
            return TimeRange.Empty;
        }
        if (!context.TimeContext.IsLegalHoliday() && !context.TimeContext.IsSpecialNonWorking())
        {
            return range;
        }

        // See RegularOverTimeEvaluator's comment — same reasoning, mirrored for a rest day.
        var provider = HolidayOTFactory.Create(context.TimeContext, context.PipeLineResult.OT);
        provider.Calculate(HolidayType.LEGAL);
        provider.Calculate(HolidayType.SPECIAL);

        var legalOTAdditionalRange = context.TimeContext.Payload.Ledger.GetByTag($"REGULAR_OT_ACTUAL_{HolidayType.LEGAL}", context.TimeContext);
        var specialOTAdditionalRange = context.TimeContext.Payload.Ledger.GetByTag($"REGULAR_OT_ACTUAL_{HolidayType.SPECIAL}", context.TimeContext);
        return legalOTAdditionalRange + specialOTAdditionalRange;
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
        if (context.TimeContext.IsRestDay())
        {
            return TimeRange.Empty;
        }
        // Working-type Special holidays never belong in this column (they route through
        // Regular via HolidayPlusRegularRule instead) — this exclusion is unconditional,
        // regardless of how much time PipeLineResult.SpecialHoliday holds.
        if (!context.TimeContext.IsSpecialNonWorking())
        {
            return TimeRange.Empty;
        }

        var specialHoliday = context.PipeLineResult.SpecialHoliday;
        // A day that's ALSO a Legal holiday is normally excluded entirely — except under
        // BasedOnActualWorkHours on a boundary-crossing shift, where the Legal and Special
        // holidays can genuinely fall on two different calendar dates and
        // PipeLineResult.SpecialHoliday already holds only the actual Special-holiday overlap
        // for its own date. Trust that value instead of the coarse "is this shift a legal
        // holiday AT ALL" boolean, mirroring RegularDayEvaluator/RestDayEvaluator's fix for the
        // same boundary-crossing case.
        if (context.TimeContext.IsLegalHoliday() && specialHoliday.IsEmpty())
        {
            return TimeRange.Empty;
        }
        return specialHoliday;
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

