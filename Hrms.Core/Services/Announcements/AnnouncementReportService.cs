using System.Linq.Expressions;
using Hrms.Domain.Entities.Announcements;

namespace Hrms.Core.Services.Announcements;

/// <summary>
/// The publisher's view of who received, viewed and acknowledged an announcement. Read-only;
/// works in every state except after a purge, since receipts are kept through unpublish,
/// archive and the bin.
/// </summary>
public class AnnouncementReportService : BaseService<AnnouncementRecipient>
{
    public AnnouncementReportService(IUnitOfWorkService uow) : base(uow)
    {
    }

    public async Task<AnnouncementStatsResponse> GetStatsAsync(Guid announcementId, CancellationToken token)
    {
        var announcement = await LoadAnnouncementAsync(announcementId, token);
        var now = DateTime.UtcNow;
        var recipients = GetQueryable(r => r.AnnouncementId == announcementId);
        var acknowledged = IsAcknowledged(announcement.AckRequiredVersion);
        var overdueApplies = announcement.RequiresAcknowledgement && announcement.AcknowledgeByUtc < now;

        var reach = await recipients.CountAsync(token);
        var viewed = await recipients.CountAsync(r => r.FirstViewedAt != null, token);
        var acknowledgedCount = announcement.RequiresAcknowledgement ? await recipients.CountAsync(acknowledged, token) : 0;
        var viewDates = await recipients
            .Where(r => r.FirstViewedAt != null)
            .Select(r => r.FirstViewedAt!.Value)
            .ToListAsync(token);

        return new AnnouncementStatsResponse
        {
            Reach = reach,
            Viewed = viewed,
            NotViewed = reach - viewed,
            ViewRate = Rate(viewed, reach),
            RequiresAcknowledgement = announcement.RequiresAcknowledgement,
            Acknowledged = acknowledgedCount,
            PendingAcknowledgement = announcement.RequiresAcknowledgement ? reach - acknowledgedCount : 0,
            AcknowledgementRate = announcement.RequiresAcknowledgement ? Rate(acknowledgedCount, reach) : 0,
            Overdue = overdueApplies ? reach - acknowledgedCount : 0,
            Unreachable = await recipients.CountAsync(r => r.UserId == null && (r.Email == null || r.Email == ""), token),
            ByDepartment = await BreakdownAsync(recipients, r => r.DepartmentName, announcement.AckRequiredVersion, token),
            ByPosition = await BreakdownAsync(recipients, r => r.PositionName, announcement.AckRequiredVersion, token),
            ByBranch = await BreakdownAsync(recipients, r => r.BranchName, announcement.AckRequiredVersion, token),
            ViewsPerDay = viewDates
                .GroupBy(d => DateOnly.FromDateTime(d))
                .OrderBy(g => g.Key)
                .Select(g => new DailyCount { Date = g.Key, Count = g.Count() })
                .ToList(),
        };
    }

    public async Task<PaginatedResult<List<RecipientResponse>>> GetRecipientsAsync(Guid announcementId, RecipientListQuery query, CancellationToken token)
    {
        var announcement = await LoadAnnouncementAsync(announcementId, token);
        var now = DateTime.UtcNow;
        var required = announcement.AckRequiredVersion;
        var overdueApplies = announcement.RequiresAcknowledgement && announcement.AcknowledgeByUtc < now;

        var recipients = GetQueryable(r => r.AnnouncementId == announcementId);
        recipients = query.Status switch
        {
            RecipientStatusFilter.Viewed => recipients.Where(r => r.FirstViewedAt != null),
            RecipientStatusFilter.NotViewed => recipients.Where(r => r.FirstViewedAt == null),
            RecipientStatusFilter.Acknowledged => announcement.RequiresAcknowledgement
                ? recipients.Where(IsAcknowledged(required))
                : recipients.Where(r => false),
            RecipientStatusFilter.NotAcknowledged => announcement.RequiresAcknowledgement
                ? recipients.Where(r => r.AcknowledgedVersion == null || r.AcknowledgedVersion < required)
                : recipients.Where(r => false),
            RecipientStatusFilter.Overdue => overdueApplies
                ? recipients.Where(r => r.AcknowledgedVersion == null || r.AcknowledgedVersion < required)
                : recipients.Where(r => false),
            RecipientStatusFilter.Unreachable => recipients.Where(r => r.UserId == null && (r.Email == null || r.Email == "")),
            _ => recipients,
        };
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            recipients = recipients.Where(r => r.EmployeeName.Contains(search) || r.EmployeeNo.Contains(search));
        }
        if (!string.IsNullOrWhiteSpace(query.Department)) recipients = recipients.Where(r => r.DepartmentName == query.Department);
        if (!string.IsNullOrWhiteSpace(query.Position)) recipients = recipients.Where(r => r.PositionName == query.Position);
        if (!string.IsNullOrWhiteSpace(query.Branch)) recipients = recipients.Where(r => r.BranchName == query.Branch);

        var descending = string.Equals(query.SortOrder, "descend", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(query.SortOrder, "desc", StringComparison.OrdinalIgnoreCase);
        recipients = (query.SortField?.ToLowerInvariant()) switch
        {
            "department" => descending ? recipients.OrderByDescending(r => r.DepartmentName).ThenBy(r => r.EmployeeName) : recipients.OrderBy(r => r.DepartmentName).ThenBy(r => r.EmployeeName),
            "firstviewedat" => descending ? recipients.OrderByDescending(r => r.FirstViewedAt) : recipients.OrderBy(r => r.FirstViewedAt),
            "acknowledgedat" => descending ? recipients.OrderByDescending(r => r.AcknowledgedAt) : recipients.OrderBy(r => r.AcknowledgedAt),
            _ => descending ? recipients.OrderByDescending(r => r.EmployeeName) : recipients.OrderBy(r => r.EmployeeName),
        };

        var total = await recipients.CountAsync(token);
        var page = query.Page > 0 ? query.Page : 1;
        var paged = query.Limit > 0 ? recipients.Skip((page - 1) * query.Limit).Take(query.Limit) : recipients;

        var rows = await paged.Select(r => new RecipientResponse
        {
            EmployeeId = r.EmployeeId,
            EmployeeNo = r.EmployeeNo,
            EmployeeName = r.EmployeeName,
            DepartmentName = r.DepartmentName,
            PositionName = r.PositionName,
            BranchName = r.BranchName,
            IsReachable = r.UserId != null || (r.Email != null && r.Email != ""),
            DeliveredAt = r.DeliveredAt,
            FirstViewedAt = r.FirstViewedAt,
            LastViewedAt = r.LastViewedAt,
            ViewCount = r.ViewCount,
            AcknowledgedAt = r.AcknowledgedAt,
            AcknowledgedVersion = r.AcknowledgedVersion,
            IsAcknowledged = r.AcknowledgedVersion != null && r.AcknowledgedVersion >= required,
            RemindCount = r.RemindCount,
            LastRemindedAt = r.LastRemindedAt,
        }).ToListAsync(token);

        foreach (var row in rows)
            row.IsOverdue = overdueApplies && !row.IsAcknowledged;

        return new PaginatedResult<List<RecipientResponse>>
        {
            Data = rows,
            MetaData = new PaginationMetaData(total, page, query.Limit > 0 ? query.Limit : total),
        };
    }

    private async Task<Announcement> LoadAnnouncementAsync(Guid id, CancellationToken token) =>
        await Repository.FindAll<Announcement>().AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, token)
        ?? throw new NotFoundException("Announcement not found.");

    private static Expression<Func<AnnouncementRecipient, bool>> IsAcknowledged(int requiredVersion) =>
        r => r.AcknowledgedVersion != null && r.AcknowledgedVersion >= requiredVersion;

    private static async Task<List<BreakdownRow>> BreakdownAsync(
        IQueryable<AnnouncementRecipient> recipients,
        Expression<Func<AnnouncementRecipient, string>> key,
        int requiredVersion,
        CancellationToken token)
    {
        var rows = await recipients
            .GroupBy(key)
            .Select(g => new BreakdownRow
            {
                Name = g.Key,
                Total = g.Count(),
                Viewed = g.Count(r => r.FirstViewedAt != null),
                Acknowledged = g.Count(r => r.AcknowledgedVersion != null && r.AcknowledgedVersion >= requiredVersion),
            })
            .ToListAsync(token);

        foreach (var row in rows.Where(r => string.IsNullOrWhiteSpace(r.Name)))
            row.Name = "Unassigned";
        return rows.OrderByDescending(r => r.Total).ThenBy(r => r.Name).ToList();
    }

    private static double Rate(int part, int whole) => whole == 0 ? 0 : Math.Round(part * 100.0 / whole, 1);
}
