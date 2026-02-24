namespace DTR.Core;

public class RegularHolidayStrategy : IHolidayFilterStrategy
{
    public bool IsApplicable(HolidayInfo holiday, EmployeeDTRRun employee)
        => holiday.HolType == HolidayType.LEGAL;
     
}