using BuzlinkRepository;

namespace Hrms.Domain.Entities.EmployeeEntities;

[DisableSoftDelete]
public class EmploymentHistory : BaseEntity, IOptionalEmployeeOwned
{
    public Guid? EmployeeId { get; set; }
    //public virtual Employee? Employee { get; set; }
    public string CompanyName { get; set; }
    public string Position { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
}
