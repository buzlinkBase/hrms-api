using DTR.Core;
using DTR.Core.Services;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Domain.ValueObjects;
using hrms.test.TestSupport;
using Mapster;
using MapsterMapper;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>
/// The Rostering report's client filter follows the client each day is actually rostered under:
/// a Work Rotation day tagged to a client belongs to that client, any other day to the client on
/// the employee's own record -- so a reliever shows under every client they worked for.
/// </summary>
public class RosterReportClientFilterTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly DateOnly Monday = new(2026, 10, 5);

    private sealed record Seeded(Guid Home, Guid Other, Guid Ana, Guid Ben);

    private static async Task<Seeded> SeedAsync(SqliteHrmsContext db)
    {
        await using var seed = db.NewContext(CompanyId);
        var home = new Client { Id = Guid.NewGuid(), TenantId = CompanyId, Name = "Home" };
        var other = new Client { Id = Guid.NewGuid(), TenantId = CompanyId, Name = "Other" };
        var group = new PayrollGroup { Id = Guid.NewGuid(), TenantId = CompanyId };
        var shift = new TimeShift
        {
            Id = Guid.NewGuid(), TenantId = CompanyId, ShiftName = "Day",
            StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(17),
        };
        Employee Emp(string first, string no, Guid clientId) => new()
        {
            Id = Guid.NewGuid(), TenantId = CompanyId, PayrollGroupId = group.Id,
            FirstName = first, LastName = "Reyes", EmployeeNo = no, TimeShiftId = shift.Id, ClientId = clientId,
        };
        var ana = Emp("Ana", "E-001", home.Id);
        var ben = Emp("Ben", "E-002", other.Id);
        seed.Clients.AddRange(home, other);
        seed.PayrollGroups.Add(group);
        seed.TimeShifts.Add(shift);
        seed.Employees.AddRange(ana, ben);
        // Ana is a reliever for Other on Oct 7 only.
        seed.WorkSchedulePlans.Add(new WorkSchedulePlan
        {
            Id = Guid.NewGuid(), TenantId = CompanyId, BatchCode = "ROT-1", EmployeeId = ana.Id,
            PayrollDate = new DateOnly(2026, 10, 7), TimeShiftId = shift.Id, ClientId = other.Id,
        });
        await seed.SaveChangesAsync();
        return new Seeded(home.Id, other.Id, ana.Id, ben.Id);
    }

    private static async Task<List<RosterReportModel>> QueryAsync(SqliteHrmsContext db, Guid? clientId)
    {
        await using var context = db.NewContext(CompanyId);
        var uow = new UnitOfWorkService(context);
        var config = TestMapping.GlobalSettings;
        var mapper = new Mapper(config);
        var sut = new RosterReportService(
            new EmployeeService(uow, Substitute.For<IMapper>(), config, null!, null!, null!, null!, null!, null!),
            new WorkScheduleResolver(
                new WorkSchedulePlanService(uow, config, mapper),
                new TimeShiftService(uow, config, mapper),
                new EmployeeFixedScheduleService(uow)),
            new RestDayResolver(new ChangeRestDayService(uow), new RestDayDateService(uow, mapper)));
        return await sut.RosterReportQuery(
            new DTRRequestPayload { FromDate = Monday, ToDate = Monday.AddDays(4), ClientId = clientId },
            CancellationToken.None);
    }

    [Fact]
    public async Task ClientFilter_KeepsEachDayUnderTheClientItIsRosteredFor()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db);

        var home = await QueryAsync(db, s.Home);
        var other = await QueryAsync(db, s.Other);

        home.Where(x => x.EmployeeId == s.Ana).Select(x => x.WorkDate.Day).Should().Equal(5, 6, 8, 9);
        home.Should().NotContain(x => x.EmployeeId == s.Ben);
        other.Where(x => x.EmployeeId == s.Ana).Select(x => x.WorkDate.Day).Should().Equal(7);
        other.Where(x => x.EmployeeId == s.Ben).Should().HaveCount(5);
    }

    [Fact]
    public async Task WithoutAClientFilter_EveryoneIsListed()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db);

        var all = await QueryAsync(db, null);

        all.Should().HaveCount(10);
    }
}
