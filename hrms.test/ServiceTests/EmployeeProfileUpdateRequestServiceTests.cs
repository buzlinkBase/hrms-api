using System.Linq.Expressions;
using Hrms.Core.Services.Approvals;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.Approvals;
using Hrms.Domain.Entities.EmployeeEntities;
using MassTransit;
using MockQueryable.NSubstitute;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>
/// EmployeeProfileUpdateRequestService.AddAsync/ApproveAsync/DeclineAsync/WithdrawAsync -- all
/// four stay on the GetOneAsync/Repository.FindOneAsync/GetQueryable pattern (never raw Context),
/// so they're fully testable with the same IRepository mocking ApprovalEngineServiceTests uses for
/// the real engine underneath. FindAllAsync/FindOneWithCurrentValuesAsync are NOT covered here --
/// they read via raw Context.EmployeeProfileUpdateRequests.Include(...) for the list/detail views,
/// the same class of limitation already flagged for PassSlipApplicationService's own
/// FindAllAsync/FineOneAsync (no test file exists for that service either).
/// </summary>
public class EmployeeProfileUpdateRequestServiceTests
{
    private static Employee BuildEmployee(Guid? id = null, DateTime? updatedAt = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        EmployeeNo = "EMP-001",
        FirstName = "Test",
        LastName = "Employee",
        Contact = "09170000000",
        Address1 = "Old Address",
        CivilStatus = "Single",
        BloodType = "O+",
        UpdatedAt = updatedAt,
        Skills = [], Educations = [], Dependents = [], EmployeeRecords = [], Employments = [], Assets = [], RestDays = [],
    };

    private static (EmployeeProfileUpdateRequestService Service, List<EmployeeProfileUpdateRequest> Requests, List<ApprovalInstance> Instances) BuildService(
        List<Employee> employees, List<ApprovalWorkflow> workflows)
    {
        var instances = new List<ApprovalInstance>();
        var actions = new List<ApprovalAction>();
        var requests = new List<EmployeeProfileUpdateRequest>();

        var repo = Substitute.For<IRepository>();
        repo.FindOneAsync<Employee>(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => employees.SingleOrDefault(e => e.Id == call.Arg<Guid>()));
        repo.Find<Employee>(Arg.Any<Expression<Func<Employee, bool>>>())
            .Returns(call => employees.Where(call.Arg<Expression<Func<Employee, bool>>>().Compile()).ToList().BuildMockDbSet());
        repo.Find<ApprovalWorkflow>(Arg.Any<Expression<Func<ApprovalWorkflow, bool>>>())
            .Returns(call => workflows.Where(call.Arg<Expression<Func<ApprovalWorkflow, bool>>>().Compile()).ToList().BuildMockDbSet());
        repo.Find<ApprovalInstance>(Arg.Any<Expression<Func<ApprovalInstance, bool>>>())
            .Returns(call => instances.Where(call.Arg<Expression<Func<ApprovalInstance, bool>>>().Compile()).ToList().BuildMockDbSet());
        // No NotificationPreference rows in any of these tests -- ResolveDeliveryFlagsAsync's
        // opt-out default (both channels on) is exactly what every test here expects.
        repo.Find<NotificationPreference>(Arg.Any<Expression<Func<NotificationPreference, bool>>>())
            .Returns(call => new List<NotificationPreference>().BuildMockDbSet());
        repo.AddAsync(Arg.Any<ApprovalInstance>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask())
            .AndDoes(call => instances.Add(call.Arg<ApprovalInstance>()));
        repo.AddAsync(Arg.Any<ApprovalAction>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask())
            .AndDoes(call =>
            {
                var action = call.Arg<ApprovalAction>();
                actions.Add(action);
                instances.Single(i => i.Id == action.ApprovalInstanceId).Actions.Add(action);
            });

        repo.FindOneAsync<EmployeeProfileUpdateRequest>(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => requests.SingleOrDefault(r => r.Id == call.Arg<Guid>()));
        // GetQueryable(Expression<...>) (used by AddAsync's open-request check) goes through
        // BaseService.GetQueryable(bool) -> Repository.FindAll<T>().Where(...), not Find<T>(...).
        repo.FindAll<EmployeeProfileUpdateRequest>().Returns(_ => requests.BuildMockDbSet());
        repo.AddAsync(Arg.Any<EmployeeProfileUpdateRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask())
            .AndDoes(call => requests.Add(call.Arg<EmployeeProfileUpdateRequest>()));

        var uow = Substitute.For<IUnitOfWorkService>();
        uow.Repository.Returns(repo);
        uow.CommitChangesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));

        var approvalEngine = new ApprovalEngineService(uow, Substitute.For<IPublishEndpoint>());
        return (new EmployeeProfileUpdateRequestService(uow, approvalEngine), requests, instances);
    }

    [Fact]
    public async Task AddAsync_NoWorkflowConfigured_CreatesForApprovalRequestAndStartsInstance()
    {
        var employee = BuildEmployee(updatedAt: new DateTime(2026, 1, 1));
        var (service, requests, instances) = BuildService([employee], []);

        var model = new EmployeeProfileUpdateRequest { Id = Guid.NewGuid(), EmployeeId = employee.Id, NewContact = "09171111111" };
        await service.AddAsync(model, CancellationToken.None);

        requests.Should().ContainSingle();
        requests[0].ApprovalStatus.Should().Be(ApprovalStatus.ForApproval);
        requests[0].EmployeeSnapshotUpdatedAt.Should().Be(employee.UpdatedAt);
        instances.Should().ContainSingle();
    }

    [Fact]
    public async Task AddAsync_AlreadyHasOpenRequest_Throws()
    {
        var employee = BuildEmployee();
        var (service, requests, _) = BuildService([employee], []);
        requests.Add(new EmployeeProfileUpdateRequest { Id = Guid.NewGuid(), EmployeeId = employee.Id, ApprovalStatus = ApprovalStatus.ForApproval });

        var act = () => service.AddAsync(new EmployeeProfileUpdateRequest { EmployeeId = employee.Id }, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ApproveAsync_NoWorkflowConfigured_FallbackSingleApproval_UpdatesEmployee()
    {
        var employee = BuildEmployee(updatedAt: new DateTime(2026, 1, 1));
        var approver = BuildEmployee();
        var (service, requests, _) = BuildService([employee, approver], []);

        var model = new EmployeeProfileUpdateRequest { Id = Guid.NewGuid(), EmployeeId = employee.Id, NewContact = "09171111111", NewCivilStatus = "Married" };
        await service.AddAsync(model, CancellationToken.None);

        var result = await service.ApproveAsync(model.Id, approver.Id, approverHasOverride: false, note: null, forceApply: false, CancellationToken.None);

        result.HasConflict.Should().BeFalse();
        employee.Contact.Should().Be("09171111111");
        employee.CivilStatus.Should().Be("Married");
        requests.Single(r => r.Id == model.Id).ApprovalStatus.Should().Be(ApprovalStatus.Approved);
    }

    [Fact]
    public async Task DeclineAsync_LeavesEmployeeUntouched()
    {
        var employee = BuildEmployee(updatedAt: new DateTime(2026, 1, 1));
        var approver = BuildEmployee();
        var (service, requests, _) = BuildService([employee, approver], []);

        var model = new EmployeeProfileUpdateRequest { Id = Guid.NewGuid(), EmployeeId = employee.Id, NewContact = "09171111111" };
        await service.AddAsync(model, CancellationToken.None);

        await service.DeclineAsync(model.Id, approver.Id, approverHasOverride: false, note: "Not eligible", CancellationToken.None);

        employee.Contact.Should().Be("09170000000");
        requests.Single(r => r.Id == model.Id).ApprovalStatus.Should().Be(ApprovalStatus.Declined);
    }

    [Fact]
    public async Task ApproveAsync_EmployeeChangedSinceSubmission_ReturnsConflictWithoutRecordingAction()
    {
        var employee = BuildEmployee(updatedAt: new DateTime(2026, 1, 1));
        var approver = BuildEmployee();
        var (service, requests, instances) = BuildService([employee, approver], []);

        var model = new EmployeeProfileUpdateRequest { Id = Guid.NewGuid(), EmployeeId = employee.Id, NewContact = "09171111111" };
        await service.AddAsync(model, CancellationToken.None);

        // HR edits the employee directly while the request is still pending.
        employee.UpdatedAt = new DateTime(2026, 1, 2);

        var result = await service.ApproveAsync(model.Id, approver.Id, approverHasOverride: false, note: null, forceApply: false, CancellationToken.None);

        result.HasConflict.Should().BeTrue();
        employee.Contact.Should().Be("09170000000");
        requests.Single(r => r.Id == model.Id).ApprovalStatus.Should().Be(ApprovalStatus.ForApproval);
        instances.Single().Actions.Should().BeEmpty();
    }

    [Fact]
    public async Task ApproveAsync_ForceApply_AppliesDespiteConflict()
    {
        var employee = BuildEmployee(updatedAt: new DateTime(2026, 1, 1));
        var approver = BuildEmployee();
        var (service, requests, _) = BuildService([employee, approver], []);

        var model = new EmployeeProfileUpdateRequest { Id = Guid.NewGuid(), EmployeeId = employee.Id, NewContact = "09171111111" };
        await service.AddAsync(model, CancellationToken.None);

        employee.UpdatedAt = new DateTime(2026, 1, 2);

        var result = await service.ApproveAsync(model.Id, approver.Id, approverHasOverride: false, note: null, forceApply: true, CancellationToken.None);

        result.HasConflict.Should().BeFalse();
        employee.Contact.Should().Be("09171111111");
        requests.Single(r => r.Id == model.Id).ApprovalStatus.Should().Be(ApprovalStatus.Approved);
    }

    [Fact]
    public async Task WithdrawAsync_MarksWithdrawn_AndLeavesEmployeeUntouched()
    {
        var employee = BuildEmployee();
        var (service, requests, _) = BuildService([employee], []);

        var model = new EmployeeProfileUpdateRequest { Id = Guid.NewGuid(), EmployeeId = employee.Id, NewContact = "09171111111" };
        await service.AddAsync(model, CancellationToken.None);

        await service.WithdrawAsync(model.Id, employee.Id, CancellationToken.None);

        requests.Single(r => r.Id == model.Id).ApprovalStatus.Should().Be(ApprovalStatus.Withdrawn);
        employee.Contact.Should().Be("09170000000");
    }

    [Fact]
    public async Task WithdrawAsync_WrongEmployee_Throws()
    {
        var employee = BuildEmployee();
        var stranger = BuildEmployee();
        var (service, requests, _) = BuildService([employee, stranger], []);

        var model = new EmployeeProfileUpdateRequest { Id = Guid.NewGuid(), EmployeeId = employee.Id, NewContact = "09171111111" };
        await service.AddAsync(model, CancellationToken.None);

        var act = () => service.WithdrawAsync(model.Id, stranger.Id, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
