
using BuzlinkRepository;

namespace Hrms.Domain.Entities;

public abstract class BaseEntity : EntityBase, IEntityTenant
{
    public Guid TenantId { get; set; }
    public string Status { get; set; } = "Active";
}

public interface IPostedFilter
{
    public bool IsPosted { get; set; }
}

public interface IDateRangeFilter
{
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
}
public interface IDateFilter
{
    public DateOnly PayrollDate { get; set; }
}


