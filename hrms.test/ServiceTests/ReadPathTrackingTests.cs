using Hrms.Core.Services.Approvals;
using Hrms.Domain.Entities.Approvals;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Infrastructure;
using hrms.test.TestSupport;
using MapsterMapper;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>
/// Read paths that run on every DTR compute / payroll run (and a few busy lists) load their rows
/// untracked: nothing ends up in the change tracker, so a large read doesn't pay for snapshotting
/// and change detection. Plus the fixes found alongside: inactive recurring holidays no longer
/// apply, and the raw-logs Payroll Group / Area / Client filters filter on the right columns.
/// </summary>
public class ReadPathTrackingTests
{
    private static readonly DateOnly From = new(2026, 3, 1);
    private static readonly DateOnly To = new(2026, 3, 31);

    private static async Task<(SqliteHrmsContext Db, Employee Employee)> SeedAsync()
    {
        var db = new SqliteHrmsContext();
        await using var seed = db.NewContext();
        var payrollGroupId = Guid.NewGuid();
        seed.PayrollGroups.Add(new PayrollGroup { Id = payrollGroupId });
        var employee = new Employee { Id = Guid.NewGuid(), PayrollGroupId = payrollGroupId, LastName = "Reyes", FirstName = "Ana" };
        seed.Employees.Add(employee);

        seed.GeneralSettings.Add(new GeneralSetting { Id = Guid.NewGuid(), IdentityType = "Company", Description = "OTEligibility", Value = "x" });
        seed.Holidays.AddRange(
            new Holiday { Id = Guid.NewGuid(), Description = "Active one-off", HolDate = new DateOnly(2026, 3, 10), HolYear = 2026 },
            new Holiday { Id = Guid.NewGuid(), Description = "Active recurring", HolDate = new DateOnly(2020, 3, 20), HolYear = 2020, IsRecuring = true },
            new Holiday { Id = Guid.NewGuid(), Description = "Inactive recurring", HolDate = new DateOnly(2020, 3, 25), HolYear = 2020, IsRecuring = true, Status = "Inactive" });
        seed.TravelOrderApplications.Add(new TravelOrderApplication
        {
            Id = Guid.NewGuid(), EmployeeId = employee.Id, StartDate = new DateOnly(2026, 3, 5), EndDate = new DateOnly(2026, 3, 6),
            ApprovalStatus = ApprovalStatus.Approved,
        });
        seed.UTApplications.Add(new UnderTimeApplication
        {
            Id = Guid.NewGuid(), EmployeeId = employee.Id, PayrollDate = new DateOnly(2026, 3, 7), ApprovalStatus = ApprovalStatus.Approved,
        });
        await seed.SaveChangesAsync();
        return (db, employee);
    }

    [Fact]
    public async Task DtrAndPayrollHotReads_LeaveNothingInTheChangeTracker()
    {
        var (db, employee) = await SeedAsync();
        using var _ = db;
        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var employees = new HashSet<Guid> { employee.Id };

        (await new DTR.Core.GeneralSettingService(uow).GetSettingsAsync("Company")).Should().NotBeEmpty();
        (await new HolidayService(uow, Substitute.For<IMapper>()).GetAllHolidays(From, To, default)).Should().NotBeEmpty();
        (await new TravelOrderApplicationService(uow, new ApprovalEngineService(uow, Substitute.For<IPublishEndpoint>()))
            .FindByDateRangeAsync(From, To, employees, default)).Should().NotBeEmpty();
        (await new UnderTimeApplicationService(uow).FindByDateRangeAsync(From, To, employees, default)).Should().NotBeEmpty();
        await new ChangeRestDayService(uow).GetChangeRestDays(From, To, employees, default);

        context.ChangeTracker.Entries().Should().BeEmpty();
    }

    // Operator precedence used to read "IsRecuring || (inRange && Active)".
    [Fact]
    public async Task GetAllHolidays_SkipsInactiveRecurringHolidays()
    {
        var (db, _) = await SeedAsync();
        using var __ = db;
        await using var context = db.NewContext();

        var holidays = await new HolidayService(new UnitOfWorkService(context), Substitute.For<IMapper>()).GetAllHolidays(From, To, default);

        holidays.Select(x => x.Description).Should().BeEquivalentTo(["Active one-off", "Active recurring"]);
    }

    [Fact]
    public async Task ApprovalInstanceReadOnly_IsUntracked_AndCarriesItsCollections()
    {
        var (db, employee) = await SeedAsync();
        using var _ = db;
        var applicationId = Guid.NewGuid();
        await using (var seed = db.NewContext())
        {
            var workflow = new ApprovalWorkflow { Id = Guid.NewGuid(), ApplicationType = ApprovalApplicationType.Leave, Name = "Leave", IsActive = true };
            workflow.Steps.Add(new ApprovalWorkflowStep { Id = Guid.NewGuid(), StepNumber = 1, ApproverType = ApproverType.Person, ApproverEmployeeId = employee.Id });
            seed.ApprovalWorkflows.Add(workflow);
            var instance = new ApprovalInstance
            {
                Id = Guid.NewGuid(), ApplicationType = ApprovalApplicationType.Leave, ApplicationId = applicationId,
                ApplicantEmployeeId = employee.Id, ApprovalWorkflowId = workflow.Id, CurrentStepNumber = 1,
            };
            instance.Actions.Add(new ApprovalAction { Id = Guid.NewGuid(), StepNumber = 1, ActorEmployeeId = employee.Id, Action = ApprovalActionType.Reassigned });
            seed.ApprovalInstances.Add(instance);
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var engine = new ApprovalEngineService(new UnitOfWorkService(context), Substitute.For<IPublishEndpoint>());

        var read = await engine.GetInstanceReadOnlyAsync(ApprovalApplicationType.Leave, applicationId);

        read.Should().NotBeNull();
        read!.Workflow!.Steps.Should().ContainSingle();
        read.Actions.Should().ContainSingle();
        context.ChangeTracker.Entries().Should().BeEmpty();
    }

    // The raw-logs filters compared Payroll Group against DepartmentId and Area against
    // EmployeeId, and ignored Client entirely.
    [Fact]
    public async Task GetRawLogs_FiltersPayrollGroupAreaAndClient_OnTheRightColumns()
    {
        using var db = new SqliteHrmsContext();
        Guid groupA = Guid.NewGuid(), groupB = Guid.NewGuid(), area = Guid.NewGuid(), client = Guid.NewGuid();
        await using (var seed = db.NewContext())
        {
            seed.PayrollGroups.AddRange(new PayrollGroup { Id = groupA }, new PayrollGroup { Id = groupB });
            seed.Areas.Add(new CostCenters { Id = area, Name = "Site" });
            seed.Clients.Add(new Client { Id = client, Name = "Client" });
            var inGroupA = new Employee { Id = Guid.NewGuid(), PayrollGroupId = groupA, LastName = "A" };
            var inGroupB = new Employee { Id = Guid.NewGuid(), PayrollGroupId = groupB, LastName = "B" };
            seed.Employees.AddRange(inGroupA, inGroupB);
            var day = new DateTime(2026, 3, 2, 8, 0, 0);
            seed.Attendances.AddRange(
                new Attendance { Id = Guid.NewGuid(), EmployeeId = inGroupA.Id, WorkDateTime = day, OperationAreaId = area, ClientId = client },
                new Attendance { Id = Guid.NewGuid(), EmployeeId = inGroupB.Id, WorkDateTime = day });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var service = new AttendanceService(new UnitOfWorkService(context));
        AttendanceFilter Filter() => new() { FromDate = new DateOnly(2026, 3, 1), ToDate = new DateOnly(2026, 3, 31) };

        (await service.GetRawLogs(Filter() with { PayrollGroupId = groupA })).Should().ContainSingle().Which.Name.Should().StartWith("A,");
        (await service.GetRawLogs(Filter() with { OperationAreaId = area })).Should().ContainSingle().Which.Name.Should().StartWith("A,");
        (await service.GetRawLogs(Filter() with { ClientId = client })).Should().ContainSingle().Which.Name.Should().StartWith("A,");
        (await service.GetRawLogs(Filter())).Should().HaveCount(2);
    }
}
