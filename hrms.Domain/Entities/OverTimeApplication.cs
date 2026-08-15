using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Domain.Entities;

public class OverTimeApplication : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public required virtual Employee Employee { get; set; }
    public DateOnly OTDate { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public double ManualOTMinutes { get; set; }
    public bool IsManualEntry { get; set; }
    public double OverTimeThreshold { get; set; } = 0;
    public string Remarks { get; set; } = string.Empty;
    public ApprovalStatus ApprovalStatus { get; set; }
}

