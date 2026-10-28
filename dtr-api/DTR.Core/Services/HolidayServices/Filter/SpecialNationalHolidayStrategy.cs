namespace DTR.Core;

public class SpecialNationalHolidayStrategy : IHolidayFilterStrategy
{
    public bool IsApplicable(HolidayInfo holiday, Guid? dayAreaId)
        => holiday.HolType == HolidayType.SPECIAL && holiday.AreaIds.Count == 0;
}
