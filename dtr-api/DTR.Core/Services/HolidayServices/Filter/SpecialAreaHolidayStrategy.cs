namespace DTR.Core;

public class SpecialAreaHolidayStrategy : IHolidayFilterStrategy
{
    // A localized special holiday applies when the day's site is one of its sites.
    public bool IsApplicable(HolidayInfo holiday, Guid? dayAreaId)
        => holiday.HolType == HolidayType.SPECIAL
            && holiday.AreaIds.Count > 0
            && dayAreaId is { } areaId
            && holiday.AreaIds.Contains(areaId);
}
