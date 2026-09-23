using System.Linq.Expressions;
using System.Security.Claims;
using FluentAssertions;
using Hrms.Api.Controllers;
using Hrms.Core.Services.Approvals;
using Hrms.Domain;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.Approvals;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Domain.ValueObjects;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MockQueryable.NSubstitute;
using NSubstitute;
using Onepunch.Common.Lib.DTO;
using Xunit;

namespace hrms.test.ControllerTests;

/// <summary>
/// ApprovalsController.Get -- specifically the new per-step ApproverLabel resolution
/// (ResolveApproverLabel), reused for every step of the workflow (not just the current one) so
/// the frontend's Approval Progress timeline can show upcoming steps' approver/department, not
/// just a step count. See ApprovalEngineServiceTests for the engine's own advance/quorum rules.
/// </summary>
public class ApprovalsControllerTests
{
    private static Employee BuildEmployee(Guid? id = null, string firstName = "Test", string lastName = "Employee") => new()
    {
        Id = id ?? Guid.NewGuid(),
        FirstName = firstName,
        LastName = lastName,
        EmployeeNo = "EMP-001",
        Skills = [], Educations = [], Dependents = [], EmployeeRecords = [], Employments = [], Assets = [], RestDays = [],
    };

    private static (ApprovalsController Controller, ApprovalEngineService Engine) BuildController(
        List<Employee> employees, List<ApprovalWorkflow> workflows, string permission)
    {
        var instances = new List<ApprovalInstance>();

        var repo = Substitute.For<IRepository>();
        repo.Find<Employee>(Arg.Any<Expression<Func<Employee, bool>>>())
            .Returns(call => employees.Where(call.Arg<Expression<Func<Employee, bool>>>().Compile()).ToList().BuildMockDbSet());
        repo.Find<ApprovalWorkflow>(Arg.Any<Expression<Func<ApprovalWorkflow, bool>>>())
            .Returns(call => workflows.Where(call.Arg<Expression<Func<ApprovalWorkflow, bool>>>().Compile()).ToList().BuildMockDbSet());
        repo.Find<ApprovalInstance>(Arg.Any<Expression<Func<ApprovalInstance, bool>>>())
            .Returns(call => instances.Where(call.Arg<Expression<Func<ApprovalInstance, bool>>>().Compile()).ToList().BuildMockDbSet());
        repo.AddAsync(Arg.Any<ApprovalInstance>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask())
            .AndDoes(call => instances.Add(call.Arg<ApprovalInstance>()));

        var uow = Substitute.For<IUnitOfWorkService>();
        uow.Repository.Returns(repo);
        var engine = new ApprovalEngineService(uow, Substitute.For<IPublishEndpoint>());

        // EmployeeService is never touched on this test's path -- the caller already holds the
        // coarse {Row}:View permission, so ApprovalsController.Get skips the applicant-lookup
        // branch that would otherwise need it. PayrollBatchService is only touched for
        // ApprovalApplicationType.PayrollPosting's dynamic permission-prefix lookup, never
        // exercised by this test's Leave/Overtime-type fixtures.
        var controller = new ApprovalsController(engine, null!, null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("permission", permission)])),
                },
            },
        };
        return (controller, engine);
    }

    [Fact]
    public async Task Get_MultiStepWorkflow_ReturnsResolvedApproverLabelForEveryStep()
    {
        var applicant = BuildEmployee();
        var step1Approver = BuildEmployee(firstName: "Jane", lastName: "Doe");
        var department = new Department { Id = Guid.NewGuid(), Name = "Finance" };
        var workflow = new ApprovalWorkflow
        {
            Id = Guid.NewGuid(),
            ApplicationType = ApprovalApplicationType.Leave,
            IsActive = true,
            Steps =
            [
                new ApprovalWorkflowStep
                {
                    StepNumber = 1, ApproverType = ApproverType.Person,
                    ApproverEmployeeId = step1Approver.Id, ApproverEmployee = step1Approver,
                    NamedApprovers = [],
                },
                new ApprovalWorkflowStep
                {
                    StepNumber = 2, ApproverType = ApproverType.Department,
                    ApproverDepartmentId = department.Id, ApproverDepartment = department,
                    NamedApprovers = [],
                },
                new ApprovalWorkflowStep
                {
                    StepNumber = 3, ApproverType = ApproverType.ApplicantManager,
                    NamedApprovers = [],
                },
            ],
        };
        var (controller, engine) = BuildController([applicant, step1Approver], [workflow], "Leave:View");
        var applicationId = Guid.NewGuid();
        await engine.StartAsync(ApprovalApplicationType.Leave, applicationId, applicant.Id, CancellationToken.None);

        var result = await controller.Get(ApprovalApplicationType.Leave, applicationId, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<ApprovalInstanceResponse>().Subject;
        response.Steps.Should().HaveCount(3);
        response.Steps[0].ApproverLabel.Should().Be("Jane Doe");
        response.Steps[1].ApproverLabel.Should().Be("Finance");
        response.Steps[2].ApproverLabel.Should().Be("Your Manager");
        // Step 1 is also still resolved as the current step, unaffected by adding Steps.
        response.CurrentStepApproverLabel.Should().Be("Jane Doe");
    }
}
