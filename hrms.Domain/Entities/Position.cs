namespace Hrms.Domain.Entities;

public class Position : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double Rate { get; set; } = 0;
}
