using NPOI.HSSF.Record;

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

        // GetCurrentSpecialHoliday already narrows to the single HolidayInfo relevant to the
        // configured HolidayTimeBasis (CurrentDate-matching for BasedOnTimeInDayType; whichever
        // date the shift actually overlaps for BasedOnActualWorkHours) — re-requiring a
        // CurrentDate match here on top of that used to silently defeat the ActualWorkHours
        // branch whenever a boundary-crossing shift's Special holiday fell on its non-anchor
        // date, even though that date's overlap was already correctly identified.
        var info = GetCurrentSpecialHoliday(context)
            .FirstOrDefault(x => x?.WorkType == HolidayWorkType.Working);
        var result = info == null ? false : true;
        context.Payload.SharedSpecCache.RecordTag(nameof(IsSpecialWorking), context, result);
        return result;

    }
    internal static bool IsSpecialNonWorking(this TimeContext context)
    {
        var cached = context.Payload.SharedSpecCache.GetByTag(nameof(IsSpecialNonWorking), context);
        if (cached.Found)
            return cached.Value;
        // See IsSpecialWorking's comment — same reasoning.
        var info = GetCurrentSpecialHoliday(context)
            .FirstOrDefault(x => x?.WorkType == HolidayWorkType.NonWorking);
        var result = info == null ? false : true;
        context.Payload.SharedSpecCache.RecordTag(nameof(IsSpecialNonWorking), context, result);
        return result;
    }

    private static List<HolidayInfo?> GetCurrentSpecialHoliday(TimeContext context)
    {
        var infos = context.Payload.Provider.HolidayProvider.GetHolidayInfoDuringShift(
                HolidayType.SPECIAL,
                context.Payload.Data.Employee,
                context.Payload.Data.CurrentShift);

        // Mirrors IsHolidaySpec/HolidayPolicyProviderFactory/HolidayOTFactory's basis handling
        // — a shift crossing a holiday's date boundary needs the same BasedOnTimeInDayType vs
        // BasedOnActualWorkHours distinction for Special Holiday classification that those
        // three already apply. BasedOnTimeInDayType: only the info matching the shift's anchor
        // date (CurrentDate) counts. BasedOnActualWorkHours: take whichever date the shift
        // actually overlaps last, per GetHolidayInfoDuringShift's own PayrollDate ordering.
        if (context.Payload.Data.CompanyPolicy.HolidayTimeBasis == HolidayTimeBasis.BasedOnTimeInDayType)
        {
            var first = infos.FirstOrDefault();
            return first != null && first.PayrollDate == context.Payload.Data.CurrentDate
                ? new List<HolidayInfo?> { first }
                : new List<HolidayInfo?>();
        }

        var last = infos.LastOrDefault();
        return last != null ? new List<HolidayInfo?> { last } : new List<HolidayInfo?>();
    }
    internal static bool IsRestDay(this TimeContext context)
    {
        return new IsRestDaySpec().IsSatisfiedBy(TimeRange.Empty, context);
    }
}
