using Hrms.Domain.Entities.EmployeeEntities;
namespace Hrms.Domain.Entities;

public class WorkSchedulePlan : BaseEntity
{
    public string BatchCode { get; set; }
    public DateOnly PayrollDate { get; set; }
    public Guid EmployeeId { get; set; }
    public virtual Employee Employee { get; set; }
    public Guid TimeShiftId { get; set; }
    public virtual TimeShift TimeShift { get; set; }
}
