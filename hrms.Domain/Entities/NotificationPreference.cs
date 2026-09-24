using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Domain.Entities;

// Per-employee, per-application-type opt-out of a notification channel. Reuses
// ApprovalApplicationType as the category so it stays consistent with how the approval engine
// already categorizes every notification-worthy event -- no separate taxonomy needed. Absence
// of a row for a given (EmployeeId, ApplicationType) means both channels stay on (opt-out
// model), so introducing this table needs no backfill for existing employees -- see
// NotificationPreferenceService.ResolveDeliveryFlags.
public class NotificationPreference : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public virtual Employee? Employee { get; set; }

    public ApprovalApplicationType ApplicationType { get; set; }

    public bool EmailEnabled { get; set; } = true;
    public bool PushEnabled { get; set; } = true;
}
