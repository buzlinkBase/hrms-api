using Hrms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Hrms.Core.Messaging.Notifications;

/// <summary>
/// Daily clean-up of old bell entries (UserNotification), every company at once: anything older
/// than Notifications:RetentionDays (default 90) goes, and so does anything already read for longer
/// than Notifications:ReadRetentionDays (default 2). The approvals themselves are untouched --
/// only the notices about them expire.
/// </summary>
[DisallowConcurrentExecution]
public sealed class NotificationRetentionJob : IJob
{
    public static readonly JobKey Key = new("notification-retention", "hrms");
    public static readonly TriggerKey TriggerKey = new("notification-retention-trigger", "hrms");
    public const int DefaultRetentionDays = 90;
    public const int DefaultReadRetentionDays = 2;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationRetentionJob> _logger;

    public NotificationRetentionJob(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<NotificationRetentionJob> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var days = Math.Max(1, _configuration.GetValue("Notifications:RetentionDays", DefaultRetentionDays));
        var readDays = Math.Max(1, _configuration.GetValue("Notifications:ReadRetentionDays", DefaultReadRetentionDays));
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrmsContext>();

        var now = DateTime.UtcNow;
        var removed = await PurgeAsync(db, now.AddDays(-days), now.AddDays(-readDays), context.CancellationToken);
        _logger.LogInformation(
            "Notification retention: removed {Count} entries (older than {Days} days, or read more than {ReadDays} days ago)",
            removed, days, readDays);
    }

    /// <summary>
    /// Deletes every company's entries created before <paramref name="cutoffUtc"/>, plus entries
    /// that were read before <paramref name="readCutoffUtc"/>. Unread entries only expire by age.
    /// </summary>
    public static Task<int> PurgeAsync(HrmsContext db, DateTime cutoffUtc, DateTime readCutoffUtc, CancellationToken token) =>
        db.UserNotifications.IgnoreQueryFilters()
            .Where(x => x.CreatedAt < cutoffUtc || (x.ReadAt != null && x.ReadAt < readCutoffUtc))
            .ExecuteDeleteAsync(token);
}
