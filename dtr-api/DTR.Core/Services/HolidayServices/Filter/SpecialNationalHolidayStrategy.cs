namespace DTR.Core;

public class SpecialNationalHolidayStrategy : IHolidayFilterStrategy
{
    public bool IsApplicable(HolidayInfo holiday, EmployeeDTRRun employee)
        => holiday.HolType == HolidayType.SPECIAL && holiday.AreaId == Guid.Empty;
}