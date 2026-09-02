using NetTopologySuite.Geometries;

namespace Hrms.Domain.Entities;

public class CostCenters : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public Polygon? Boundary { get; set; }
    // Which Branch this Project Site belongs to — nullable at the DB level so existing rows
    // (created before this field existed) don't break, but the Project Site setup screen
    // requires it for new/edited records. Every Branch-then-Area cascading filter across the
    // app (Employee, DTR, Attendance, Biometric Devices, etc.) filters Area options by this.
    public Guid? BranchId { get; set; }
    public virtual Branch? Branch { get; set; }
}
