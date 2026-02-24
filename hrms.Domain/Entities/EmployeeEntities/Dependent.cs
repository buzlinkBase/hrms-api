
using BuzlinkRepository;

namespace Hrms.Domain.Entities.EmployeeEntities;

[DisableSoftDelete]
public class Dependent : BaseEntity
{
    public Guid? EmployeeId { get; set; }
    public string FullName { get; set; }
    public string Relationship { get; set; }
    public string Gender { get; set; }
    public DateOnly DOB { get; set; }
}
