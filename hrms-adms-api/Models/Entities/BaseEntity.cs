namespace Hrms.adms.Models.Entities;

public class BaseEntity : EntityBase, IEntityTenant, ITimeStamp
{
    public Guid TenantId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
