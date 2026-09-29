using Hrms.Core.Services.Announcements.Audience;
using Hrms.Core.Services.Announcements.Lifecycle;
using Hrms.Domain.Entities.Announcements;
using Hrms.Domain.Entities.EmployeeEntities;
using Microsoft.Extensions.Logging;

namespace Hrms.Core.Services.Announcements;

/// <summary>
/// Publisher side of company announcements: drafting, audience, and every lifecycle action.
/// Each action is checked against <see cref="AnnouncementStateMachine"/> first. Reports live in
/// AnnouncementReportService and the employee side in MyAnnouncementService.
/// </summary>
public class AnnouncementService : BaseService<Announcement>
{
    public const int TitleMaxLength = 200;
    public const int BodyMaxLength = 10_000;
    public const int ReasonMaxLength = 500;

    // A publish job firing this much earlier than the announcement's current publish time was
    // booked for an older time (a reschedule whose cancel didn't land) -- it's ignored.
    private static readonly TimeSpan StaleJobTolerance = TimeSpan.FromMinutes(1);

    private readonly AudienceResolver _audience;
    private readonly AnnouncementPushService _push;
    private readonly IAnnouncementJobScheduler _jobs;
    private readonly ILogger<AnnouncementService> _logger;

    public AnnouncementService(
        IUnitOfWorkService uow,
        AudienceResolver audience,
        AnnouncementPushService push,
        IAnnouncementJobScheduler jobs,
        ILogger<AnnouncementService> logger) : base(uow)
    {
        _audience = audience;
        _push = push;
        _jobs = jobs;
        _logger = logger;
    }

    // ── Reads ───────────────────────────────────────────────────────────────

    public async Task<PaginatedResult<List<AnnouncementSummaryResponse>>> ListAsync(AnnouncementListQuery query, CancellationToken token)
    {
        var now = DateTime.UtcNow;
        var states = query.Tab switch
        {
            AnnouncementListTab.Unpublished => new[] { AnnouncementState.Unpublished },
            AnnouncementListTab.Archived => new[] { AnnouncementState.Archived },
            AnnouncementListTab.Deleted => new[] { AnnouncementState.Deleted },
            _ => new[] { AnnouncementState.Draft, AnnouncementState.Scheduled, AnnouncementState.Published },
        };

        var filtered = GetQueryable().Where(a => states.Contains(a.State));
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            filtered = filtered.Where(a => a.Title.Contains(search));
        }
        if (query.From is { } from)
            filtered = filtered.Where(a => (a.PublishedAtUtc ?? a.PublishAtUtc ?? a.CreatedAt) >= from);
        if (query.To is { } to)
            filtered = filtered.Where(a => (a.PublishedAtUtc ?? a.PublishAtUtc ?? a.CreatedAt) < to.Date.AddDays(1));

        var total = await filtered.CountAsync(token);
        var page = query.Page > 0 ? query.Page : 1;
        var limit = query.Limit is > 0 and <= 200 ? query.Limit : 20;

        var rows = await ProjectSummaries(filtered
                .OrderByDescending(a => a.PublishedAtUtc ?? a.PublishAtUtc ?? a.CreatedAt)
                .Skip((page - 1) * limit)
                .Take(limit))
            .ToListAsync(token);

        return new PaginatedResult<List<AnnouncementSummaryResponse>>
        {
            Data = rows.Select(r => ToSummary(new AnnouncementSummaryResponse(), r, now)).ToList(),
            MetaData = new PaginationMetaData(total, page, limit),
        };
    }

    public async Task<AnnouncementDetailResponse> GetAsync(Guid id, CancellationToken token)
    {
        var row = await ProjectSummaries(GetQueryable(a => a.Id == id)).FirstOrDefaultAsync(token)
                  ?? throw new NotFoundException("Announcement not found.");
        var a = row.Announcement;

        var detail = ToSummary(new AnnouncementDetailResponse(), row, DateTime.UtcNow);
        detail.Body = a.Body;
        detail.PinnedUntilUtc = a.PinnedUntilUtc;
        detail.ExcludeInactive = a.ExcludeInactive;
        detail.LastEditedAtUtc = a.LastEditedAtUtc;
        detail.UnpublishedByName = a.UnpublishedByName;
        detail.UnpublishReason = a.UnpublishReason;
        detail.AudienceRules = row.Rules.Select(r => new AudienceRuleResponse
        {
            Id = r.Id,
            Kind = r.Kind,
            TargetId = r.TargetId,
            TargetValue = r.TargetValue,
            TargetLabel = r.TargetLabel,
            IsExclusion = r.IsExclusion,
        }).ToList();
        return detail;
    }

    public async Task<AudiencePreviewResponse> PreviewAudienceAsync(AudiencePreviewRequest request, CancellationToken token)
    {
        var audience = ResolveAudience(request.AudienceRules.Select(ToSpec).ToList(), request.ExcludeInactive);

        return new AudiencePreviewResponse
        {
            Total = await audience.CountAsync(token),
            Sample = await audience
                .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
                .Take(20)
                .Select(e => e.FirstName + " " + e.LastName)
                .ToListAsync(token),
            ByDepartment = await audience
                .GroupBy(e => e.Department != null ? e.Department.Name : "No department")
                .Select(g => new NameCount { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToListAsync(token),
        };
    }

    // ── Drafting ────────────────────────────────────────────────────────────

    public async Task<Guid> CreateDraftAsync(AnnouncementUpsertRequest request, AnnouncementActor actor, CancellationToken token)
    {
        ValidateContent(request);
        var announcement = new Announcement
        {
            State = AnnouncementState.Draft,
            CreatedByUserId = actor.UserId,
            CreatedByName = actor.Name,
        };
        ApplyContent(announcement, request);
        ReplaceRules(announcement, request.AudienceRules);

        await Repository.AddAsync(announcement, token);
        await CommitChangesAsync(token);
        return announcement.Id;
    }

    public async Task UpdateAsync(Guid id, AnnouncementUpsertRequest request, CancellationToken token)
    {
        var announcement = await LoadAsync(id, token);
        var now = DateTime.UtcNow;
        AnnouncementStateMachine.EnsureAllowed(announcement, AnnouncementAction.Edit, now);
        ValidateContent(request);

        var wasPublished = announcement.State == AnnouncementState.Published;
        var ackWasRequired = announcement.RequiresAcknowledgement;
        var previousExpiry = announcement.ExpiresAtUtc;
        ApplyContent(announcement, request);

        if (wasPublished)
        {
            // The audience was snapshotted at publish -- rules stay as sent.
            announcement.Version += 1;
            announcement.LastEditedAtUtc = now;
            var ackNewlyRequired = announcement.RequiresAcknowledgement && !ackWasRequired;
            if (announcement.RequiresAcknowledgement && (request.RequireReacknowledgement || ackNewlyRequired))
                announcement.AckRequiredVersion = announcement.Version;
            if (announcement.ExpiresAtUtc != previousExpiry)
                await RebookExpiryAsync(announcement, token);
        }
        else
        {
            ReplaceRules(announcement, request.AudienceRules);
            if (announcement.State == AnnouncementState.Scheduled)
            {
                if (announcement.ExpiresAtUtc <= announcement.PublishAtUtc)
                    throw new ValidationException("The expiry must be after the publish time.");
                await EnsureHasAudienceAsync(announcement, token);
            }
        }

        await CommitChangesAsync(token);
        if (wasPublished) await NotifyRecipientsAsync(announcement, "Updated", token);
    }

    public async Task<Guid> DuplicateAsync(Guid id, AnnouncementActor actor, CancellationToken token)
    {
        var source = await LoadAsync(id, token);
        AnnouncementStateMachine.EnsureAllowed(source, AnnouncementAction.Duplicate, DateTime.UtcNow);

        var copy = new Announcement
        {
            Title = source.Title.Length + 7 <= TitleMaxLength ? $"{source.Title} (copy)" : source.Title,
            Body = source.Body,
            Priority = source.Priority,
            RequiresAcknowledgement = source.RequiresAcknowledgement,
            ExcludeInactive = source.ExcludeInactive,
            State = AnnouncementState.Draft,
            CreatedByUserId = actor.UserId,
            CreatedByName = actor.Name,
            // Dates are the publisher's to pick again -- the old ones are usually in the past.
            AudienceRules = source.AudienceRules.Select(r => new AnnouncementAudienceRule
            {
                Kind = r.Kind,
                TargetId = r.TargetId,
                TargetValue = r.TargetValue,
                TargetLabel = r.TargetLabel,
                IsExclusion = r.IsExclusion,
            }).ToList(),
        };

        await Repository.AddAsync(copy, token);
        await CommitChangesAsync(token);
        return copy.Id;
    }

    // ── Sending ─────────────────────────────────────────────────────────────

    public async Task ScheduleAsync(Guid id, DateTime publishAtUtc, CancellationToken token)
    {
        var announcement = await LoadAsync(id, token);
        var now = DateTime.UtcNow;
        AnnouncementStateMachine.EnsureAllowed(announcement, AnnouncementAction.Schedule, now);
        if (publishAtUtc <= now) throw new ValidationException("Pick a publish time in the future, or publish now.");
        if (announcement.ExpiresAtUtc is { } expires && expires <= publishAtUtc)
            throw new ValidationException("The expiry must be after the publish time.");
        await EnsureHasAudienceAsync(announcement, token);

        await CancelJobAsync(announcement.PublishJobToken, token);
        announcement.PublishAtUtc = publishAtUtc;
        announcement.State = AnnouncementState.Scheduled;
        announcement.PublishJobToken = await _jobs.ScheduleAsync(id, AnnouncementLifecycleWork.Publish, publishAtUtc, token);
        await CommitChangesAsync(token);
    }

    public async Task UnscheduleAsync(Guid id, CancellationToken token)
    {
        var announcement = await LoadAsync(id, token);
        AnnouncementStateMachine.EnsureAllowed(announcement, AnnouncementAction.Unschedule, DateTime.UtcNow);
        await CancelJobAsync(announcement.PublishJobToken, token);
        announcement.PublishJobToken = null;
        announcement.State = AnnouncementState.Draft;
        announcement.PublishAtUtc = null;
        await CommitChangesAsync(token);
    }

    /// <summary>
    /// "Publish now": sends it immediately. False (and no change) when it was already sent.
    /// Refuses an empty audience or an expiry that has already passed.
    /// </summary>
    public async Task<bool> PublishNowAsync(Guid id, AnnouncementActor actor, CancellationToken token)
    {
        var announcement = await LoadAsync(id, token);
        if (!AnnouncementStateMachine.IsNeverSent(announcement.State)) return false;
        if (announcement.ExpiresAtUtc is { } expires && expires <= DateTime.UtcNow)
            throw new ValidationException("The expiry has already passed -- change it before publishing.");
        return await PublishCoreAsync(announcement, actor, requireRecipients: true, token);
    }

    /// <summary>
    /// The Quartz publish job firing. Goes out even if the audience has since emptied (it was
    /// checked when scheduled); a job booked for an earlier time than the current one -- a
    /// reschedule whose cancel didn't land -- or one that's already been sent is ignored.
    /// </summary>
    public async Task<bool> PublishScheduledAsync(Guid id, CancellationToken token)
    {
        var announcement = await LoadAsync(id, token);
        if (announcement.State != AnnouncementState.Scheduled ||
            announcement.PublishAtUtc > DateTime.UtcNow + StaleJobTolerance)
            return false;
        return await PublishCoreAsync(announcement, AnnouncementActor.System, requireRecipients: false, token);
    }

    // Snapshots the audience as of right now into recipient rows and marks it Published.
    private async Task<bool> PublishCoreAsync(Announcement announcement, AnnouncementActor actor, bool requireRecipients, CancellationToken token)
    {
        var id = announcement.Id;
        var now = DateTime.UtcNow;

        var already = await Repository.FindAll<AnnouncementRecipient>()
            .Where(r => r.AnnouncementId == id)
            .Select(r => r.EmployeeId)
            .ToListAsync(token);
        var alreadySet = already.ToHashSet();

        var audience = await ResolveAudience(announcement.AudienceRules.Select(ToSpec).ToList(), announcement.ExcludeInactive)
            .Select(e => new
            {
                e.Id,
                e.UserId,
                e.Email,
                e.EmployeeNo,
                Name = e.FirstName + " " + e.LastName,
                Department = e.Department != null ? e.Department.Name : "",
                Position = e.Position != null ? e.Position.Name : "",
                Branch = e.Branch != null ? e.Branch.Name : "",
            })
            .ToListAsync(token);

        if (requireRecipients && audience.Count == 0)
            throw new ValidationException("Nobody matches this audience yet -- adjust the audience before publishing.");

        var recipients = audience
            .Where(e => !alreadySet.Contains(e.Id))
            .Select(e => new AnnouncementRecipient
            {
                AnnouncementId = id,
                EmployeeId = e.Id,
                UserId = e.UserId,
                Email = e.Email,
                EmployeeNo = e.EmployeeNo,
                EmployeeName = e.Name.Trim(),
                DepartmentName = e.Department,
                PositionName = e.Position,
                BranchName = e.Branch,
                DeliveredAt = now,
            })
            .ToList();
        if (recipients.Count > 0) await Repository.AddRangeAsync(recipients, token);

        announcement.State = AnnouncementState.Published;
        announcement.PublishedAtUtc = now;
        announcement.PublishedByUserId = actor.UserId;
        announcement.PublishedByName = actor.Name;
        // Published by hand ahead of its schedule -- the pending auto-publish is no longer needed.
        await CancelJobAsync(announcement.PublishJobToken, token);
        announcement.PublishJobToken = null;
        await RebookExpiryAsync(announcement, token);
        await CommitChangesAsync(token);

        await SafePushAsync(recipients, announcement, "Published", token);
        return true;
    }

    public async Task UnpublishAsync(Guid id, string? reason, AnnouncementActor actor, CancellationToken token)
    {
        var announcement = await LoadAsync(id, token);
        AnnouncementStateMachine.EnsureAllowed(announcement, AnnouncementAction.Unpublish, DateTime.UtcNow);
        if (reason?.Length > ReasonMaxLength) throw new ValidationException($"Keep the reason under {ReasonMaxLength} characters.");

        announcement.State = AnnouncementState.Unpublished;
        announcement.UnpublishedAtUtc = DateTime.UtcNow;
        announcement.UnpublishedByName = actor.Name;
        announcement.UnpublishReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        await RebookExpiryAsync(announcement, token); // off while it isn't published
        await CommitChangesAsync(token);
        await NotifyRecipientsAsync(announcement, "Unpublished", token);
    }

    // Same recipients, receipts kept -- it simply becomes visible again.
    public async Task RepublishAsync(Guid id, CancellationToken token)
    {
        var announcement = await LoadAsync(id, token);
        AnnouncementStateMachine.EnsureAllowed(announcement, AnnouncementAction.Republish, DateTime.UtcNow);

        announcement.State = AnnouncementState.Published;
        announcement.UnpublishedAtUtc = null;
        announcement.UnpublishedByName = null;
        announcement.UnpublishReason = null;
        await RebookExpiryAsync(announcement, token);
        await CommitChangesAsync(token);
        await NotifyRecipientsAsync(announcement, "Published", token);
    }

    /// <summary>Nudges the recipients who haven't viewed / acknowledged yet. Returns how many.</summary>
    public async Task<int> RemindAsync(Guid id, RemindTarget target, CancellationToken token)
    {
        var announcement = await LoadAsync(id, token);
        AnnouncementStateMachine.EnsureAllowed(announcement, AnnouncementAction.Remind, DateTime.UtcNow);
        if (target == RemindTarget.NotAcknowledged && !announcement.RequiresAcknowledgement)
            throw new ValidationException("This announcement doesn't ask for acknowledgement.");

        var required = announcement.AckRequiredVersion;
        var recipients = Repository.FindAll<AnnouncementRecipient>().Where(r => r.AnnouncementId == id);
        recipients = target == RemindTarget.NotViewed
            ? recipients.Where(r => r.FirstViewedAt == null)
            : recipients.Where(r => r.AcknowledgedVersion == null || r.AcknowledgedVersion < required);
        var targets = await recipients.ToListAsync(token);
        if (targets.Count == 0) return 0;

        var now = DateTime.UtcNow;
        foreach (var recipient in targets)
        {
            recipient.RemindCount += 1;
            recipient.LastRemindedAt = now;
        }
        await CommitChangesAsync(token);

        await SafePushAsync(targets, announcement, "Reminder", token);
        return targets.Count;
    }

    public async Task PinAsync(Guid id, DateTime? pinnedUntilUtc, CancellationToken token)
    {
        var announcement = await LoadAsync(id, token);
        var now = DateTime.UtcNow;
        AnnouncementStateMachine.EnsureAllowed(announcement, AnnouncementAction.Pin, now);
        if (pinnedUntilUtc is { } until && until <= now) throw new ValidationException("Pin until a time in the future.");

        announcement.IsPinned = true;
        announcement.PinnedUntilUtc = pinnedUntilUtc;
        await CommitChangesAsync(token);
    }

    public async Task UnpinAsync(Guid id, CancellationToken token)
    {
        var announcement = await LoadAsync(id, token);
        AnnouncementStateMachine.EnsureAllowed(announcement, AnnouncementAction.Unpin, DateTime.UtcNow);
        announcement.IsPinned = false;
        announcement.PinnedUntilUtc = null;
        await CommitChangesAsync(token);
    }

    // ── Archive / delete ────────────────────────────────────────────────────

    public Task ArchiveAsync(Guid id, CancellationToken token) => ArchiveCoreAsync(id, AnnouncementAction.Archive, token);

    /// <summary>System: a Published announcement past its expiry. No-op if it no longer applies.</summary>
    public async Task<bool> ExpireAsync(Guid id, CancellationToken token)
    {
        var announcement = await LoadAsync(id, token);
        if (!AnnouncementStateMachine.IsAllowed(announcement, AnnouncementAction.Expire, DateTime.UtcNow)) return false;
        await ArchiveCoreAsync(announcement, token);
        return true;
    }

    public async Task RestoreAsync(Guid id, CancellationToken token)
    {
        var announcement = await LoadAsync(id, token);
        AnnouncementStateMachine.EnsureAllowed(announcement, AnnouncementAction.Restore, DateTime.UtcNow);

        announcement.State = AnnouncementStateMachine.Next(announcement, AnnouncementAction.Restore)!.Value;
        announcement.StateBeforeArchive = null;
        announcement.ArchivedAtUtc = null;
        // Restoring an expired one would only be archived again straight away.
        if (announcement.State == AnnouncementState.Published && AnnouncementStateMachine.IsExpired(announcement, DateTime.UtcNow))
            announcement.ExpiresAtUtc = null;
        await RebookExpiryAsync(announcement, token);
        await CommitChangesAsync(token);
    }

    /// <summary>
    /// A never-sent Draft/Scheduled announcement is removed for good. A sent one goes to the
    /// "Recently deleted" bin (restorable for <see cref="AnnouncementStateMachine.BinRetention"/>)
    /// because its receipts are records.
    /// </summary>
    public async Task DeleteAsync(Guid id, CancellationToken token)
    {
        var announcement = await LoadAsync(id, token);
        AnnouncementStateMachine.EnsureAllowed(announcement, AnnouncementAction.Delete, DateTime.UtcNow);

        if (AnnouncementStateMachine.Next(announcement, AnnouncementAction.Delete) == null)
        {
            await CancelJobAsync(announcement.PublishJobToken, token);
            Repository.Remove(announcement);
            await CommitChangesAsync(token);
            return;
        }

        var wasVisible = announcement.State is AnnouncementState.Published or AnnouncementState.Archived;
        var now = DateTime.UtcNow;
        announcement.StateBeforeDelete = announcement.State;
        announcement.State = AnnouncementState.Deleted;
        announcement.BinnedAtUtc = now;
        await RebookExpiryAsync(announcement, token);
        await CancelJobAsync(announcement.PurgeJobToken, token);
        announcement.PurgeJobToken = await _jobs.ScheduleAsync(
            announcement.Id, AnnouncementLifecycleWork.Purge, now + AnnouncementStateMachine.BinRetention, token);
        await CommitChangesAsync(token);
        if (wasVisible) await NotifyRecipientsAsync(announcement, "Removed", token);
    }

    public async Task RestoreDeletedAsync(Guid id, CancellationToken token)
    {
        var announcement = await LoadAsync(id, token);
        AnnouncementStateMachine.EnsureAllowed(announcement, AnnouncementAction.RestoreDeleted, DateTime.UtcNow);

        announcement.State = AnnouncementStateMachine.Next(announcement, AnnouncementAction.RestoreDeleted)!.Value;
        announcement.StateBeforeDelete = null;
        announcement.BinnedAtUtc = null;
        await CancelJobAsync(announcement.PurgeJobToken, token);
        announcement.PurgeJobToken = null;
        await RebookExpiryAsync(announcement, token);
        await CommitChangesAsync(token);
    }

    /// <summary>System: removes a binned announcement past retention, with its rules and receipts.</summary>
    public async Task<bool> PurgeAsync(Guid id, CancellationToken token)
    {
        var announcement = await LoadAsync(id, token);
        if (!AnnouncementStateMachine.IsAllowed(announcement, AnnouncementAction.Purge, DateTime.UtcNow)) return false;

        await Repository.FindAll<AnnouncementRecipient>().Where(r => r.AnnouncementId == id).ExecuteDeleteAsync(token);
        await Repository.FindAll<AnnouncementAudienceRule>().Where(r => r.AnnouncementId == id).ExecuteDeleteAsync(token);
        await GetQueryable(a => a.Id == id).ExecuteDeleteAsync(token);
        await CommitChangesAsync(token);
        return true;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<Announcement> LoadAsync(Guid id, CancellationToken token) =>
        await GetQueryable(a => a.Id == id, noTracking: false)
            .Include(a => a.AudienceRules)
            .FirstOrDefaultAsync(token)
        ?? throw new NotFoundException("Announcement not found.");

    private async Task ArchiveCoreAsync(Guid id, AnnouncementAction action, CancellationToken token)
    {
        var announcement = await LoadAsync(id, token);
        AnnouncementStateMachine.EnsureAllowed(announcement, action, DateTime.UtcNow);
        await ArchiveCoreAsync(announcement, token);
    }

    private async Task ArchiveCoreAsync(Announcement announcement, CancellationToken token)
    {
        announcement.StateBeforeArchive = announcement.State;
        announcement.State = AnnouncementState.Archived;
        announcement.ArchivedAtUtc = DateTime.UtcNow;
        await RebookExpiryAsync(announcement, token);
        await CommitChangesAsync(token);
        await NotifyRecipientsAsync(announcement, "Archived", token);
    }

    private async Task CancelJobAsync(Guid? jobToken, CancellationToken token)
    {
        if (jobToken is { } jobId) await _jobs.CancelAsync(jobId, token);
    }

    /// <summary>
    /// Keeps exactly one auto-archive job in line with the announcement: none unless it is
    /// Published with an expiry, otherwise one at the expiry (or right away if already past).
    /// </summary>
    private async Task RebookExpiryAsync(Announcement announcement, CancellationToken token)
    {
        await CancelJobAsync(announcement.ExpireJobToken, token);
        announcement.ExpireJobToken = null;

        if (announcement.State == AnnouncementState.Published && announcement.ExpiresAtUtc is { } expires)
        {
            var now = DateTime.UtcNow;
            announcement.ExpireJobToken = await _jobs.ScheduleAsync(
                announcement.Id, AnnouncementLifecycleWork.Expire, expires > now ? expires : now, token);
        }
    }

    private IQueryable<Employee> ResolveAudience(IReadOnlyCollection<AudienceRuleSpec> rules, bool excludeInactive) =>
        _audience.Resolve(Repository.FindAll<Employee>().AsNoTracking(), rules, excludeInactive);

    private async Task EnsureHasAudienceAsync(Announcement announcement, CancellationToken token)
    {
        if (!announcement.AudienceRules.Any(r => !r.IsExclusion))
            throw new ValidationException("Choose who this announcement is for.");
        var count = await ResolveAudience(announcement.AudienceRules.Select(ToSpec).ToList(), announcement.ExcludeInactive)
            .CountAsync(token);
        if (count == 0) throw new ValidationException("Nobody matches this audience yet -- adjust the audience.");
    }

    private static void ValidateContent(AnnouncementUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title)) throw new ValidationException("Title is required.");
        if (request.Title.Trim().Length > TitleMaxLength) throw new ValidationException($"Keep the title under {TitleMaxLength} characters.");
        if (string.IsNullOrWhiteSpace(request.Body)) throw new ValidationException("Message is required.");
        if (request.Body.Length > BodyMaxLength) throw new ValidationException($"Keep the message under {BodyMaxLength:N0} characters.");
        if (request.AcknowledgeByUtc != null && !request.RequiresAcknowledgement)
            throw new ValidationException("An acknowledgement due date needs \"Require acknowledgement\" turned on.");
        if (request.ExpiresAtUtc is { } expires && expires <= DateTime.UtcNow)
            throw new ValidationException("The expiry must be in the future.");
        if (request.AudienceRules.Any(r => r.Kind != AudienceRuleKind.AllEmployees && r.Kind != AudienceRuleKind.EmploymentStatus && r.TargetId == null))
            throw new ValidationException("Every audience rule needs a target.");
    }

    private static void ApplyContent(Announcement announcement, AnnouncementUpsertRequest request)
    {
        announcement.Title = request.Title.Trim();
        announcement.Body = request.Body.TrimEnd();
        announcement.Priority = request.Priority;
        announcement.RequiresAcknowledgement = request.RequiresAcknowledgement;
        announcement.AcknowledgeByUtc = request.RequiresAcknowledgement ? request.AcknowledgeByUtc : null;
        announcement.ExpiresAtUtc = request.ExpiresAtUtc;
        announcement.IsPinned = request.IsPinned;
        announcement.PinnedUntilUtc = request.IsPinned ? request.PinnedUntilUtc : null;
        announcement.ExcludeInactive = request.ExcludeInactive;
    }

    private static void ReplaceRules(Announcement announcement, IEnumerable<AudienceRuleRequest> rules)
    {
        announcement.AudienceRules.Clear();
        foreach (var rule in rules)
        {
            announcement.AudienceRules.Add(new AnnouncementAudienceRule
            {
                Kind = rule.Kind,
                TargetId = rule.TargetId,
                TargetValue = rule.TargetValue,
                TargetLabel = rule.TargetLabel.Trim(),
                IsExclusion = rule.IsExclusion,
            });
        }
    }

    private static AudienceRuleSpec ToSpec(AudienceRuleRequest r) => new(r.Kind, r.TargetId, r.TargetValue, r.IsExclusion);
    private static AudienceRuleSpec ToSpec(AnnouncementAudienceRule r) => new(r.Kind, r.TargetId, r.TargetValue, r.IsExclusion);

    private async Task NotifyRecipientsAsync(Announcement announcement, string kind, CancellationToken token)
    {
        var recipients = await Repository.FindAll<AnnouncementRecipient>().AsNoTracking()
            .Where(r => r.AnnouncementId == announcement.Id)
            .ToListAsync(token);
        await SafePushAsync(recipients, announcement, kind, token);
    }

    // After commit only; a failed push must never surface as a failed action.
    private async Task SafePushAsync(IReadOnlyCollection<AnnouncementRecipient> recipients, Announcement announcement, string kind, CancellationToken token)
    {
        if (recipients.Count == 0) return;
        try
        {
            await _push.PushAsync(recipients, new AnnouncementPushNotification
            {
                AnnouncementId = announcement.Id,
                Kind = kind,
                Title = announcement.Title,
                Priority = announcement.Priority,
            }, token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Announcement {Id}: live push ({Kind}) failed", announcement.Id, kind);
        }
    }

    private sealed class SummaryRow
    {
        public Announcement Announcement { get; set; } = null!;
        public List<AnnouncementAudienceRule> Rules { get; set; } = [];
        public int RecipientCount { get; set; }
        public int ViewedCount { get; set; }
        public int AcknowledgedCount { get; set; }
    }

    private static IQueryable<SummaryRow> ProjectSummaries(IQueryable<Announcement> query) =>
        query.Select(a => new SummaryRow
        {
            Announcement = a,
            Rules = a.AudienceRules.ToList(),
            RecipientCount = a.Recipients.Count(),
            ViewedCount = a.Recipients.Count(r => r.FirstViewedAt != null),
            AcknowledgedCount = a.Recipients.Count(r => r.AcknowledgedVersion != null && r.AcknowledgedVersion >= a.AckRequiredVersion),
        });

    private static T ToSummary<T>(T target, SummaryRow row, DateTime now) where T : AnnouncementSummaryResponse
    {
        var a = row.Announcement;
        target.Id = a.Id;
        target.Title = a.Title;
        target.Priority = a.Priority;
        target.State = a.State;
        target.IsExpired = a.State == AnnouncementState.Published && AnnouncementStateMachine.IsExpired(a, now);
        target.IsPinned = AnnouncementStateMachine.IsPinnedNow(a, now);
        target.RequiresAcknowledgement = a.RequiresAcknowledgement;
        target.AcknowledgeByUtc = a.AcknowledgeByUtc;
        target.PublishAtUtc = a.PublishAtUtc;
        target.PublishedAtUtc = a.PublishedAtUtc;
        target.PublishedByName = a.PublishedByName;
        target.CreatedByName = a.CreatedByName;
        target.CreatedAt = a.CreatedAt;
        target.ExpiresAtUtc = a.ExpiresAtUtc;
        target.UnpublishedAtUtc = a.UnpublishedAtUtc;
        target.ArchivedAtUtc = a.ArchivedAtUtc;
        target.PurgeAtUtc = a.BinnedAtUtc + AnnouncementStateMachine.BinRetention;
        target.Version = a.Version;
        target.AudienceSummary = row.Rules
            .OrderBy(r => r.IsExclusion)
            .Select(r => r.Kind == AudienceRuleKind.AllEmployees ? "All employees" : (r.IsExclusion ? "Except " : "") + r.TargetLabel)
            .ToList();
        target.RecipientCount = row.RecipientCount;
        target.ViewedCount = row.ViewedCount;
        target.AcknowledgedCount = row.AcknowledgedCount;
        target.AllowedActions = AnnouncementStateMachine.AllowedActions(a, now).ToList();
        return target;
    }
}
