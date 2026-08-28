using BuzlinkRepository;
using Microsoft.EntityFrameworkCore;

namespace Hrms.Domain.Entities;

[DisableSoftDelete]
public class ClientRateTable : BaseEntity
{
    public Guid ClientId { get; set; }
    public RateType Type { get; set; }
    [Precision(18, 2)]
    public decimal Rate { get; set; }
}
