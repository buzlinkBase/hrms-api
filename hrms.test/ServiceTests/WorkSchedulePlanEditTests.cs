using Hrms.Core.Services;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Domain.ValueObjects;
using Hrms.Infrastructure;
using hrms.test.TestSupport;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>
/// Editing one Work Rotation entry: its shift, payroll date and project site change in place, the
/// employee never does, and an employee still holds one entry per date.
/// </summary>
public class WorkSchedulePlanEditTests
{
    private static WorkSchedulePlanService BuildService(IUnitOfWorkService uow) =>
        new(uow, new TypeAdapterConfig(), Substitute.For<IMapper>());

    private sealed record Seeded(Guid EmployeeId, Guid PlanId, Guid OtherPlanId, Guid DayShiftId, Guid NightShiftId, Guid AreaId);

    // One employee with a permanent Day shift, and two rotation entries (Oct 16 and Oct 17) on a
    // Night shift -- so a list that showed the employee's own shift would read "Day" and be wrong.
    private static Seeded Seed(HrmsContext seed)
    {
        var group = new PayrollGroup { Id = Guid.NewGuid() };
        seed.PayrollGroups.Add(group);
        var day = new TimeShift { Id = Guid.NewGuid(), ShiftName = "Dayshift" };
        var night = new TimeShift { Id = Guid.NewGuid(), ShiftName = "Nightshift" };
        seed.TimeShifts.AddRange(day, night);
        var employee = new Employee
        {
            Id = Guid.NewGuid(), PayrollGroupId = group.Id, TimeShiftId = day.Id,
            FirstName = "Elizabeth", LastName = "Arnofo",
        };
        seed.Employees.Add(employee);
        var area = new CostCenters { Id = Guid.NewGuid(), Name = "Site A", Code = "A" };
        seed.Add(area);

        WorkSchedulePlan Plan(int dayOfMonth) => new()
        {
            Id = Guid.NewGuid(), EmployeeId = employee.Id, TimeShiftId = night.Id,
            PayrollDate = new DateOnly(2026, 10, dayOfMonth), BatchCode = "WRP1",
        };
        var first = Plan(16);
        var second = Plan(17);
        seed.WorkSchedulePlans.AddRange(first, second);
        return new Seeded(employee.Id, first.Id, second.Id, day.Id, night.Id, area.Id);
    }

    [Fact]
    public async Task GetModelAsync_ShowsTheEntrysOwnShift_NotTheEmployeesPermanentOne()
    {
        using var db = new SqliteHrmsContext();
        Seeded s;
        await using (var seed = db.NewContext()) { s = Seed(seed); await seed.SaveChangesAsync(); }

        await using var context = db.NewContext();
        var model = await BuildService(new UnitOfWorkService(context)).GetModelAsync(s.PlanId, CancellationToken.None);

        model.Should().NotBeNull();
        model!.ShiftName.Should().Be("Nightshift");
        model.FullName.Should().Contain("Arnofo");
    }

    [Fact]
    public async Task FindAllAsync_ListsEachEmployeesDaysInDateOrder_GroupedByEmployeeName()
    {
        using var db = new SqliteHrmsContext();
        await using (var seed = db.NewContext())
        {
            var s = Seed(seed); // "Arnofo, Elizabeth": Oct 16 and Oct 17
            var group = seed.PayrollGroups.Local.First();
            var earlier = new Employee { Id = Guid.NewGuid(), PayrollGroupId = group.Id, FirstName = "Ace", LastName = "Baquirosa" };
            var later = new Employee { Id = Guid.NewGuid(), PayrollGroupId = group.Id, FirstName = "Zed", LastName = "Zamora" };
            seed.Employees.AddRange(earlier, later);
            // Inserted out of order on purpose: later date first, and the last-named employee first.
            seed.WorkSchedulePlans.AddRange(
                new WorkSchedulePlan { Id = Guid.NewGuid(), EmployeeId = later.Id, TimeShiftId = s.NightShiftId, PayrollDate = new DateOnly(2026, 10, 1), BatchCode = "B" },
                new WorkSchedulePlan { Id = Guid.NewGuid(), EmployeeId = earlier.Id, TimeShiftId = s.NightShiftId, PayrollDate = new DateOnly(2026, 10, 9), BatchCode = "B" },
                new WorkSchedulePlan { Id = Guid.NewGuid(), EmployeeId = earlier.Id, TimeShiftId = s.NightShiftId, PayrollDate = new DateOnly(2026, 10, 2), BatchCode = "B" });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var rows = await BuildService(new UnitOfWorkService(context)).FindAllAsync(
            new WorkRotationPlanFilter(DateOnly.MinValue, DateOnly.MaxValue, null), CancellationToken.None);

        rows.Select(r => (r.FullName!.Split(',')[0], r.PayrollDate.Day)).Should().Equal(
            ("Arnofo", 16), ("Arnofo", 17), ("Baquirosa", 2), ("Baquirosa", 9), ("Zamora", 1));
    }

    [Fact]
    public async Task UpdateAsync_ChangesShiftDateAndSite_AndKeepsTheEmployeeAndBatch()
    {
        using var db = new SqliteHrmsContext();
        Seeded s;
        await using (var seed = db.NewContext()) { s = Seed(seed); await seed.SaveChangesAsync(); }

        await using (var context = db.NewContext())
        {
            await BuildService(new UnitOfWorkService(context)).UpdateAsync(new UpdateWorkSchedulePlan
            {
                Id = s.PlanId,
                EmployeeId = Guid.NewGuid(), // ignored: the employee never changes
                TimeShiftId = s.DayShiftId,
                PayrollDate = new DateOnly(2026, 10, 25),
                OperationAreaId = s.AreaId,
            }, CancellationToken.None);
        }

        await using var check = db.NewContext();
        var saved = await check.WorkSchedulePlans.SingleAsync(x => x.Id == s.PlanId);
        saved.EmployeeId.Should().Be(s.EmployeeId);
        saved.TimeShiftId.Should().Be(s.DayShiftId);
        saved.PayrollDate.Should().Be(new DateOnly(2026, 10, 25));
        saved.OperationAreaId.Should().Be(s.AreaId);
        saved.BatchCode.Should().Be("WRP1");
    }

    [Fact]
    public async Task UpdateAsync_SavesTheClientPickedOnTheDay_AndTheModelNamesIt()
    {
        using var db = new SqliteHrmsContext();
        Seeded s;
        var clientId = Guid.NewGuid();
        await using (var seed = db.NewContext())
        {
            s = Seed(seed);
            seed.Add(new Client { Id = clientId, Code = "B", Name = "Client B" });
            await seed.SaveChangesAsync();
        }

        await using (var context = db.NewContext())
        {
            await BuildService(new UnitOfWorkService(context)).UpdateAsync(new UpdateWorkSchedulePlan
            {
                Id = s.PlanId, TimeShiftId = s.NightShiftId, PayrollDate = new DateOnly(2026, 10, 16), ClientId = clientId,
            }, CancellationToken.None);
        }

        await using var check = db.NewContext();
        var service = BuildService(new UnitOfWorkService(check));
        var model = await service.GetModelAsync(s.PlanId, CancellationToken.None);
        model!.ClientId.Should().Be(clientId);
        model.ClientName.Should().Be("Client B");
        (await service.ClientExistsAsync(clientId, CancellationToken.None)).Should().BeTrue();
        (await service.ClientExistsAsync(Guid.NewGuid(), CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task DeletingTheClient_LeavesTheEntry_WithNoClient()
    {
        using var db = new SqliteHrmsContext();
        Seeded s;
        var clientId = Guid.NewGuid();
        await using (var seed = db.NewContext())
        {
            s = Seed(seed);
            seed.Add(new Client { Id = clientId, Code = "B", Name = "Client B" });
            seed.WorkSchedulePlans.Local.Single(x => x.Id == s.PlanId).ClientId = clientId;
            await seed.SaveChangesAsync();
        }

        await using (var remove = db.NewContext())
        {
            await remove.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON");
            await remove.Clients.Where(x => x.Id == clientId).ExecuteDeleteAsync();
        }

        await using var check = db.NewContext();
        (await check.WorkSchedulePlans.SingleAsync(x => x.Id == s.PlanId)).ClientId.Should().BeNull("ON DELETE SET NULL");
    }

    [Fact]
    public async Task UpdateAsync_RefusesToMoveAnEntryOntoADayAnotherEntryHolds()
    {
        using var db = new SqliteHrmsContext();
        Seeded s;
        await using (var seed = db.NewContext()) { s = Seed(seed); await seed.SaveChangesAsync(); }

        await using var context = db.NewContext();
        var act = async () => await BuildService(new UnitOfWorkService(context)).UpdateAsync(new UpdateWorkSchedulePlan
        {
            Id = s.PlanId,
            TimeShiftId = s.NightShiftId,
            PayrollDate = new DateOnly(2026, 10, 17), // the other entry's day
        }, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*already has a work rotation*");
    }

    [Fact]
    public async Task UpdateAsync_AllowsKeepingTheSameDate_WhenOnlyTheShiftChanges()
    {
        using var db = new SqliteHrmsContext();
        Seeded s;
        await using (var seed = db.NewContext()) { s = Seed(seed); await seed.SaveChangesAsync(); }

        await using (var context = db.NewContext())
        {
            await BuildService(new UnitOfWorkService(context)).UpdateAsync(new UpdateWorkSchedulePlan
            {
                Id = s.PlanId, TimeShiftId = s.DayShiftId, PayrollDate = new DateOnly(2026, 10, 16),
            }, CancellationToken.None);
        }

        await using var check = db.NewContext();
        (await check.WorkSchedulePlans.SingleAsync(x => x.Id == s.PlanId)).TimeShiftId.Should().Be(s.DayShiftId);
    }

    [Fact]
    public async Task UpdateAsync_Throws_WhenTheEntryDoesNotExist()
    {
        using var db = new SqliteHrmsContext();
        await using var context = db.NewContext();

        var act = async () => await BuildService(new UnitOfWorkService(context)).UpdateAsync(
            new UpdateWorkSchedulePlan { Id = Guid.NewGuid() }, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
