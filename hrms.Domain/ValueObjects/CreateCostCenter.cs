using NetTopologySuite.Geometries;

namespace Hrms.Domain.ValueObjects;

public class CreateCostCenter
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public Polygon? Boundary { get; set; }
    public string Status { get; set; } = "Active";
    public Guid? BranchId { get; set; }
}

public class UpdateCostCenter : CreateCostCenter
{
    public Guid Id { get; set; }
}

public class CostCenterModel : UpdateCostCenter
{
    // Denormalized for the Project Site list/table — avoids a separate Branches lookup just
    // to show which branch each row belongs to. Populated via MappingProfile.cs since the
    // property names don't match CostCenters.Branch.Name/Code for Mapster's convention map.
    public string? BranchName { get; set; }
    public string? BranchCode { get; set; }
}