using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Domain.Entities;

public class Department : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? HeadId { get; set; }
    public virtual Employee? Head { get; set; }

}
