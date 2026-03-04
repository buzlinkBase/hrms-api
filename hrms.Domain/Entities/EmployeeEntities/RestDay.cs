using BuzlinkRepository;

namespace Hrms.Domain.Entities.EmployeeEntities;

public class RestDay : BaseEntity
{
    public DayName DayName { get; set; }
    public Guid EmployeeId { get; set; }
    public virtual Employee? Employee { get; set; }
}

public class RestDayDate : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public virtual Employee? Employee { get; set; }
    public DateOnly PayrollDate { get; set; }
}