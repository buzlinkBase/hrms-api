namespace DTR.Core;

internal static class CommonExtensions
{
    internal static bool IsHoliday(this TimeContext context)
    {
        var holiday = new IsHolidaySpec(HolidayType.LEGAL)
        .Or(new IsHolidaySpec(HolidayType.SPECIAL))
        ;
        return holiday.IsSatisfiedBy(context.CanonicalTimeRange, context);
    }
    internal static bool IsLegalHoliday(this TimeContext context)
    {
        var holiday = new IsHolidaySpec(HolidayType.LEGAL);
        return holiday.IsSatisfiedBy(context.CanonicalTimeRange, context);
    }

    internal static bool IsDoubleHoliday(this TimeRange plus8)
    {
        var holCount = plus8.GetMetaData<int>("HolidayCount");
        return holCount > 1;
    }

    internal static bool IsDoubleLegalHoliday(this TimeContext context)
    {
        var currentDate = context.Payload.Data.CurrentShift.ShiftDate;
        var holidays = context.Payload.Provider.HolidayProvider
            .GetHolidayDuringDate(HolidayType.LEGAL, context.Payload.Data.Employee, currentDate);
        return holidays.Count() > 1;
    }

    internal static bool IsSpecialHoliday(this TimeContext context)
    {
        var holiday = new IsHolidaySpec(HolidayType.SPECIAL);
        return holiday.IsSatisfiedBy(context.CanonicalTimeRange, context);
    }
    internal static bool IsSpecialWorking(this TimeContext context)
    {
        var cached = context.Payload.SharedSpecCache.GetByTag(nameof(IsSpecialWorking), context);
        if (cached.Found)
            return cached.Value;

        var result = GetCurrentSpecialHoliday(context)?.WorkType == HolidayWorkType.Working;
        context.Payload.SharedSpecCache.RecordTag(nameof(IsSpecialWorking), context, result);
        return result;
    }
    internal static bool IsSpecialNonWorking(this TimeContext context)
    {
        var cached = context.Payload.SharedSpecCache.GetByTag(nameof(IsSpecialNonWorking), context);
        if (cached.Found)
            return cached.Value;

        var result = GetCurrentSpecialHoliday(context)?.WorkType == HolidayWorkType.NonWorking;
        context.Payload.SharedSpecCache.RecordTag(nameof(IsSpecialNonWorking), context, result);
        return result;
    }

    private static HolidayInfo? GetCurrentSpecialHoliday(TimeContext context)
    {
        return context.Payload.Provider.HolidayProvider.GetHolidayInfoDuringShift(
            HolidayType.SPECIAL,
            context.Payload.Data.Employee,
            context.Payload.Data.CurrentShift);
    }
    internal static bool IsRestDay(this TimeContext context)
    {
        return new IsRestDaySpec().IsSatisfiedBy(TimeRange.Empty, context);
    }
}
