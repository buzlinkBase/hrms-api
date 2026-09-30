using System.Security.Claims;
using Hrms.Core.Hubs;
using Hrms.Core.Messaging;
using Hrms.Core.Services.Approvals;
using Hrms.Core.Services.Notifications;
using Hrms.Domain.Entities;
using hrms.test.TestSupport;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using Onepunch.Common.Lib.DTO;

namespace hrms.test.ServiceTests;

/// <summary>
/// Live approval updates for the person the push is about (applicant or assigned approver).
/// Pushes used to target Clients.User(Employee.UserId) only, and UserId is set only for accounts
/// linked by invitation -- employees matched to their login by email got no live updates. Each
/// hub connection now joins a company + email group, and the worker targets it.
/// </summary>
public class ApprovalRecipientPushTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public void RecipientGroup_IgnoresEmailCaseAndSpacing()
    {
        ApprovalRecipientGroups.For(TenantId, "  Ana.Reyes@Company.com ")
            .Should().Be(ApprovalRecipientGroups.For(TenantId, "ana.reyes@company.com"));
    }

    [Fact]
    public void RecipientGroup_IsCompanyScoped()
    {
        ApprovalRecipientGroups.For(Guid.NewGuid(), "ana@company.com")
            .Should().NotBe(ApprovalRecipientGroups.For(TenantId, "ana@company.com"));
    }

    // ---- hub ----

    private static async Task<IGroupManager> ConnectAsync(params Claim[] claims)
    {
        var groups = Substitute.For<IGroupManager>();
        var context = Substitute.For<HubCallerContext>();
        context.ConnectionId.Returns("conn-1");
        context.User.Returns(new ClaimsPrincipal(new ClaimsIdentity(claims, "test")));
        var hub = new NotificationHub { Context = context, Groups = groups };

        await hub.OnConnectedAsync();
        return groups;
    }

    [Fact]
    public async Task Connect_JoinsTheCallersOwnRecipientGroup()
    {
        var groups = await ConnectAsync(new Claim("tenantId", TenantId.ToString()), new Claim("email", "Ana@Company.com"));

        await groups.Received(1).AddToGroupAsync("conn-1", ApprovalRecipientGroups.For(TenantId, "ana@company.com"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Connect_WithoutEmail_JoinsNoRecipientGroup()
    {
        var groups = await ConnectAsync(new Claim("tenantId", TenantId.ToString()));

        await groups.DidNotReceive().AddToGroupAsync(
            Arg.Any<string>(), Arg.Is<string>(g => g.StartsWith("approval-recipient:")), Arg.Any<CancellationToken>());
    }

    // ---- worker ----

    private static (ApprovalPushNotificationWorker Worker, IHubClients Clients) BuildWorker(Guid tenantId, SqliteHrmsContext db)
    {
        var hubContext = Substitute.For<IHubContext<NotificationHub>>();
        var tenantProvider = Substitute.For<ITenantProvider>();
        tenantProvider.TenantId.Returns(tenantId);
        var notifications = new UserNotificationService(new UnitOfWorkService(db.NewContext(tenantId)));
        return (new ApprovalPushNotificationWorker(new ApprovalPushNotificationService(hubContext), tenantProvider, notifications), hubContext.Clients);
    }

    // No company filter: the SQLite test context doesn't stamp TenantId the way production does.
    private static async Task<List<UserNotification>> SavedAsync(SqliteHrmsContext db)
    {
        await using var context = db.NewContext();
        return await context.UserNotifications.AsNoTracking().ToListAsync();
    }

    private static ConsumeContext<ApprovalNotificationRequested> Message(string email, Guid? userId, bool deliverPush = true, Guid? messageId = null)
    {
        var context = Substitute.For<ConsumeContext<ApprovalNotificationRequested>>();
        context.MessageId.Returns(messageId ?? Guid.NewGuid());
        context.Message.Returns(new ApprovalNotificationRequested
        {
            RecipientEmail = email,
            RecipientUserId = userId,
            DeliverPush = deliverPush,
            ApplicationType = "Leave",
            ApplicationId = Guid.NewGuid(),
            ApprovalInstanceId = Guid.NewGuid(),
            ApplicationTypeLabel = "Leave",
            ApplicantName = "Ana Reyes",
            StatusLabel = "Approved",
        });
        return context;
    }

    [Fact]
    public async Task Worker_TargetsTheRecipientGroup_EvenWithoutALinkedUserId()
    {
        using var db = new SqliteHrmsContext();
        var (worker, clients) = BuildWorker(TenantId, db);
        var group = Substitute.For<IClientProxy>();
        clients.Group(ApprovalRecipientGroups.For(TenantId, "ana@company.com")).Returns(group);

        await worker.Consume(Message("ana@company.com", userId: null));

        await group.Received(1).SendCoreAsync(
            ApprovalPushNotificationService.EventName,
            Arg.Is<object?[]>(args => ((ApprovalPushNotification)args[0]!).StatusLabel == "Approved"),
            Arg.Any<CancellationToken>());
        clients.DidNotReceive().User(Arg.Any<string>());
    }

    [Fact]
    public async Task Worker_WithoutEmail_FallsBackToTheUserId()
    {
        using var db = new SqliteHrmsContext();
        var (worker, clients) = BuildWorker(TenantId, db);
        var userId = Guid.NewGuid();
        var user = Substitute.For<IClientProxy>();
        clients.User(userId.ToString()).Returns(user);

        await worker.Consume(Message("", userId));

        await user.Received(1).SendCoreAsync(ApprovalPushNotificationService.EventName, Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Worker_PushDisabled_SendsNothing_AndSavesNothing()
    {
        using var db = new SqliteHrmsContext();
        var (worker, clients) = BuildWorker(TenantId, db);

        await worker.Consume(Message("ana@company.com", Guid.NewGuid(), deliverPush: false));

        clients.ReceivedCalls().Should().BeEmpty();
        (await SavedAsync(db)).Should().BeEmpty();
    }

    // ---- saved bell entries ----

    [Fact]
    public async Task Worker_SavesTheEntry_ThenPushesItWithTheSavedId()
    {
        using var db = new SqliteHrmsContext();
        var (worker, clients) = BuildWorker(TenantId, db);
        var group = Substitute.For<IClientProxy>();
        clients.Group(Arg.Any<string>()).Returns(group);
        var userId = Guid.NewGuid();

        await worker.Consume(Message(" Ana@Company.com ", userId));

        var saved = (await SavedAsync(db)).Should().ContainSingle().Subject;
        saved.RecipientEmail.Should().Be("ana@company.com");
        saved.RecipientUserId.Should().Be(userId);
        saved.StatusLabel.Should().Be("Approved");
        saved.ReadAt.Should().BeNull();
        await group.Received(1).SendCoreAsync(
            ApprovalPushNotificationService.EventName,
            Arg.Is<object?[]>(args => ((ApprovalPushNotification)args[0]!).Id == saved.Id),
            Arg.Any<CancellationToken>());
    }

    // Regression: saving the bell entry used to run before the push with nothing catching it, so a
    // missing UserNotifications table (migration not yet applied) faulted the message and nobody
    // was notified at all. Delivery never waits on storage now.
    [Fact]
    public async Task Worker_WhenSavingFails_StillPushes_WithoutAnId()
    {
        using var db = new SqliteHrmsContext();
        await using (var drop = db.NewContext())
            await drop.Database.ExecuteSqlRawAsync("DROP TABLE UserNotifications");
        var (worker, clients) = BuildWorker(TenantId, db);
        var group = Substitute.For<IClientProxy>();
        clients.Group(ApprovalRecipientGroups.For(TenantId, "owner@company.com")).Returns(group);

        await worker.Consume(Message("owner@company.com", userId: null));

        await group.Received(1).SendCoreAsync(
            ApprovalPushNotificationService.EventName,
            Arg.Is<object?[]>(args => ((ApprovalPushNotification)args[0]!).Id == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Worker_ARedeliveredMessage_DoesNotSaveASecondEntry()
    {
        using var db = new SqliteHrmsContext();
        var messageId = Guid.NewGuid();
        var (first, _) = BuildWorker(TenantId, db);
        await first.Consume(Message("ana@company.com", null, messageId: messageId));
        var (retry, _) = BuildWorker(TenantId, db);

        await retry.Consume(Message("ana@company.com", null, messageId: messageId));

        (await SavedAsync(db)).Should().ContainSingle().Which.Id.Should().Be(messageId);
    }
}
