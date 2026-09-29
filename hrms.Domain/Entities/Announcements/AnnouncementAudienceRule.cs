using BuzlinkRepository;

namespace Hrms.Domain.Entities.Announcements;

[DisableSoftDelete]
public class AnnouncementAudienceRule : BaseEntity
{
    public Guid AnnouncementId { get; set; }
    public virtual Announcement? Announcement { get; set; }

    public AudienceRuleKind Kind { get; set; }
    // The department/position/branch/project site/client/section/employee id this rule targets.
    public Guid? TargetId { get; set; }
    // Non-id targets -- the EmploymentStatus name for an EmploymentStatus rule.
    public string? TargetValue { get; set; }
    // Display name at the time the rule was saved (e.g. "Operations Department").
    public string TargetLabel { get; set; } = string.Empty;
    // Removes its matches from the audience instead of adding them.
    public bool IsExclusion { get; set; }
}
