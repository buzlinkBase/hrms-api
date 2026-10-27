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
    // Optional: the one Client this Project Site is run for (and later billed to). Null for
    // companies that don't split clients by area, and for sites not yet tagged.
    public Guid? ClientId { get; set; }
    public virtual Client? Client { get; set; }
    // The employee who heads this site -- what "Applicant's project site head" resolves to.
    public Guid? HeadEmployeeId { get; set; }
}
