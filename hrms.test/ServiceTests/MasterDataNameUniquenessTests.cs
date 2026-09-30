using Hrms.Core.Services;
using Hrms.Domain.Entities;
using Hrms.Infrastructure;
using hrms.test.TestSupport;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>
/// Master data (clients, departments, branches, positions, sections, project sites, payroll
/// groups, time shifts) is identified by its name: a second record with the same name -- trimmed
/// and ignoring case -- is refused, whatever its code (BaseService.NameIsTakenAsync).
/// </summary>
public class MasterDataNameUniquenessTests
{
    private static async Task<SqliteHrmsContext> SeedAsync(Action<HrmsContext> seedAction)
    {
        var db = new SqliteHrmsContext();
        await using var seed = db.NewContext();
        seedAction(seed);
        await seed.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Client_WithAnExistingName_IsRefused_IgnoringCaseAndSpacing()
    {
        using var db = await SeedAsync(s => s.Clients.Add(new Client { Id = Guid.NewGuid(), Code = "000001", Name = "Acme Corp" }));
        await using var context = db.NewContext();
        var sut = new ClientService(new UnitOfWorkService(context));

        var act = () => sut.AddAsync(new Client { Code = "000099", Name = "  acme CORP " }, CancellationToken.None);

        await act.Should().ThrowAsync<Exception>().WithMessage("*client named*already exists*");
    }

    [Fact]
    public async Task Department_IsCheckedByName_NotByCode()
    {
        using var db = await SeedAsync(s => s.Departments.Add(new Department { Id = Guid.NewGuid(), Code = "000001", Name = "Sales" }));

        await using (var context = db.NewContext())
        {
            var act = () => new DepartmentService(new UnitOfWorkService(context))
                .AddAsync(new Department { Code = "000002", Name = "SALES" }, CancellationToken.None);
            await act.Should().ThrowAsync<Exception>().WithMessage("*department named*already exists*");
        }

        await using (var context = db.NewContext())
        {
            var act = () => new DepartmentService(new UnitOfWorkService(context))
                .AddAsync(new Department { Code = "000001", Name = "Marketing" }, CancellationToken.None);
            await act.Should().NotThrowAsync("the code isn't the department's identity, its name is");
        }
    }

    [Fact]
    public async Task SavingARecord_UnderItsOwnName_IsAllowed()
    {
        var id = Guid.NewGuid();
        using var db = await SeedAsync(s => s.Positions.Add(new Position { Id = id, Code = "000001", Name = "Supervisor" }));
        await using var context = db.NewContext();
        var sut = new PositionService(new UnitOfWorkService(context));

        var act = () => sut.UpdateAsync(new UpdatePosition { Id = id, Code = "000001", Name = "supervisor ", Status = "Active" }, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ADeletedRecordsName_CanBeUsedAgain()
    {
        using var db = await SeedAsync(s => s.Areas.Add(new CostCenters { Id = Guid.NewGuid(), Code = "000001", Name = "Royal Gem", DeletedAt = DateTime.UtcNow }));
        await using var context = db.NewContext();

        var act = () => new CostCenterService(new UnitOfWorkService(context))
            .AddAsync(new CostCenters { Code = "000002", Name = "Royal Gem" }, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SectionNames_AreUniqueWithinTheirDepartmentOnly()
    {
        var sales = Guid.NewGuid();
        var ops = Guid.NewGuid();
        using var db = await SeedAsync(s =>
        {
            s.Departments.AddRange(
                new Department { Id = sales, Code = "000001", Name = "Sales" },
                new Department { Id = ops, Code = "000002", Name = "Operations" });
            s.Sections.Add(new Section { Id = Guid.NewGuid(), DepartmentId = sales, Code = "000001", Name = "Team A" });
        });

        await using (var context = db.NewContext())
        {
            var act = () => new SectionService(new UnitOfWorkService(context))
                .AddAsync(new Section { DepartmentId = ops, Code = "000002", Name = "Team A" }, CancellationToken.None);
            await act.Should().NotThrowAsync("another department may have its own Team A");
        }

        await using (var context = db.NewContext())
        {
            var act = () => new SectionService(new UnitOfWorkService(context))
                .AddAsync(new Section { DepartmentId = sales, Code = "000003", Name = "team a" }, CancellationToken.None);
            await act.Should().ThrowAsync<Exception>().WithMessage("*already has a section named*");
        }
    }

    [Fact]
    public async Task TimeShift_WithAnExistingName_IsRefused()
    {
        using var db = await SeedAsync(s => s.TimeShifts.Add(new TimeShift { Id = Guid.NewGuid(), ShiftName = "Day Shift" }));
        await using var context = db.NewContext();
        var sut = new TimeShiftService(new UnitOfWorkService(context), TypeAdapterConfig.GlobalSettings, Substitute.For<IMapper>());

        var act = () => sut.AddRangeAsync([new TimeShift { ShiftName = "DAY SHIFT" }], CancellationToken.None);

        await act.Should().ThrowAsync<Exception>().WithMessage("*time shift named*already exists*");
    }
}
