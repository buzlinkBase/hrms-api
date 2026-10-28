namespace DTR.Core;

public class RegularHolidayStrategy : IHolidayFilterStrategy
{
    public bool IsApplicable(HolidayInfo holiday, Guid? dayAreaId)
        => holiday.HolType == HolidayType.LEGAL;

}