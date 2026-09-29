using BuzlinkRepository;

namespace Hrms.Domain.Entities.Announcements;

// One employee's copy of a published announcement -- both their saved copy (so they can reopen
// it later) and the publisher's read/acknowledgement record. Name/department/position/branch
// are snapshots taken at publish time, so reports stay stable when people move or leave. No FK
// to Employee on purpose: the receipt outlives the employee record.
[DisableSoftDelete]
public class AnnouncementRecipient : BaseEntity
{
    public Guid AnnouncementId { get; set; }
    public virtual Announcement? Announcement { get; set; }

    public Guid EmployeeId { get; set; }
    public Guid? UserId { get; set; }
    public string? Email { get; set; }
    public string EmployeeNo { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string PositionName { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;

    public DateTime DeliveredAt { get; set; }
    public DateTime? FirstViewedAt { get; set; }
    public DateTime? LastViewedAt { get; set; }
    public int ViewCount { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    // The announcement Version this person acknowledged -- compared with
    // Announcement.AckRequiredVersion, so a re-acknowledgement request keeps the old record.
    public int? AcknowledgedVersion { get; set; }
    public DateTime? LastRemindedAt { get; set; }
    public int RemindCount { get; set; }
}
