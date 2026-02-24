namespace Hrms.Domain.Entities;

public class Section : BaseEntity
{
    public Guid? DepartmentId { get; set; }
    public virtual Department? Department { get; set; }
    public string SectionType { get; set; } = "Section";
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
