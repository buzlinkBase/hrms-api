using System.Linq.Expressions;
using Hrms.Core.Services.Announcements.Lifecycle;
using Hrms.Domain.Entities.Announcements;

namespace Hrms.Core.Services.Announcements;

/// <summary>
/// Employee side of announcements: their saved copies (AnnouncementRecipient rows), opening one
/// (which records the view) and acknowledging. Always scoped to the caller's own EmployeeId,
/// resolved server-side by the controller -- never taken from the client.
/// </summary>
public class MyAnnouncementService : BaseService<AnnouncementRecipient>
{
    private const int ExcerptLength = 160;

    public MyAnnouncementService(IUnitOfWorkService uow) : base(uow)
    {
    }

    public async Task<PaginatedResult<List<MyAnnouncementListItem>>> GetMineAsync(Guid employeeId, MyAnnouncementQuery query, CancellationToken token)
    {
        var now = DateTime.UtcNow;
        var mine = query.Filter switch
        {
            MyAnnouncementFilter.Archived => Mine(employeeId).Where(r => r.Announcement!.State == AnnouncementState.Archived),
            MyAnnouncementFilter.Unread => Visible(employeeId, now).Where(r => r.FirstViewedAt == null),
            MyAnnouncementFilter.NeedsAcknowledgement => Visible(employeeId, now).Where(NeedsAcknowledgement()),
            _ => Visible(employeeId, now),
        };

        var total = await mine.CountAsync(token);
        var page = query.Page > 0 ? query.Page : 1;
        var limit = query.Limit is > 0 and <= 100 ? query.Limit : 20;

        var rows = await mine
            .OrderByDescending(r => r.Announcement!.IsPinned && (r.Announcement.PinnedUntilUtc == null || r.Announcement.PinnedUntilUtc > now))
            .ThenByDescending(r => r.LastRemindedAt ?? r.Announcement!.PublishedAtUtc)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(ToListItem(now))
            .ToListAsync(token);

        return new PaginatedResult<List<MyAnnouncementListItem>>
        {
            Data = rows,
            MetaData = new PaginationMetaData(total, page, limit),
        };
    }

    public Task<int> GetUnreadCountAsync(Guid employeeId, CancellationToken token) =>
        Visible(employeeId, DateTime.UtcNow).CountAsync(r => r.FirstViewedAt == null, token);

    public Task<List<MyAnnouncementListItem>> GetPendingAcknowledgementsAsync(Guid employeeId, CancellationToken token)
    {
        var now = DateTime.UtcNow;
        return Visible(employeeId, now)
            .Where(NeedsAcknowledgement())
            .OrderByDescending(r => r.Announcement!.Priority == AnnouncementPriority.Urgent)
            .ThenBy(r => r.Announcement!.AcknowledgeByUtc ?? DateTime.MaxValue)
            .Select(ToListItem(now))
            .ToListAsync(token);
    }

    /// <summary>
    /// Opens one of my announcements and records the view. An unpublished one returns the
    /// "withdrawn" notice instead of its body (and records nothing); one that was never mine, or
    /// is deleted, is not found.
    /// </summary>
    public async Task<MyAnnouncementDetail> OpenAsync(Guid employeeId, Guid announcementId, CancellationToken token)
    {
        var now = DateTime.UtcNow;
        var recipient = await LoadMineAsync(employeeId, announcementId, token);
        var a = recipient.Announcement!;

        if (a.State == AnnouncementState.Unpublished)
        {
            return new MyAnnouncementDetail
            {
                Id = a.Id,
                Title = a.Title,
                Priority = a.Priority,
                PublishedAtUtc = a.PublishedAtUtc,
                PublishedByName = a.PublishedByName,
                IsWithdrawn = true,
                WithdrawnAtUtc = a.UnpublishedAtUtc,
                WithdrawnByName = a.UnpublishedByName,
                WithdrawReason = a.UnpublishReason,
                IsRead = recipient.FirstViewedAt != null,
                AcknowledgedAt = recipient.AcknowledgedAt,
            };
        }

        recipient.FirstViewedAt ??= now;
        recipient.LastViewedAt = now;
        recipient.ViewCount += 1;
        await CommitChangesAsync(token);

        var detail = ToListItem(now).Compile()(recipient);
        return new MyAnnouncementDetail
        {
            Id = detail.Id,
            Title = detail.Title,
            Excerpt = detail.Excerpt,
            Priority = detail.Priority,
            PublishedAtUtc = detail.PublishedAtUtc,
            PublishedByName = detail.PublishedByName,
            IsPinned = detail.IsPinned,
            IsEdited = detail.IsEdited,
            IsArchived = detail.IsArchived,
            IsRead = true,
            RequiresAcknowledgement = detail.RequiresAcknowledgement,
            AcknowledgeByUtc = detail.AcknowledgeByUtc,
            AcknowledgedAt = detail.AcknowledgedAt,
            NeedsAcknowledgement = detail.NeedsAcknowledgement,
            NeedsReacknowledgement = detail.NeedsReacknowledgement,
            IsOverdue = detail.IsOverdue,
            LastRemindedAt = detail.LastRemindedAt,
            Body = a.Body,
            LastEditedAtUtc = a.LastEditedAtUtc,
        };
    }

    public async Task AcknowledgeAsync(Guid employeeId, Guid announcementId, CancellationToken token)
    {
        var now = DateTime.UtcNow;
        var recipient = await LoadMineAsync(employeeId, announcementId, token);
        var a = recipient.Announcement!;

        if (a.State != AnnouncementState.Published)
            throw new ValidationException("This announcement can no longer be acknowledged.");
        if (!a.RequiresAcknowledgement)
            throw new ValidationException("This announcement doesn't ask for acknowledgement.");
        if (recipient.AcknowledgedVersion >= a.AckRequiredVersion)
            throw new ValidationException("You already acknowledged this announcement.");

        recipient.AcknowledgedAt = now;
        recipient.AcknowledgedVersion = a.Version;
        recipient.FirstViewedAt ??= now;
        recipient.LastViewedAt ??= now;
        await CommitChangesAsync(token);
    }

    private IQueryable<AnnouncementRecipient> Mine(Guid employeeId) =>
        GetQueryable(r => r.EmployeeId == employeeId);

    // What shows in the feed and bell: published and not yet expired.
    private IQueryable<AnnouncementRecipient> Visible(Guid employeeId, DateTime now) =>
        Mine(employeeId).Where(r =>
            r.Announcement!.State == AnnouncementState.Published &&
            (r.Announcement.ExpiresAtUtc == null || r.Announcement.ExpiresAtUtc > now));

    private static Expression<Func<AnnouncementRecipient, bool>> NeedsAcknowledgement() =>
        r => r.Announcement!.RequiresAcknowledgement &&
             (r.AcknowledgedVersion == null || r.AcknowledgedVersion < r.Announcement.AckRequiredVersion);

    private async Task<AnnouncementRecipient> LoadMineAsync(Guid employeeId, Guid announcementId, CancellationToken token)
    {
        var recipient = await GetQueryable(r => r.EmployeeId == employeeId && r.AnnouncementId == announcementId, noTracking: false)
            .Include(r => r.Announcement)
            .FirstOrDefaultAsync(token);

        // Never-sent and binned announcements aren't the employee's to see.
        if (recipient?.Announcement == null ||
            AnnouncementStateMachine.IsNeverSent(recipient.Announcement.State) ||
            recipient.Announcement.State == AnnouncementState.Deleted)
            throw new NotFoundException("Announcement not found.");
        return recipient;
    }

    private static Expression<Func<AnnouncementRecipient, MyAnnouncementListItem>> ToListItem(DateTime now) => r => new MyAnnouncementListItem
    {
        Id = r.AnnouncementId,
        Title = r.Announcement!.Title,
        Excerpt = r.Announcement.Body.Length > ExcerptLength ? r.Announcement.Body.Substring(0, ExcerptLength) + "…" : r.Announcement.Body,
        Priority = r.Announcement.Priority,
        PublishedAtUtc = r.Announcement.PublishedAtUtc,
        PublishedByName = r.Announcement.PublishedByName,
        IsPinned = r.Announcement.IsPinned && (r.Announcement.PinnedUntilUtc == null || r.Announcement.PinnedUntilUtc > now),
        IsEdited = r.Announcement.Version > 1,
        IsArchived = r.Announcement.State == AnnouncementState.Archived,
        IsRead = r.FirstViewedAt != null,
        RequiresAcknowledgement = r.Announcement.RequiresAcknowledgement,
        AcknowledgeByUtc = r.Announcement.AcknowledgeByUtc,
        AcknowledgedAt = r.AcknowledgedAt,
        NeedsAcknowledgement = r.Announcement.State == AnnouncementState.Published &&
                               r.Announcement.RequiresAcknowledgement &&
                               (r.AcknowledgedVersion == null || r.AcknowledgedVersion < r.Announcement.AckRequiredVersion),
        NeedsReacknowledgement = r.Announcement.State == AnnouncementState.Published &&
                                 r.Announcement.RequiresAcknowledgement &&
                                 r.AcknowledgedVersion != null && r.AcknowledgedVersion < r.Announcement.AckRequiredVersion,
        IsOverdue = r.Announcement.State == AnnouncementState.Published &&
                    r.Announcement.RequiresAcknowledgement &&
                    r.Announcement.AcknowledgeByUtc != null && r.Announcement.AcknowledgeByUtc < now &&
                    (r.AcknowledgedVersion == null || r.AcknowledgedVersion < r.Announcement.AckRequiredVersion),
        LastRemindedAt = r.LastRemindedAt,
    };
}
