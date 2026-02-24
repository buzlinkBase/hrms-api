using Hrms.Domain.Entities.EmployeeEntities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Hrms.Domain.Entities;

public class WorkSchedulePlan : BaseEntity
{
    [Required]
    public DateOnly PayrollDate { get; set; }
    public Guid EmployeeId { get; set; }
    [ForeignKey("EmployeeId")]
    public virtual Employee Employee { get; set; }
    public Guid TimeShiftId { get; set; }
    [ForeignKey("TimeShiftId")]
    public virtual TimeShift TimeShift { get; set; }
}
