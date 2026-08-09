
using BuzlinkRepository;
using Mapster;

namespace Hrms.Domain.Entities;

public abstract class BaseEntity : EntityBase, IEntityTenant, ITimeStamp
{
    public string Status { get; set; } = "Active";

    [AdaptIgnore]
    public Guid TenantId { get; set; }
    [AdaptIgnore]
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    [AdaptIgnore]
    public DateTime? DeletedAt { get; set; }

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


