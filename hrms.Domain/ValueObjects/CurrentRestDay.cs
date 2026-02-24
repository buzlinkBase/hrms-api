
namespace Hrms.Domain.ValueObjects;

public class CurrentRestDay
{
    public EmployeeDTRRun Employee { get; set; }
    public DateOnly PayrollDate { get; set; }
    public DayName DayName { get; set; }
    public ChangeSchedState State { get; set; }
}
