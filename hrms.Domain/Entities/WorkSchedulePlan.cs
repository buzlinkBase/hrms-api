using Hrms.Domain.Entities.EmployeeEntities;
namespace Hrms.Domain.Entities;

public class WorkSchedulePlan : BaseEntity, IEmployeeOwned
{
    public string BatchCode { get; set; }
    public DateOnly PayrollDate { get; set; }
    public Guid EmployeeId { get; set; }
    public virtual Employee Employee { get; set; }
    public Guid TimeShiftId { get; set; }
    public virtual TimeShift TimeShift { get; set; }
    // Project site the employee reports to on this date. When set, the DTR for the day is
    // recorded under this site (and its branch/client) instead of the punch's or the
    // employee's own; null keeps today's behavior.
    public Guid? OperationAreaId { get; set; }
    public virtual CostCenters? OperationArea { get; set; }
}
