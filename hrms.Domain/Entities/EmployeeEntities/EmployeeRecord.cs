using BuzlinkRepository;

namespace Hrms.Domain.Entities.EmployeeEntities;

[DisableSoftDelete]
public class EmployeeRecord : BaseEntity
{
    public Guid? EmployeeId { get; set; }
    //public virtual Employee? Employee { get; set; }
    public string RecordType { get; set; }
    public string Description { get; set; }
    public string File { get; set; }
}
