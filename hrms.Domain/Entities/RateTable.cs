using BuzlinkRepository;
using Microsoft.EntityFrameworkCore;

namespace Hrms.Domain.Entities;

[DisableSoftDelete]
public class RateTable : BaseEntity
{
    public RateType Type { get; set; }
    public string? ShortDescription { get; set; } = string.Empty;
    public string?  Description { get; set; } = string.Empty;
    [Precision(18, 2)]
    public decimal Rate { get; set; }
    public int Remarks { get; set; }
}
