using Hrms.Core.Services;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Domain.ValueObjects;
using hrms.test.TestSupport;
using Mapster;
using MapsterMapper;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>
/// An application switched to a fixed schedule after an attendance-based run was saved must not keep
/// that run's skip verdict: the row is released so payroll can pay it. A posted run keeps its verdict.
/// </summary>
public class OtherIncomeModeSwitchTests : IDisposable
{
    // 2026-10-15 is a Thursday.
    private static readonly DateOnly Thursday = new(2026, 10, 15);

    private readonly SqliteHrmsContext _db = new();

    public void Dispose() => _db.Dispose();

    private sealed record Seeded(Guid EmployeeId, Guid IncomeId, Guid ApplicationId, Guid RowId);

    private async Task<Seeded> SeedSkippedRowAsync(bool postedRun)
    {
        await using var seed = _db.NewContext();
        var groupId = Guid.NewGuid();
        seed.PayrollGroups.Add(new PayrollGroup { Id = groupId });
        var employee = new Employee { Id = Guid.NewGuid(), PayrollGroupId = groupId };
        seed.Employees.Add(employee);
        var income = new OtherIncome { Id = Guid.NewGuid(), Code = "HG", Name = "HG Allowance" };
        seed.Allowances.Add(income);

        var payroll = new Payroll
        {
            Id = Guid.NewGuid(), EmployeeId = employee.Id, IsPosted = postedRun,
            PayPeriodStart = new DateOnly(2026, 10, 1), PayPeriodEnd = Thursday,
        };
        seed.Payrolls.Add(payroll);

        var application = new OtherIncomeApplication
        {
            Id = Guid.NewGuid(), EmployeeId = employee.Id, IncomeId = income.Id, Amount = 250m,
            StartDate = Thursday, EndDate = new DateOnly(2026, 12, 31),
            PayMode = OtherIncomePayMode.PerQualifyingDay, AttendanceBasis = OtherIncomeAttendanceBasis.Worked,
            ApplicableDays = (int)(OtherIncomeWeekdays.Monday | OtherIncomeWeekdays.Tuesday | OtherIncomeWeekdays.Wednesday),
        };
        var row = new OtherIncomeSchedules
        {
            Id = Guid.NewGuid(), EmployeeId = employee.Id, IncomeId = income.Id, Date = Thursday, Amount = 250m,
            IsSkipped = true, SkipReason = OtherIncomeSkipReasons.NotApplicableDay, ConsumedByPayrollId = payroll.Id,
        };
        application.Schedule.Add(row);
        seed.OtherIncomeApplications.Add(application);
        await seed.SaveChangesAsync();
        return new Seeded(employee.Id, income.Id, application.Id, row.Id);
    }

    private static IncomeAplMainService BuildService(IUnitOfWorkService uow)
    {
        _ = TestMapping.GlobalSettings; // registers MappingProfile once per run (see TestMapping)
        var employeeService = new EmployeeService(
            uow, Substitute.For<IMapper>(), TypeAdapterConfig.GlobalSettings,
            new DepartmentService(uow), new PayrollGroupService(uow), new BranchService(uow),
            new CostCenterService(uow), new PositionService(uow), new SectionService(uow));
        return new IncomeAplMainService(uow, new OtherIncomeService(uow), employeeService,
            new Mapper(TypeAdapterConfig.GlobalSettings));
    }

    private async Task SwitchToFixedAsync(Seeded s)
    {
        await using var context = _db.NewContext();
        var service = BuildService(new UnitOfWorkService(context));
        await service.UpdateAsync(new UpdateOtherIncomeApplication
        {
            Id = s.ApplicationId, EmployeeId = s.EmployeeId, IncomeId = s.IncomeId, Amount = 250m,
            StartDate = Thursday, EndDate = new DateOnly(2026, 12, 31),
            PayMode = OtherIncomePayMode.FixedSchedule,
            Schedule = [new UpdateOtherIncomeSchedule { Id = s.RowId, Date = Thursday, Amount = 250m }],
        }, CancellationToken.None);
    }

    [Fact]
    public async Task SwitchingToFixed_ReleasesARowSkippedByADraftRun()
    {
        var s = await SeedSkippedRowAsync(postedRun: false);

        await SwitchToFixedAsync(s);

        await using var check = _db.NewContext();
        var row = await check.OtherIncomeApplicationDetails.FindAsync(s.RowId);
        row!.IsSkipped.Should().BeFalse();
        row.SkipReason.Should().BeNull();
        row.ConsumedByPayrollId.Should().BeNull();
    }

    [Fact]
    public async Task SwitchingToFixed_KeepsTheVerdictOfAPostedRun()
    {
        var s = await SeedSkippedRowAsync(postedRun: true);

        await SwitchToFixedAsync(s);

        await using var check = _db.NewContext();
        var row = await check.OtherIncomeApplicationDetails.FindAsync(s.RowId);
        row!.IsSkipped.Should().BeTrue();
        row.ConsumedByPayrollId.Should().NotBeNull();
    }
}
