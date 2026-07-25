namespace DTR.Core;

public class SpecialAreaHolidayStrategy : IHolidayFilterStrategy
{
    public bool IsApplicable(HolidayInfo holiday, EmployeeDTRRun employee)
    {
        if (holiday.HolType != HolidayType.SPECIAL) return false; 
        // 1. If the holiday is tied to a specific Area, enforce the Area match.
        if (holiday.AreaId.HasValue && holiday.AreaId.Value != Guid.Empty)
        {
            return employee.AreaId.HasValue && holiday.AreaId.Value == employee.AreaId.Value;
        }
        return  false;
    }
}