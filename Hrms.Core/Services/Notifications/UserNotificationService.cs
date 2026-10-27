using Hrms.Domain.Entities;

namespace Hrms.Core.Services.Notifications;

public sealed record UserNotificationItem(
    Guid Id,
    string Type,
    ApprovalApplicationType ApplicationType,
    Guid ApplicationId,
    Guid ApprovalInstanceId,
    string ApplicationTypeLabel,
    string ApplicantName,
    string StatusLabel,
    int? StepNumber,
    int? TotalSteps,
    string? Note,
    DateTime CreatedAt,
    DateTime? ReadAt);

public sealed record UserNotificationPage(List<UserNotificationItem> Items, int Total, int UnreadCount, int Page, int PageSize);

/// <summary>
/// The notification bell's saved entries. Each person's are found the way the live push finds
/// them -- their account id, or their login email in this company -- and only ever their own:
/// the caller's identity comes from their token, resolved by the controller.
/// </summary>
public class UserNotificationService : BaseService<UserNotification>
{
    public const int MaxPageSize = 50;

    public UserNotificationService(IUnitOfWorkService uow) : base(uow)
    {
    }

    public static string? NormalizeEmail(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();

    /// <summary>
    /// Saves one approval update for its recipient. <paramref name="id"/> is the message's own id,
    /// so a redelivered message finds the entry it already saved instead of adding a second.
    /// </summary>
    public async Task<UserNotification> SaveApprovalAsync(Guid id, Guid? recipientUserId, string? recipientEmail,
        Approvals.ApprovalPushNotification update, CancellationToken token)
    {
        // By id alone: a message id is unique across companies, and the entry may not be readable
        // through the company filter yet.
        var existing = await Repository.FindAll<UserNotification>().IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id, token);
        if (existing != null) return existing;

        var notification = new UserNotification
        {
            Id = id,
            RecipientUserId = recipientUserId,
            RecipientEmail = NormalizeEmail(recipientEmail),
            Type = UserNotification.ApprovalType,
            ApplicationType = update.ApplicationType,
            ApplicationId = update.ApplicationId,
            ApprovalInstanceId = update.ApprovalInstanceId,
            ApplicationTypeLabel = update.ApplicationTypeLabel,
            ApplicantName = update.ApplicantName,
            StatusLabel = update.StatusLabel,
            StepNumber = update.StepNumber,
            TotalSteps = update.TotalSteps,
            Note = update.Note,
            CreatedAt = update.TimestampUtc,
        };
        await Repository.AddAsync(notification, token);
        await CommitChangesAsync(token);
        return notification;
    }

    public async Task<UserNotificationPage> GetMineAsync(Guid userId, string? email, int page, int pageSize, bool unreadOnly, CancellationToken token)
    {
        page = page > 0 ? page : 1;
        pageSize = pageSize is > 0 and <= MaxPageSize ? pageSize : 20;

        var mine = Mine(userId, email);
        var listed = unreadOnly ? mine.Where(x => x.ReadAt == null) : mine;
        var total = await listed.CountAsync(token);
        var unread = await mine.CountAsync(x => x.ReadAt == null, token);
        var items = await listed
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new UserNotificationItem(
                x.Id, x.Type, x.ApplicationType, x.ApplicationId, x.ApprovalInstanceId,
                x.ApplicationTypeLabel, x.ApplicantName, x.StatusLabel, x.StepNumber, x.TotalSteps,
                x.Note, x.CreatedAt, x.ReadAt))
            .ToListAsync(token);

        return new UserNotificationPage(items, total, unread, page, pageSize);
    }

    /// <summary>False when it isn't one of the caller's own.</summary>
    public async Task<bool> MarkReadAsync(Guid userId, string? email, Guid id, CancellationToken token)
    {
        var notification = await Mine(userId, email, tracked: true).FirstOrDefaultAsync(x => x.Id == id, token);
        if (notification == null) return false;
        if (notification.ReadAt == null)
        {
            notification.ReadAt = DateTime.UtcNow;
            await CommitChangesAsync(token);
        }
        return true;
    }

    public async Task<int> MarkAllReadAsync(Guid userId, string? email, CancellationToken token)
    {
        var unread = await Mine(userId, email, tracked: true).Where(x => x.ReadAt == null).ToListAsync(token);
        if (unread.Count == 0) return 0;
        var now = DateTime.UtcNow;
        unread.ForEach(x => x.ReadAt = now);
        await CommitChangesAsync(token);
        return unread.Count;
    }

    /// <summary>Removes every saved entry of the caller's own; returns how many.</summary>
    public async Task<int> ClearAllAsync(Guid userId, string? email, CancellationToken token)
    {
        var mine = await Mine(userId, email, tracked: true).ToListAsync(token);
        if (mine.Count == 0) return 0;
        await RemoveRangeAsync(mine, token);
        await CommitChangesAsync(token);
        return mine.Count;
    }

    private IQueryable<UserNotification> Mine(Guid userId, string? email, bool tracked = false)
    {
        var normalized = NormalizeEmail(email);
        var all = Repository.FindAll<UserNotification>();
        if (!tracked) all = all.AsNoTracking();
        return all.Where(x => x.RecipientUserId == userId || (normalized != null && x.RecipientEmail == normalized));
    }
}
