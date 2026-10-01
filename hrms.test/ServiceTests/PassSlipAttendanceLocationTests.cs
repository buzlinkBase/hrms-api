using Hrms.Core.Services.Approvals;
using Hrms.Domain.Entities.EmployeeEntities;
using hrms.test.TestSupport;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>The attendance logs an approved pass slip writes.</summary>
public class PassSlipAttendanceLocationTests
{
    // Regression: the pass slip's logs carried no Bio ID, branch, client, project site or
    // department, so the DTR showed "—" for them and location-filtered reports skipped them.
    [Fact]
    public async Task ApprovedPassSlip_LogsCarryTheEmployeesBioIdAndLocation()
    {
        using var db = new SqliteHrmsContext();
        Guid branch = Guid.NewGuid(), client = Guid.NewGuid(), site = Guid.NewGuid(), dept = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var employee = new Employee
        {
            Id = Guid.NewGuid(), PayrollGroupId = groupId, LastName = "Cruz", BioId = 42,
            BranchId = branch, ClientId = client, AreaId = site, DepartmentId = dept,
        };
        var approver = new Employee { Id = Guid.NewGuid(), PayrollGroupId = groupId, LastName = "Boss" };
        var slip = new PassSlipApplication
        {
            Id = Guid.NewGuid(), EmployeeId = employee.Id, ApplicationDate = new DateOnly(2026, 9, 3),
            DepartureTime = new DateTime(2026, 9, 3, 10, 0, 0), ReturnTime = new DateTime(2026, 9, 3, 11, 0, 0),
            Purpose = "Bank", ApprovalStatus = ApprovalStatus.ForApproval,
        };
        await using (var seed = db.NewContext())
        {
            seed.PayrollGroups.Add(new PayrollGroup { Id = groupId });
            seed.Branches.Add(new Branch { Id = branch, Name = "Manila" });
            seed.Clients.Add(new Client { Id = client, Name = "Client" });
            seed.Areas.Add(new CostCenters { Id = site, Name = "Site", BranchId = branch });
            seed.Departments.Add(new Department { Id = dept, Name = "Ops" });
            seed.Employees.AddRange(employee, approver);
            seed.PassSlipApplications.Add(slip);
            await seed.SaveChangesAsync();
        }

        await using (var context = db.NewContext())
        {
            var uow = new UnitOfWorkService(context);
            await new PassSlipApplicationService(uow, new AttendanceService(uow), new ApprovalEngineService(uow, Substitute.For<IPublishEndpoint>()))
                .ApproveAsync(slip.Id, approver.Id, approverHasOverride: true, note: null, CancellationToken.None);
        }

        await using var verify = db.NewContext();
        var logs = await verify.Attendances.Where(x => x.EmployeeId == employee.Id).ToListAsync();
        logs.Should().HaveCount(2);
        logs.Should().AllSatisfy(log =>
        {
            log.BioId.Should().Be(42);
            log.BranchId.Should().Be(branch);
            log.ClientId.Should().Be(client);
            log.OperationAreaId.Should().Be(site);
            log.DepartmentId.Should().Be(dept);
        });
    }
}
