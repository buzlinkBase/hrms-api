namespace Hrms.adms.Models.Entities;

public class BaseEntity : EntityBase, IEntityTenant
{
    public Guid TenantId { get; set; }
}
