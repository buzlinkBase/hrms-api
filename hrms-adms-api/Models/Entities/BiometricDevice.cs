namespace Hrms.adms.Models.Entities;

public class BiometricDevice : BaseEntity, IEntityTenant
{
    public required string SN { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? DepartmentId { get; set; }
    public string Status { get; set; } = "Active";
}
