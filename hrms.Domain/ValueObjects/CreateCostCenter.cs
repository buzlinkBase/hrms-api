using NetTopologySuite.Geometries;

namespace Hrms.Domain.ValueObjects;

public class CreateCostCenter
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public Polygon? Boundary { get; set; }
    public string Status { get; set; } = "Active";
}

public class UpdateCostCenter : CreateCostCenter
{
    public Guid Id { get; set; }
}

public class CostCenterModel : UpdateCostCenter
{
}