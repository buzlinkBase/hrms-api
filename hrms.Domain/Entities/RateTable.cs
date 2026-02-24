using BuzlinkRepository;

namespace Hrms.Domain.Entities;

[DisableSoftDelete]
public class RateTable : BaseEntity
{
    public RateType Type { get; set; }
    public string ShortDescription { get; set; }
    public string Description { get; set; }
    public decimal Rate { get; set; }
    public int Remarks { get; set; }
}
