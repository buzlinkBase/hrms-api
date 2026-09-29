using Hrms.Core.Services.Announcements.Lifecycle;
using Hrms.Domain.Entities.Announcements;

namespace Hrms.Core.Services.Announcements;

// Who is doing a publisher action -- recorded on the announcement (published/unpublished by).
public sealed record AnnouncementActor(Guid? UserId, string Name)
{
    public static readonly AnnouncementActor System = new(null, "System");
}

// ── Requests ────────────────────────────────────────────────────────────────

public class AudienceRuleRequest
{
    public AudienceRuleKind Kind { get; set; }
    public Guid? TargetId { get; set; }
    public string? TargetValue { get; set; }
    public string TargetLabel { get; set; } = string.Empty;
    public bool IsExclusion { get; set; }
}

public class AnnouncementUpsertRequest
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public AnnouncementPriority Priority { get; set; } = AnnouncementPriority.Normal;
    public bool RequiresAcknowledgement { get; set; }
    public DateTime? AcknowledgeByUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public bool IsPinned { get; set; }
    public DateTime? PinnedUntilUtc { get; set; }
    public bool ExcludeInactive { get; set; } = true;
    // Ignored once published -- the recipients were already snapshotted (use Duplicate instead).
    public List<AudienceRuleRequest> AudienceRules { get; set; } = [];
    // Only for an edit after publishing, when acknowledgement is required: everyone must
    // acknowledge the new version.
    public bool RequireReacknowledgement { get; set; }
}

public class AudiencePreviewRequest
{
    public List<AudienceRuleRequest> AudienceRules { get; set; } = [];
    public bool ExcludeInactive { get; set; } = true;
}

public class ScheduleAnnouncementRequest
{
    public DateTime PublishAtUtc { get; set; }
}

public class UnpublishAnnouncementRequest
{
    public string? Reason { get; set; }
}

public class PinAnnouncementRequest
{
    public DateTime? PinnedUntilUtc { get; set; }
}

public enum RemindTarget
{
    NotViewed,
    NotAcknowledged,
}

public class RemindAnnouncementRequest
{
    public RemindTarget Target { get; set; } = RemindTarget.NotViewed;
}

// Admin list tabs.
public enum AnnouncementListTab
{
    Active,
    Unpublished,
    Archived,
    Deleted,
}

public class AnnouncementListQuery
{
    public AnnouncementListTab Tab { get; set; } = AnnouncementListTab.Active;
    public string? Search { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public int Limit { get; set; } = 20;
}

public enum RecipientStatusFilter
{
    All,
    Viewed,
    NotViewed,
    Acknowledged,
    NotAcknowledged,
    Overdue,
    Unreachable,
}

public class RecipientListQuery
{
    public RecipientStatusFilter Status { get; set; } = RecipientStatusFilter.All;
    public string? Search { get; set; }
    public string? Department { get; set; }
    public string? Position { get; set; }
    public string? Branch { get; set; }
    // name | department | firstViewedAt | acknowledgedAt
    public string? SortField { get; set; }
    public string? SortOrder { get; set; }
    public int Page { get; set; } = 1;
    // 0 = everything (used by the Excel export).
    public int Limit { get; set; } = 20;
}

public enum MyAnnouncementFilter
{
    All,
    Unread,
    NeedsAcknowledgement,
    Archived,
}

public class MyAnnouncementQuery
{
    public MyAnnouncementFilter Filter { get; set; } = MyAnnouncementFilter.All;
    public int Page { get; set; } = 1;
    public int Limit { get; set; } = 20;
}

// ── Responses ───────────────────────────────────────────────────────────────

public class AudienceRuleResponse
{
    public Guid Id { get; set; }
    public AudienceRuleKind Kind { get; set; }
    public Guid? TargetId { get; set; }
    public string? TargetValue { get; set; }
    public string TargetLabel { get; set; } = string.Empty;
    public bool IsExclusion { get; set; }
}

public class AnnouncementSummaryResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public AnnouncementPriority Priority { get; set; }
    public AnnouncementState State { get; set; }
    public bool IsExpired { get; set; }
    public bool IsPinned { get; set; }
    public bool RequiresAcknowledgement { get; set; }
    public DateTime? AcknowledgeByUtc { get; set; }
    public DateTime? PublishAtUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public string? PublishedByName { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? UnpublishedAtUtc { get; set; }
    public DateTime? ArchivedAtUtc { get; set; }
    // When a binned announcement is purged for good.
    public DateTime? PurgeAtUtc { get; set; }
    public int Version { get; set; }
    public List<string> AudienceSummary { get; set; } = [];
    public int RecipientCount { get; set; }
    public int ViewedCount { get; set; }
    public int AcknowledgedCount { get; set; }
    public List<AnnouncementAction> AllowedActions { get; set; } = [];
}

public class AnnouncementDetailResponse : AnnouncementSummaryResponse
{
    public string Body { get; set; } = string.Empty;
    public DateTime? PinnedUntilUtc { get; set; }
    public bool ExcludeInactive { get; set; }
    public DateTime? LastEditedAtUtc { get; set; }
    public string? UnpublishedByName { get; set; }
    public string? UnpublishReason { get; set; }
    public List<AudienceRuleResponse> AudienceRules { get; set; } = [];
}

public class NameCount
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class AudiencePreviewResponse
{
    public int Total { get; set; }
    public List<string> Sample { get; set; } = [];
    public List<NameCount> ByDepartment { get; set; } = [];
}

public class BreakdownRow
{
    public string Name { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Viewed { get; set; }
    public int Acknowledged { get; set; }
}

public class DailyCount
{
    public DateOnly Date { get; set; }
    public int Count { get; set; }
}

public class AnnouncementStatsResponse
{
    public int Reach { get; set; }
    public int Viewed { get; set; }
    public int NotViewed { get; set; }
    public double ViewRate { get; set; }
    public bool RequiresAcknowledgement { get; set; }
    public int Acknowledged { get; set; }
    public int PendingAcknowledgement { get; set; }
    public double AcknowledgementRate { get; set; }
    public int Overdue { get; set; }
    // No login account yet -- can't open the announcement until one is linked.
    public int Unreachable { get; set; }
    public List<BreakdownRow> ByDepartment { get; set; } = [];
    public List<BreakdownRow> ByPosition { get; set; } = [];
    public List<BreakdownRow> ByBranch { get; set; } = [];
    public List<DailyCount> ViewsPerDay { get; set; } = [];
}

public class RecipientResponse
{
    public Guid EmployeeId { get; set; }
    public string EmployeeNo { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string PositionName { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public bool IsReachable { get; set; }
    public DateTime DeliveredAt { get; set; }
    public DateTime? FirstViewedAt { get; set; }
    public DateTime? LastViewedAt { get; set; }
    public int ViewCount { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public int? AcknowledgedVersion { get; set; }
    // Acknowledged the version currently required (false after a re-acknowledgement request).
    public bool IsAcknowledged { get; set; }
    public bool IsOverdue { get; set; }
    public int RemindCount { get; set; }
    public DateTime? LastRemindedAt { get; set; }
}

public class MyAnnouncementListItem
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Excerpt { get; set; } = string.Empty;
    public AnnouncementPriority Priority { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public string? PublishedByName { get; set; }
    public bool IsPinned { get; set; }
    public bool IsEdited { get; set; }
    public bool IsArchived { get; set; }
    public bool IsRead { get; set; }
    public bool RequiresAcknowledgement { get; set; }
    public DateTime? AcknowledgeByUtc { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public bool NeedsAcknowledgement { get; set; }
    // Acknowledged an earlier version; the publisher asked everyone to acknowledge the update.
    public bool NeedsReacknowledgement { get; set; }
    public bool IsOverdue { get; set; }
    public DateTime? LastRemindedAt { get; set; }
}

public class MyAnnouncementDetail : MyAnnouncementListItem
{
    public string Body { get; set; } = string.Empty;
    public DateTime? LastEditedAtUtc { get; set; }
    // Unpublished by the publisher -- Body is empty and these explain why.
    public bool IsWithdrawn { get; set; }
    public DateTime? WithdrawnAtUtc { get; set; }
    public string? WithdrawnByName { get; set; }
    public string? WithdrawReason { get; set; }
}

public class AnnouncementPushNotification
{
    public Guid AnnouncementId { get; set; }
    // Published | Updated | Reminder | Unpublished
    public string Kind { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public AnnouncementPriority Priority { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
