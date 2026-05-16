using NetTopologySuite.Geometries;

namespace Hrms.Domain.Entities;

public class CostCenters : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public Polygon? Boundary { get; set; }
}
