using System.Security.Claims;
using Hrms.Core.Hubs;
using Hrms.Core.Messaging;
using Hrms.Core.Services.Approvals;
using MassTransit;
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

    private static (ApprovalPushNotificationWorker Worker, IHubClients Clients) BuildWorker(Guid tenantId)
    {
        var hubContext = Substitute.For<IHubContext<NotificationHub>>();
        var tenantProvider = Substitute.For<ITenantProvider>();
        tenantProvider.TenantId.Returns(tenantId);
        return (new ApprovalPushNotificationWorker(new ApprovalPushNotificationService(hubContext), tenantProvider), hubContext.Clients);
    }

    private static ConsumeContext<ApprovalNotificationRequested> Message(string email, Guid? userId, bool deliverPush = true)
    {
        var context = Substitute.For<ConsumeContext<ApprovalNotificationRequested>>();
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
        var (worker, clients) = BuildWorker(TenantId);
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
        var (worker, clients) = BuildWorker(TenantId);
        var userId = Guid.NewGuid();
        var user = Substitute.For<IClientProxy>();
        clients.User(userId.ToString()).Returns(user);

        await worker.Consume(Message("", userId));

        await user.Received(1).SendCoreAsync(ApprovalPushNotificationService.EventName, Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Worker_PushDisabled_SendsNothing()
    {
        var (worker, clients) = BuildWorker(TenantId);

        await worker.Consume(Message("ana@company.com", Guid.NewGuid(), deliverPush: false));

        clients.ReceivedCalls().Should().BeEmpty();
    }
}
