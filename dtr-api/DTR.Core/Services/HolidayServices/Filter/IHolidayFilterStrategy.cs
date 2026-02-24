namespace DTR.Core;

public interface IHolidayFilterStrategy
{
    bool IsApplicable(HolidayInfo holiday, EmployeeDTRRun employee);
}
