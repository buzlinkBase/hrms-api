using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Domain.Entities;

public class ChangeRestDay : BaseEntity
{
    public DayName DayName { get; set; }
    public Guid EmployeeId { get; set; }
    public virtual Employee Employee { get; set; }
    public DateOnly PayrollDate { get; set; }
    public ChangeSchedState State { get; set; }
    public string BatchCode { get; set; }
}