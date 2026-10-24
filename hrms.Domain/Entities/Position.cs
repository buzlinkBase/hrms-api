namespace Hrms.Domain.Entities;

public class Position : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double Rate { get; set; } = 0;
    // Seniority for the org chart; a higher number sits higher in its unit.
    public int Level { get; set; } = 0;
}
