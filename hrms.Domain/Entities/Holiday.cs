using System.ComponentModel.DataAnnotations.Schema;

namespace Hrms.Domain.Entities;

public class Holiday : BaseEntity
{
    public string Description { get; set; } = string.Empty;
    public HolidayType HolType { get; set; } = HolidayType.LEGAL;
    public HolidayWorkType WorkType { get; set; } = HolidayWorkType.NonWorking;
    public int HolYear { get; set; }
    public DateOnly HolDate { get; set; }
    public bool IsRecuring { get; set; }
    public bool IsPaid { get; set; }
    public Guid? AreaId { get; set; }
    //[ForeignKey(nameof(AreaId))]
    public virtual CostCenters Area { get; set; }
}
