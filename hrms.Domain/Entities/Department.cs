using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Domain.Entities;

public class Department : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public virtual ICollection<Employee> Employees { get; set; }
}
