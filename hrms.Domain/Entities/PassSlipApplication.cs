using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Domain.Entities;

public class PassSlipApplication : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public virtual Employee? Employee { get; set; }
    public DateOnly ApplicationDate { get; set; }
    public DateTime DepartureTime { get; set; }
    public DateTime? ReturnTime { get; set; }
    public string Destination { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public ApprovalStatus ApprovalStatus { get; set; }
    public string? Remarks { get; set; }
    public string BatchCode { get; set; } = string.Empty;
}
