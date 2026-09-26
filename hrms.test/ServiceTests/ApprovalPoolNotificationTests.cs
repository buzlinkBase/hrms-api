using System.Linq.Expressions;
using Hrms.Core.Hubs;
using Hrms.Core.Messaging;
using Hrms.Core.Services.Approvals;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.Approvals;
using Hrms.Domain.Entities.EmployeeEntities;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using MockQueryable.NSubstitute;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>
/// The implicit fallback step (no workflow configured for a tenant/type) has no named approver,
/// so filing under it used to notify nobody -- e.g. an employee filing Overtime left the admin's
/// bell and Overtime list silent. It now goes to the approver group: every connected user whose
/// token carries that type's {Row}:Approve permission (Owner/Admin always).
/// </summary>
public class ApprovalPoolNotificationTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public void GroupsFor_OwnerOrAdmin_JoinsEveryTypesGroup()
    {
        var groups = ApproverGroups.GroupsFor(TenantId, isOwnerOrAdmin: true, new HashSet<string>()).ToList();

        groups.Should().HaveCount(Enum.GetValues<ApprovalApplicationType>().Length);
        groups.Should().Contain(ApproverGroups.For(TenantId, ApprovalApplicationType.PayrollPosting));
    }

    [Fact]
    public void GroupsFor_ApprovePermission_JoinsOnlyThatTypesGroup()
    {
        var groups = ApproverGroups.GroupsFor(TenantId, isOwnerOrAdmin: false, new HashSet<string> { "Overtime:Approve", "Leave:View" });

        groups.Should().Equal(ApproverGroups.For(TenantId, ApprovalApplicationType.Overtime));
    }

    [Fact]
    public void GroupsFor_NoApprovePermissions_JoinsNothing()
    {
        var groups = ApproverGroups.GroupsFor(TenantId, isOwnerOrAdmin: false, new HashSet<string> { "Overtime:View", "Overtime:Create" });

        groups.Should().BeEmpty();
    }

    [Fact]
    public void GroupsFor_DtrMasterApprove_CoversBothDtrTypes()
    {
        var groups = ApproverGroups.GroupsFor(TenantId, isOwnerOrAdmin: false, new HashSet<string> { "DTR Master:Approve" });

        groups.Should().BeEquivalentTo(
            ApproverGroups.For(TenantId, ApprovalApplicationType.Dtr),
            ApproverGroups.For(TenantId, ApprovalApplicationType.DtrDeletion));
    }

    [Fact]
    public void Groups_AreTenantScoped()
    {
        ApproverGroups.For(Guid.NewGuid(), ApprovalApplicationType.Overtime)
            .Should().NotBe(ApproverGroups.For(TenantId, ApprovalApplicationType.Overtime));
    }

    private static (ApprovalEngineService Service, IPublishEndpoint Publisher) BuildEngine(Employee applicant, List<ApprovalWorkflow> workflows)
    {
        var repo = Substitute.For<IRepository>();
        repo.Find<Employee>(Arg.Any<Expression<Func<Employee, bool>>>())
            .Returns(call => new[] { applicant }.Where(call.Arg<Expression<Func<Employee, bool>>>().Compile()).ToList().BuildMockDbSet());
        repo.Find<ApprovalWorkflow>(Arg.Any<Expression<Func<ApprovalWorkflow, bool>>>())
            .Returns(call => workflows.Where(call.Arg<Expression<Func<ApprovalWorkflow, bool>>>().Compile()).ToList().BuildMockDbSet());
        repo.Find<ApprovalInstance>(Arg.Any<Expression<Func<ApprovalInstance, bool>>>())
            .Returns(_ => new List<ApprovalInstance>().BuildMockDbSet());
        repo.Find<NotificationPreference>(Arg.Any<Expression<Func<NotificationPreference, bool>>>())
            .Returns(_ => new List<NotificationPreference>().BuildMockDbSet());

        var uow = Substitute.For<IUnitOfWorkService>();
        uow.Repository.Returns(repo);
        var publisher = Substitute.For<IPublishEndpoint>();
        return (new ApprovalEngineService(uow, publisher), publisher);
    }

    private static Employee BuildApplicant() => new()
    {
        Id = Guid.NewGuid(),
        FirstName = "Lawrence",
        LastName = "Heckler",
        EmployeeNo = "EMP-001",
        Skills = [], Educations = [], Dependents = [], EmployeeRecords = [], Employments = [], Assets = [], RestDays = [],
    };

    [Fact]
    public async Task StartAsync_NoConfiguredWorkflow_PublishesPoolNotification()
    {
        var applicant = BuildApplicant();
        var (service, publisher) = BuildEngine(applicant, []);
        var applicationId = Guid.NewGuid();

        var instance = await service.StartAsync(ApprovalApplicationType.Overtime, applicationId, applicant.Id, CancellationToken.None);

        await publisher.Received(1).Publish(
            Arg.Is<ApprovalPoolNotificationRequested>(m =>
                m.ApplicationType == ApprovalApplicationType.Overtime
                && m.ApplicationId == applicationId
                && m.ApprovalInstanceId == instance.Id
                && m.ApplicantName == "Lawrence Heckler"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_ConfiguredWorkflow_DoesNotPublishPoolNotification()
    {
        var applicant = BuildApplicant();
        var approver = BuildApplicant();
        var workflow = new ApprovalWorkflow
        {
            Id = Guid.NewGuid(),
            ApplicationType = ApprovalApplicationType.Overtime,
            IsActive = true,
            Steps = [new ApprovalWorkflowStep { StepNumber = 1, ApproverType = ApproverType.Person, ApproverEmployeeId = approver.Id, MinApprovals = 1, NamedApprovers = [] }],
        };
        var (service, publisher) = BuildEngine(applicant, [workflow]);

        await service.StartAsync(ApprovalApplicationType.Overtime, Guid.NewGuid(), applicant.Id, CancellationToken.None);

        await publisher.DidNotReceive().Publish(Arg.Any<ApprovalPoolNotificationRequested>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Worker_PushesToTheTenantsApproverGroupForThatType()
    {
        var hubContext = Substitute.For<IHubContext<NotificationHub>>();
        var groupProxy = Substitute.For<IClientProxy>();
        var expectedGroup = ApproverGroups.For(TenantId, ApprovalApplicationType.Overtime);
        hubContext.Clients.Group(expectedGroup).Returns(groupProxy);

        var tenantProvider = Substitute.For<ITenantProvider>();
        tenantProvider.TenantId.Returns(TenantId);

        var worker = new ApprovalPoolPushNotificationWorker(new ApprovalPushNotificationService(hubContext), tenantProvider);
        var message = new ApprovalPoolNotificationRequested
        {
            ApplicationType = ApprovalApplicationType.Overtime,
            ApplicationId = Guid.NewGuid(),
            ApprovalInstanceId = Guid.NewGuid(),
            ApplicationTypeLabel = "Overtime",
            ApplicantName = "Lawrence Heckler",
        };
        var context = Substitute.For<ConsumeContext<ApprovalPoolNotificationRequested>>();
        context.Message.Returns(message);

        await worker.Consume(context);

        await groupProxy.Received(1).SendCoreAsync(
            ApprovalPushNotificationService.EventName,
            Arg.Is<object?[]>(args =>
                args.Length == 1
                && ((ApprovalPushNotification)args[0]!).ApplicationId == message.ApplicationId
                && ((ApprovalPushNotification)args[0]!).StatusLabel == ApprovalEngineService.PendingApprovalStatusLabel),
            Arg.Any<CancellationToken>());
    }
}
