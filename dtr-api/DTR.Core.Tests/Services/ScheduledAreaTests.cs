using Hrms.Domain.Entities;

namespace DTR.Core.Tests.Services;

/// <summary>
/// A project site tagged on a Work Rotation Plan day: OverrideSchedule carries it (with the
/// site's branch/client) on the resolved CurrentShift, and DTRLocationResolver records the
/// day's DTR under it ahead of the punch's site and the employee's own.
/// </summary>
public class ScheduledAreaTests
{
    private static readonly DateOnly Day = new(2026, 1, 5);
    private static readonly Guid ShiftId = Guid.NewGuid();

    private static readonly List<TimeShiftModel> Shifts =
    [
        new TimeShiftModel { Id = ShiftId, ShiftName = "Day", StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(17) },
    ];

    private static CurrentShift Resolve(EmployeeDTRRun employee, WorkSchedulePlan plan)
    {
        var handler = new OverrideSchedule(new Dictionary<CurrentTimeShiftKey, WorkSchedulePlan?>
        {
            [new CurrentTimeShiftKey(employee.Id, Day)] = plan,
        }, Shifts);
        return handler.Handle(employee, Day);
    }

    // --- OverrideSchedule --------------------------------------------------------------------

    [Fact]
    public void OverrideSchedule_TaggedDay_CarriesTheSiteAndItsBranchAndClient()
    {
        var employee = new EmployeeDTRRun { Id = Guid.NewGuid() };
        var site = new CostCenters { Id = Guid.NewGuid(), Name = "Port Area", BranchId = Guid.NewGuid(), ClientId = Guid.NewGuid() };
        var plan = new WorkSchedulePlan { Id = Guid.NewGuid(), EmployeeId = employee.Id, PayrollDate = Day, TimeShiftId = ShiftId, OperationAreaId = site.Id, OperationArea = site };

        var shift = Resolve(employee, plan);

        shift.Source.Should().Be(ScheduleSource.Override);
        shift.AreaId.Should().Be(site.Id);
        shift.AreaName.Should().Be("Port Area");
        shift.AreaBranchId.Should().Be(site.BranchId);
        shift.AreaClientId.Should().Be(site.ClientId);
    }

    [Fact]
    public void OverrideSchedule_UntaggedDay_HasNoSite()
    {
        var employee = new EmployeeDTRRun { Id = Guid.NewGuid() };
        var plan = new WorkSchedulePlan { Id = Guid.NewGuid(), EmployeeId = employee.Id, PayrollDate = Day, TimeShiftId = ShiftId };

        var shift = Resolve(employee, plan);

        shift.AreaId.Should().BeNull();
        shift.AreaBranchId.Should().BeNull();
        shift.AreaClientId.Should().BeNull();
    }

    [Fact]
    public void OverrideSchedule_CarriesTheClientPickedOnTheDay_WithOrWithoutASite()
    {
        var employee = new EmployeeDTRRun { Id = Guid.NewGuid() };
        var client = new Client { Id = Guid.NewGuid(), Name = "Client B" };
        var plan = new WorkSchedulePlan { Id = Guid.NewGuid(), EmployeeId = employee.Id, PayrollDate = Day, TimeShiftId = ShiftId, ClientId = client.Id, Client = client };

        var shift = Resolve(employee, plan);

        shift.ClientId.Should().Be(client.Id);
        shift.ClientName.Should().Be("Client B");
        shift.AreaId.Should().BeNull("no site was picked -- the client stands on its own");
    }

    // --- DTRLocationResolver -----------------------------------------------------------------

    [Fact]
    public void DTRLocation_ClientPickedOnTheDay_BeatsTheSitesOwnClient()
    {
        var shift = new CurrentShift
        {
            AreaId = Guid.NewGuid(), AreaBranchId = Guid.NewGuid(), AreaClientId = Guid.NewGuid(),
            ClientId = Guid.NewGuid(),
        };

        var location = DTRLocationResolver.Resolve(shift, DevicePunch, Employee);

        location.Should().Be(new DTRLocation(shift.AreaBranchId, shift.ClientId, shift.AreaId));
    }

    [Fact]
    public void DTRLocation_ClientPickedOnASiteWithNoClient_UsesThePickedClient()
    {
        // The project site isn't tied to a client, so without the pick the day would fall back to the
        // punch's or the employee's client.
        var shift = new CurrentShift { AreaId = Guid.NewGuid(), ClientId = Guid.NewGuid() };

        var location = DTRLocationResolver.Resolve(shift, DevicePunch, Employee);

        location.Should().Be(new DTRLocation(DevicePunch.BranchId, shift.ClientId, shift.AreaId));
    }

    [Fact]
    public void DTRLocation_ClientWithoutASite_KeepsThePunchsBranchAndSite()
    {
        var shift = new CurrentShift { ClientId = Guid.NewGuid() };

        DTRLocationResolver.Resolve(shift, DevicePunch, Employee)
            .Should().Be(new DTRLocation(DevicePunch.BranchId, shift.ClientId, DevicePunch.OperationAreaId));
        DTRLocationResolver.Resolve(shift, null, Employee)
            .Should().Be(new DTRLocation(Employee.BranchId, shift.ClientId, Employee.AreaId));
    }

    private static readonly EmployeeDTRRun Employee = new()
    {
        Id = Guid.NewGuid(), AreaId = Guid.NewGuid(), BranchId = Guid.NewGuid(), ClientId = Guid.NewGuid(),
    };

    private static readonly Attendance DevicePunch = new()
    {
        OperationAreaId = Guid.NewGuid(), BranchId = Guid.NewGuid(), ClientId = Guid.NewGuid(),
    };

    [Fact]
    public void DTRLocation_ScheduledSite_WinsOverThePunchAndTheEmployee()
    {
        var shift = new CurrentShift { AreaId = Guid.NewGuid(), AreaBranchId = Guid.NewGuid(), AreaClientId = Guid.NewGuid() };

        var location = DTRLocationResolver.Resolve(shift, DevicePunch, Employee);

        location.Should().Be(new DTRLocation(shift.AreaBranchId, shift.AreaClientId, shift.AreaId));
    }

    [Fact]
    public void DTRLocation_ScheduledSiteWithoutBranchOrClient_KeepsThePunchsBranchAndClient()
    {
        var shift = new CurrentShift { AreaId = Guid.NewGuid() };

        var location = DTRLocationResolver.Resolve(shift, DevicePunch, Employee);

        location.Should().Be(new DTRLocation(DevicePunch.BranchId, DevicePunch.ClientId, shift.AreaId));
    }

    [Fact]
    public void DTRLocation_UntaggedDay_UsesThePunchThenTheEmployee()
    {
        var shift = new CurrentShift();

        DTRLocationResolver.Resolve(shift, DevicePunch, Employee)
            .Should().Be(new DTRLocation(DevicePunch.BranchId, DevicePunch.ClientId, DevicePunch.OperationAreaId));
        DTRLocationResolver.Resolve(shift, null, Employee)
            .Should().Be(new DTRLocation(Employee.BranchId, Employee.ClientId, Employee.AreaId));
    }
}
