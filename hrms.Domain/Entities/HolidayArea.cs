using BuzlinkRepository;

namespace Hrms.Domain.Entities;

// One project site a localized special holiday applies to. A special holiday with no rows is
// nationwide; legal holidays never have rows.
[DisableSoftDelete]
public class HolidayArea : BaseEntity
{
    public Guid HolidayId { get; set; }
    public virtual Holiday? Holiday { get; set; }

    public Guid AreaId { get; set; }
    public virtual CostCenters? Area { get; set; }
}
