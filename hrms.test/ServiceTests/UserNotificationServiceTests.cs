using Hrms.Core.Messaging.Notifications;
using Hrms.Core.Services.Notifications;
using Hrms.Domain.Entities;
using hrms.test.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace hrms.test.ServiceTests;

/// <summary>
/// The saved notification bell: each person sees only their own entries (by account id or login
/// email, within their company), newest first with an unread count, and can mark them read. Old
/// entries expire.
/// </summary>
public class UserNotificationServiceTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid AnaId = Guid.NewGuid();
    private const string AnaEmail = "ana@acme.com";

    private static UserNotification Entry(Guid? userId, string? email, DateTime createdAt, Guid? tenantId = null, DateTime? readAt = null) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId ?? CompanyId,
        RecipientUserId = userId,
        RecipientEmail = email,
        ApplicationType = ApprovalApplicationType.Leave,
        ApplicationId = Guid.NewGuid(),
        ApprovalInstanceId = Guid.NewGuid(),
        ApplicationTypeLabel = "Leave",
        ApplicantName = "Ben Cruz",
        StatusLabel = "Pending Your Approval",
        CreatedAt = createdAt,
        ReadAt = readAt,
    };

    private static async Task SeedAsync(SqliteHrmsContext db, params UserNotification[] entries)
    {
        await using var seed = db.NewContext(CompanyId);
        seed.UserNotifications.AddRange(entries);
        await seed.SaveChangesAsync();
    }

    private static UserNotificationService Sut(SqliteHrmsContext db) =>
        new(new UnitOfWorkService(db.NewContext(CompanyId)));

    [Fact]
    public async Task Mine_AreFoundByAccountIdOrLoginEmail_NewestFirst_WithTheUnreadCount()
    {
        using var db = new SqliteHrmsContext();
        var now = DateTime.UtcNow;
        var byId = Entry(AnaId, null, now.AddMinutes(-10));
        var byEmail = Entry(null, AnaEmail, now.AddMinutes(-1));
        var read = Entry(AnaId, null, now.AddMinutes(-30), readAt: now);
        await SeedAsync(db, byId, byEmail, read, Entry(Guid.NewGuid(), "ben@acme.com", now));

        var page = await Sut(db).GetMineAsync(AnaId, " Ana@Acme.com ", 1, 20, unreadOnly: false, default);

        page.Items.Select(x => x.Id).Should().Equal(byEmail.Id, byId.Id, read.Id);
        page.Total.Should().Be(3);
        page.UnreadCount.Should().Be(2);
    }

    [Fact]
    public async Task UnreadOnly_ListsOnlyUnread()
    {
        using var db = new SqliteHrmsContext();
        await SeedAsync(db, Entry(AnaId, null, DateTime.UtcNow), Entry(AnaId, null, DateTime.UtcNow, readAt: DateTime.UtcNow));

        var page = await Sut(db).GetMineAsync(AnaId, null, 1, 20, unreadOnly: true, default);

        page.Items.Should().ContainSingle().Which.ReadAt.Should().BeNull();
    }

    [Fact]
    public async Task AnotherCompanysEntries_AreNeverMine()
    {
        using var db = new SqliteHrmsContext();
        await SeedAsync(db, Entry(AnaId, AnaEmail, DateTime.UtcNow, tenantId: Guid.NewGuid()));

        var page = await Sut(db).GetMineAsync(AnaId, AnaEmail, 1, 20, unreadOnly: false, default);

        page.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task MarkRead_OnlyWorksOnMyOwnEntry()
    {
        using var db = new SqliteHrmsContext();
        var mine = Entry(AnaId, null, DateTime.UtcNow);
        var someoneElses = Entry(Guid.NewGuid(), null, DateTime.UtcNow);
        await SeedAsync(db, mine, someoneElses);

        var sut = Sut(db);
        (await sut.MarkReadAsync(AnaId, null, someoneElses.Id, default)).Should().BeFalse();
        (await sut.MarkReadAsync(AnaId, null, mine.Id, default)).Should().BeTrue();

        await using var check = db.NewContext();
        (await check.UserNotifications.SingleAsync(x => x.Id == mine.Id)).ReadAt.Should().NotBeNull();
        (await check.UserNotifications.SingleAsync(x => x.Id == someoneElses.Id)).ReadAt.Should().BeNull();
    }

    [Fact]
    public async Task MarkAllRead_ClearsMyUnread_AndLeavesOthersAlone()
    {
        using var db = new SqliteHrmsContext();
        var others = Entry(Guid.NewGuid(), "ben@acme.com", DateTime.UtcNow);
        await SeedAsync(db, Entry(AnaId, null, DateTime.UtcNow), Entry(null, AnaEmail, DateTime.UtcNow), others);

        var marked = await Sut(db).MarkAllReadAsync(AnaId, AnaEmail, default);

        marked.Should().Be(2);
        (await Sut(db).GetMineAsync(AnaId, AnaEmail, 1, 20, false, default)).UnreadCount.Should().Be(0);
        await using var check = db.NewContext();
        (await check.UserNotifications.SingleAsync(x => x.Id == others.Id)).ReadAt.Should().BeNull();
    }

    [Fact]
    public async Task Retention_RemovesOnlyEntriesOlderThanTheCutoff_InEveryCompany()
    {
        using var db = new SqliteHrmsContext();
        var now = DateTime.UtcNow;
        var recent = Entry(AnaId, null, now.AddDays(-10));
        await SeedAsync(db, Entry(AnaId, null, now.AddDays(-100)), Entry(AnaId, null, now.AddDays(-91), tenantId: Guid.NewGuid()), recent);

        await using var context = db.NewContext();
        var removed = await NotificationRetentionJob.PurgeAsync(context, now.AddDays(-90), default);

        removed.Should().Be(2);
        (await context.UserNotifications.IgnoreQueryFilters().Select(x => x.Id).ToListAsync()).Should().Equal(recent.Id);
    }
}
