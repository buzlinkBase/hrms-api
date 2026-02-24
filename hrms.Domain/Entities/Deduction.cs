namespace Hrms.Domain.Entities;

public class DeductionType : BaseEntity
{
    public string Description { get; set; } = string.Empty;
}

public class Deduction : BaseEntity
{
    public Guid? CategoryId { get; set; }
    public virtual DeductionType? Category { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int PriorityLevel { get; set; }
}
