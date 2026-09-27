using Hrms.Core.Services;
using Hrms.Core.Services.Approvals;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Infrastructure;
using hrms.test.TestSupport;
using Mapster;
using MapsterMapper;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>
/// An application's status is never set by whoever files or edits it -- only the approval engine
/// (ApproveAsync/DeclineAsync) moves it. Previously a plain edit PUT copied the payload's status
/// straight onto the record (e.g. Declined -> Approved with no approver), and editing a pending
/// loan silently approved it (the rebuilt entity fell back to its Approved default).
///
/// Real relational provider (SQLite, via SqliteHrmsContext) since these services read through
/// Context directly and the approval engine's rows carry real FKs to Employee.
/// </summary>
public class ApplicationStatusLockdownTests
{
    private static async Task<Employee> SeedEmployeeAsync(HrmsContext seed)
    {
        var payrollGroupId = Guid.NewGuid();
        seed.PayrollGroups.Add(new PayrollGroup { Id = payrollGroupId });
        var employee = new Employee { Id = Guid.NewGuid(), PayrollGroupId = payrollGroupId };
        seed.Employees.Add(employee);
        await seed.SaveChangesAsync();
        return employee;
    }

    private static ApprovalEngineService Engine(IUnitOfWorkService uow) => new(uow, Substitute.For<IPublishEndpoint>());

    // ---- Overtime ----

    private static async Task<(SqliteHrmsContext Db, Guid Id, Guid EmployeeId)> SeedOvertimeAsync(ApprovalStatus status)
    {
        var db = new SqliteHrmsContext();
        await using var seed = db.NewContext();
        var employee = await SeedEmployeeAsync(seed);
        var id = Guid.NewGuid();
        seed.OTApplications.Add(new OverTimeApplication
        {
            Id = id, EmployeeId = employee.Id, Employee = null!, OTDate = new DateOnly(2026, 9, 17), ApprovalStatus = status,
        });
        await seed.SaveChangesAsync();
        return (db, id, employee.Id);
    }

    [Theory]
    [InlineData(ApprovalStatus.ForApproval, ApprovalStatus.Approved)]
    [InlineData(ApprovalStatus.Declined, ApprovalStatus.Approved)]
    [InlineData(ApprovalStatus.Approved, ApprovalStatus.ForApproval)]
    public async Task Overtime_Edit_NeverChangesStatus(ApprovalStatus stored, ApprovalStatus requested)
    {
        var (db, id, employeeId) = await SeedOvertimeAsync(stored);
        using var _ = db;
        await using (var context = db.NewContext())
        {
            var uow = new UnitOfWorkService(context);
            var service = new OvertimeApplicationService(uow, Engine(uow));
            await service.UpdateAsync(new UpdateOvertimeApplication
            {
                Id = id, EmployeeId = employeeId, OTDate = new DateOnly(2026, 9, 18), Remarks = "edited", ApprovalStatus = requested,
            }, CancellationToken.None);
        }

        await using var verify = db.NewContext();
        var saved = await verify.OTApplications.SingleAsync(x => x.Id == id);
        saved.ApprovalStatus.Should().Be(stored);
        saved.Remarks.Should().Be("edited", "the field edit itself still applies");
    }

    [Fact]
    public async Task Overtime_Approve_GoesThroughTheEngine()
    {
        var (db, id, employeeId) = await SeedOvertimeAsync(ApprovalStatus.ForApproval);
        using var _ = db;
        await using (var context = db.NewContext())
        {
            var uow = new UnitOfWorkService(context);
            var service = new OvertimeApplicationService(uow, Engine(uow));
            await service.ApproveAsync(id, employeeId, approverHasOverride: true, note: null, CancellationToken.None);
        }

        await using var verify = db.NewContext();
        (await verify.OTApplications.SingleAsync(x => x.Id == id)).ApprovalStatus.Should().Be(ApprovalStatus.Approved);
        (await verify.ApprovalInstances.AnyAsync(x => x.ApplicationId == id && x.ApplicationType == ApprovalApplicationType.Overtime))
            .Should().BeTrue("the decision is recorded on the approval instance, not written directly");
    }

    [Fact]
    public async Task Overtime_Decline_NotAwaitingApproval_Throws()
    {
        var (db, id, employeeId) = await SeedOvertimeAsync(ApprovalStatus.Approved);
        using var _ = db;
        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var service = new OvertimeApplicationService(uow, Engine(uow));

        var act = () => service.DeclineAsync(id, employeeId, approverHasOverride: true, note: null, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not awaiting approval*");
    }

    [Fact]
    public async Task Overtime_UpdateOwn_SomeoneElsesApplication_IsNotFound()
    {
        var (db, id, employeeId) = await SeedOvertimeAsync(ApprovalStatus.ForApproval);
        using var _ = db;
        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var service = new OvertimeApplicationService(uow, Engine(uow));

        var act = () => service.UpdateOwnAsync(
            new UpdateOvertimeApplication { Id = id, EmployeeId = employeeId, OTDate = new DateOnly(2026, 9, 18) },
            Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Overtime_UpdateOwn_AlreadyApproved_IsRejected()
    {
        var (db, id, employeeId) = await SeedOvertimeAsync(ApprovalStatus.Approved);
        using var _ = db;
        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var service = new OvertimeApplicationService(uow, Engine(uow));

        var act = () => service.UpdateOwnAsync(
            new UpdateOvertimeApplication { Id = id, EmployeeId = employeeId, OTDate = new DateOnly(2026, 9, 18) },
            employeeId, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*still awaiting approval*");
    }

    // End to end on a real 2-step workflow: step 1 approves, the employee edits, and the chain
    // goes back to step 1 in a new cycle instead of carrying the approval over to changed content.
    [Fact]
    public async Task Overtime_UpdateOwn_AfterStep1Approval_SendsItBackToTheFirstApprover()
    {
        using var db = new SqliteHrmsContext();
        var id = Guid.NewGuid();
        Employee applicant, step1, step2;
        await using (var seed = db.NewContext())
        {
            applicant = await SeedEmployeeAsync(seed);
            step1 = await SeedEmployeeAsync(seed);
            step2 = await SeedEmployeeAsync(seed);
            seed.Add(new Hrms.Domain.Entities.Approvals.ApprovalWorkflow
            {
                Id = Guid.NewGuid(),
                Name = "OT two-step",
                ApplicationType = ApprovalApplicationType.Overtime,
                IsActive = true,
                Steps =
                [
                    new() { Id = Guid.NewGuid(), StepNumber = 1, ApproverType = ApproverType.Person, ApproverEmployeeId = step1.Id, MinApprovals = 1, NamedApprovers = [] },
                    new() { Id = Guid.NewGuid(), StepNumber = 2, ApproverType = ApproverType.Person, ApproverEmployeeId = step2.Id, MinApprovals = 1, NamedApprovers = [] },
                ],
            });
            await seed.SaveChangesAsync();
        }

        await using (var context = db.NewContext())
        {
            var uow = new UnitOfWorkService(context);
            await new OvertimeApplicationService(uow, Engine(uow)).AddAsync(new OverTimeApplication
            {
                Id = id, EmployeeId = applicant.Id, Employee = null!, OTDate = new DateOnly(2026, 9, 17),
                ApprovalStatus = ApprovalStatus.ForApproval,
            }, CancellationToken.None);
        }
        await using (var context = db.NewContext())
        {
            var uow = new UnitOfWorkService(context);
            await new OvertimeApplicationService(uow, Engine(uow))
                .ApproveAsync(id, step1.Id, approverHasOverride: false, note: null, CancellationToken.None);
        }
        await using (var verify = db.NewContext())
            (await verify.ApprovalInstances.SingleAsync(x => x.ApplicationId == id)).CurrentStepNumber.Should().Be(2);

        await using (var context = db.NewContext())
        {
            var uow = new UnitOfWorkService(context);
            await new OvertimeApplicationService(uow, Engine(uow)).UpdateOwnAsync(new UpdateOvertimeApplication
            {
                Id = id, EmployeeId = applicant.Id, OTDate = new DateOnly(2026, 9, 18), Remarks = "changed",
            }, applicant.Id, CancellationToken.None);
        }

        await using var after = db.NewContext();
        var instance = await after.ApprovalInstances.SingleAsync(x => x.ApplicationId == id);
        instance.CurrentStepNumber.Should().Be(1);
        instance.CycleNumber.Should().Be(2);
        (await after.OTApplications.SingleAsync(x => x.Id == id)).ApprovalStatus.Should().Be(ApprovalStatus.ForApproval);
    }

    // ---- Official Business ----

    [Fact]
    public async Task OfficialBusiness_Edit_NeverChangesStatus()
    {
        using var db = new SqliteHrmsContext();
        var id = Guid.NewGuid();
        Guid employeeId;
        await using (var seed = db.NewContext())
        {
            employeeId = (await SeedEmployeeAsync(seed)).Id;
            seed.TravelOrderApplications.Add(new TravelOrderApplication { Id = id, EmployeeId = employeeId, ApprovalStatus = ApprovalStatus.Declined });
            await seed.SaveChangesAsync();
        }

        await using (var context = db.NewContext())
        {
            var uow = new UnitOfWorkService(context);
            await new TravelOrderApplicationService(uow, Engine(uow)).UpdateAsync(new UpdateTravelOrderApplication
            {
                Id = id, EmployeeId = employeeId, Destination = "Client site", ApprovalStatus = ApprovalStatus.Approved,
            }, CancellationToken.None);
        }

        await using var verify = db.NewContext();
        var saved = await verify.TravelOrderApplications.SingleAsync(x => x.Id == id);
        saved.ApprovalStatus.Should().Be(ApprovalStatus.Declined);
        saved.Destination.Should().Be("Client site");
    }

    // ---- Leave ----

    private static LeaveApplicationService BuildLeaveService(IUnitOfWorkService uow) => new(
        uow, TypeAdapterConfig.GlobalSettings, Substitute.For<IMapper>(), Substitute.For<IPublishEndpoint>(),
        Substitute.For<ILogger<LeaveApplicationService>>(), null!, Engine(uow));

    private static async Task<(SqliteHrmsContext Db, Guid Id, Guid EmployeeId, Guid LeaveId)> SeedLeaveAsync(ApprovalStatus status)
    {
        var db = new SqliteHrmsContext();
        await using var seed = db.NewContext();
        var employee = await SeedEmployeeAsync(seed);
        var leave = new Leave { Id = Guid.NewGuid(), Code = "VL", Description = "Vacation Leave" };
        seed.Add(leave);
        var id = Guid.NewGuid();
        seed.leaveApplications.Add(new LeaveApplication
        {
            Id = id, EmployeeId = employee.Id, LeaveId = leave.Id, Leave = leave,
            LeaveDateFrom = new DateOnly(2026, 9, 21), LeaveDateTo = new DateOnly(2026, 9, 21), ApprovalStatus = status,
        });
        await seed.SaveChangesAsync();
        return (db, id, employee.Id, leave.Id);
    }

    [Fact]
    public async Task Leave_Edit_NeverChangesStatus()
    {
        var (db, id, employeeId, leaveId) = await SeedLeaveAsync(ApprovalStatus.ForApproval);
        using var _ = db;
        await using (var context = db.NewContext())
        {
            await BuildLeaveService(new UnitOfWorkService(context)).UpdateAsync(new UpdateLeaveApplication
            {
                Id = id, EmployeeId = employeeId, LeaveId = leaveId,
                LeaveDateFrom = new DateOnly(2026, 9, 21), LeaveDateTo = new DateOnly(2026, 9, 21),
                ApprovalStatus = ApprovalStatus.Approved,
            }, CancellationToken.None);
        }

        await using var verify = db.NewContext();
        (await verify.leaveApplications.SingleAsync(x => x.Id == id)).ApprovalStatus.Should().Be(ApprovalStatus.ForApproval);
    }

    [Fact]
    public async Task Leave_Approve_GoesThroughTheEngine()
    {
        var (db, id, employeeId, _) = await SeedLeaveAsync(ApprovalStatus.ForApproval);
        using var __ = db;
        await using (var context = db.NewContext())
        {
            await BuildLeaveService(new UnitOfWorkService(context))
                .ApproveAsync(id, employeeId, approverHasOverride: true, note: null, CancellationToken.None);
        }

        await using var verify = db.NewContext();
        (await verify.leaveApplications.SingleAsync(x => x.Id == id)).ApprovalStatus.Should().Be(ApprovalStatus.Approved);
    }

    // ---- Pass Slip ----

    [Fact]
    public async Task PassSlip_Edit_NeverChangesStatus()
    {
        using var db = new SqliteHrmsContext();
        var id = Guid.NewGuid();
        Guid employeeId;
        await using (var seed = db.NewContext())
        {
            employeeId = (await SeedEmployeeAsync(seed)).Id;
            seed.PassSlipApplications.Add(new PassSlipApplication
            {
                Id = id, EmployeeId = employeeId, ApplicationDate = new DateOnly(2026, 9, 21),
                DepartureTime = new DateTime(2026, 9, 21, 10, 0, 0), ApprovalStatus = ApprovalStatus.ForApproval,
            });
            await seed.SaveChangesAsync();
        }

        await using (var context = db.NewContext())
        {
            var uow = new UnitOfWorkService(context);
            var service = new PassSlipApplicationService(uow, new AttendanceService(uow), Engine(uow));
            await service.UpdateAsync(new UpdatePassSlipApplication
            {
                Id = id, EmployeeId = employeeId, ApplicationDate = new DateOnly(2026, 9, 21),
                DepartureTime = new DateTime(2026, 9, 21, 10, 0, 0), Remarks = "Bank errand", ApprovalStatus = ApprovalStatus.Approved,
            }, CancellationToken.None);
        }

        await using var verify = db.NewContext();
        (await verify.PassSlipApplications.SingleAsync(x => x.Id == id)).ApprovalStatus.Should().Be(ApprovalStatus.ForApproval);
    }

    // ---- Loans & Deductions ----

    // DeductionApplicationModel used to carry no status at all, so the admin Loans list always
    // showed its "Approved" fallback and never offered approve/decline on a pending loan.
    [Fact]
    public void Loan_ResponseModel_CarriesApprovalStatus()
    {
        TypeAdapterConfig.GlobalSettings.Scan(typeof(MappingProfile).Assembly);
        var entity = new DeductionApplication { Id = Guid.NewGuid(), Note = "", Remarks = "", Breakdown = [], ApprovalStatus = ApprovalStatus.ForApproval };

        entity.Adapt<DeductionApplicationModel>().ApprovalStatus.Should().Be(ApprovalStatus.ForApproval);
    }

    [Fact]
    public async Task Loan_Edit_KeepsPendingLoanForApproval()
    {
        using var db = new SqliteHrmsContext();
        var id = Guid.NewGuid();
        Guid employeeId;
        var deductionId = Guid.NewGuid();
        await using (var seed = db.NewContext())
        {
            employeeId = (await SeedEmployeeAsync(seed)).Id;
            seed.Add(new Deduction { Id = deductionId, Code = "COLOAN", Name = "Company Loan" });
            seed.Add(new DeductionApplication
            {
                Id = id, EmployeeId = employeeId, DeductionId = deductionId, Note = "", Remarks = "",
                Breakdown = [], ApprovalStatus = ApprovalStatus.ForApproval,
            });
            await seed.SaveChangesAsync();
        }

        TypeAdapterConfig.GlobalSettings.Scan(typeof(MappingProfile).Assembly);
        await using (var context = db.NewContext())
        {
            var uow = new UnitOfWorkService(context);
            var engine = Engine(uow);
            var employeeService = new EmployeeService(
                uow, Substitute.For<IMapper>(), TypeAdapterConfig.GlobalSettings,
                new DepartmentService(uow), new PayrollGroupService(uow), new BranchService(uow),
                new CostCenterService(uow), new PositionService(uow), new SectionService(uow));
            var service = new DeductionApplicationService(
                uow, new DeductionService(uow), employeeService, new Mapper(TypeAdapterConfig.GlobalSettings), engine);

            await service.UpdateAsync(new UpdateDeductionApplication
            {
                Id = id, EmployeeId = employeeId, DeductionId = deductionId, Remarks = "edited", Details = [], Breakdown = [],
                StartDate = new DateOnly(2026, 10, 1), EndDate = new DateOnly(2027, 3, 31),
            }, CancellationToken.None);
        }

        await using var verify = db.NewContext();
        var saved = await verify.Set<DeductionApplication>().SingleAsync(x => x.Id == id);
        saved.ApprovalStatus.Should().Be(ApprovalStatus.ForApproval, "an edit must not approve a pending loan");
        saved.Remarks.Should().Be("edited");
    }
}
