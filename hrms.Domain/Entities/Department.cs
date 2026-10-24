using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Domain.Entities;

public class Department : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    // Org-chart structure: nests departments (Division > Department) and names who leads this one.
    public Guid? ParentDepartmentId { get; set; }
    public Guid? HeadId { get; set; }
    public virtual ICollection<Employee> Employees { get; set; }
}
