using BuzlinkRepository;
using Onepunch.AuditEngine;

namespace Hrms.Domain.Entities.Announcements;

// A notice a publisher sends to a set of employees, with per-recipient read/acknowledgement
// tracking (AnnouncementRecipient). Soft delete is disabled: the "Recently deleted" bin is its
// own State (so binned rows stay queryable for restore), and a never-sent draft, or a binned one
// past retention, is removed for real.
[DisableSoftDelete]
[Audited]
public class Announcement : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    // Plain text -- line breaks are kept and URLs are made clickable when displayed.
    public string Body { get; set; } = string.Empty;
    public AnnouncementPriority Priority { get; set; } = AnnouncementPriority.Normal;

    public bool RequiresAcknowledgement { get; set; }
    public DateTime? AcknowledgeByUtc { get; set; }

    // When a Scheduled announcement auto-publishes (see AnnouncementLifecycleScheduler).
    public DateTime? PublishAtUtc { get; set; }
    // Past this, a Published announcement is auto-archived.
    public DateTime? ExpiresAtUtc { get; set; }

    public bool IsPinned { get; set; }
    public DateTime? PinnedUntilUtc { get; set; }

    // Leaves Terminated/Resigned/Retired/Deceased employees out of the audience.
    public bool ExcludeInactive { get; set; } = true;

    public AnnouncementState State { get; set; } = AnnouncementState.Draft;
    // Restore targets: the state an announcement had before it was archived / binned.
    public AnnouncementState? StateBeforeArchive { get; set; }
    public AnnouncementState? StateBeforeDelete { get; set; }

    public Guid? CreatedByUserId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime? PublishedAtUtc { get; set; }
    public Guid? PublishedByUserId { get; set; }
    public string? PublishedByName { get; set; }
    public DateTime? UnpublishedAtUtc { get; set; }
    public string? UnpublishedByName { get; set; }
    public string? UnpublishReason { get; set; }
    public DateTime? ArchivedAtUtc { get; set; }
    public DateTime? BinnedAtUtc { get; set; }

    // Starts at 1 and goes up on every edit made after publishing ("Edited" label).
    public int Version { get; set; } = 1;
    public DateTime? LastEditedAtUtc { get; set; }
    // A recipient counts as acknowledged when their AcknowledgedVersion reaches this. An edit
    // that asks for re-acknowledgement raises it to the new Version.
    public int AckRequiredVersion { get; set; } = 1;

    // Quartz jobs (via MassTransit's message scheduler) that will auto-publish / auto-archive /
    // purge this announcement -- kept so a reschedule, manual action or restore can cancel them.
    public Guid? PublishJobToken { get; set; }
    public Guid? ExpireJobToken { get; set; }
    public Guid? PurgeJobToken { get; set; }

    public virtual ICollection<AnnouncementAudienceRule> AudienceRules { get; set; } = new List<AnnouncementAudienceRule>();
    public virtual ICollection<AnnouncementRecipient> Recipients { get; set; } = new List<AnnouncementRecipient>();
}
