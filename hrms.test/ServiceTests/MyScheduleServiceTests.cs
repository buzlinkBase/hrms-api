using DTR.Core;
using DTR.Core.Services;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using hrms.test.TestSupport;
using Mapster;
using MapsterMapper;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>
/// My Shift Schedule runs the DTR's own schedule chains, so what an employee sees per date is
/// what the DTR computes: Work Rotation Plan &gt; Fixed Schedule &gt; Permanent Shift &gt; Open Shift,
/// with weekly rest days flagged. Mon 2026-10-05 .. Sun 2026-10-18 is the fortnight used below.
/// </summary>
public class MyScheduleServiceTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly DateOnly Monday = new(2026, 10, 5);

    private sealed record Seeded(Guid EmployeeId, Guid Day, Guid Mid, Guid Night);

    private static TimeShift Shift(string name, int startHour, int endHour) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = CompanyId,
        ShiftName = name,
        StartTime = TimeSpan.FromHours(startHour),
        EndTime = TimeSpan.FromHours(endHour),
    };

    private static async Task<Seeded> SeedAsync(SqliteHrmsContext db, bool withPermanentShift = true)
    {
        await using var seed = db.NewContext(CompanyId);
        var group = new PayrollGroup { Id = Guid.NewGuid(), TenantId = CompanyId };
        var day = Shift("Day", 8, 17);
        var mid = Shift("Mid", 12, 21);
        var night = Shift("Night", 19, 31); // ends 7:00 AM the next day
        var employee = new Employee
        {
            Id = Guid.NewGuid(), TenantId = CompanyId, PayrollGroupId = group.Id,
            FirstName = "Ana", LastName = "Reyes", EmployeeNo = "E-001",
            TimeShiftId = withPermanentShift ? day.Id : null,
        };
        seed.PayrollGroups.Add(group);
        seed.TimeShifts.AddRange(day, mid, night);
        seed.Employees.Add(employee);
        seed.RestDays.Add(new RestDay { Id = Guid.NewGuid(), TenantId = CompanyId, EmployeeId = employee.Id, DayName = DayName.Sunday });
        // Every Wednesday is the Mid shift, except Oct 14, which the rotation plan moves to Night.
        seed.EmployeeFixedSchedules.Add(new EmployeeFixedSchedule
        {
            Id = Guid.NewGuid(), TenantId = CompanyId, EmployeeId = employee.Id, DayName = DayName.Wednesday, TimeShiftId = mid.Id,
        });
        seed.WorkSchedulePlans.Add(new WorkSchedulePlan
        {
            Id = Guid.NewGuid(), TenantId = CompanyId, BatchCode = "ROT-1",
            EmployeeId = employee.Id, PayrollDate = new DateOnly(2026, 10, 14), TimeShiftId = night.Id,
        });
        await seed.SaveChangesAsync();
        return new Seeded(employee.Id, day.Id, mid.Id, night.Id);
    }

    private static MyScheduleService Sut(IUnitOfWorkService uow)
    {
        var config = TestMapping.GlobalSettings;
        var mapper = new Mapper(config);
        var employees = new EmployeeService(uow, Substitute.For<IMapper>(), config, null!, null!, null!, null!, null!, null!);
        var shifts = new WorkScheduleResolver(
            new WorkSchedulePlanService(uow, config, mapper),
            new TimeShiftService(uow, config, mapper),
            new EmployeeFixedScheduleService(uow));
        var restDays = new RestDayResolver(new ChangeRestDayService(uow), new RestDayDateService(uow, mapper));
        return new MyScheduleService(new RosterReportService(employees, shifts, restDays));
    }

    private static async Task<List<RosterReportModel>> ScheduleAsync(SqliteHrmsContext db, Guid employeeId)
    {
        await using var context = db.NewContext(CompanyId);
        return await Sut(new UnitOfWorkService(context))
            .GetAsync(employeeId, Monday, null, Monday, CancellationToken.None);
    }

    [Fact]
    public async Task EachDate_TakesItsShiftFromTheFirstTierThatHasOne()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db);

        var schedule = await ScheduleAsync(db, s.EmployeeId);
        RosterReportModel On(int day) => schedule.Single(x => x.WorkDate == new DateOnly(2026, 10, day));

        schedule.Should().HaveCount(MyScheduleService.DefaultDays);
        On(14).ScheduleSource.Should().Be(ScheduleSource.Override, "the rotation plan beats everything for its date");
        On(14).ShiftId.Should().Be(s.Night);
        On(7).ScheduleSource.Should().Be(ScheduleSource.FixedSchedule, "other Wednesdays use the fixed schedule");
        On(7).ShiftId.Should().Be(s.Mid);
        On(5).ScheduleSource.Should().Be(ScheduleSource.Permanent);
        On(5).ShiftId.Should().Be(s.Day);
    }

    [Fact]
    public async Task ShiftTimes_AreActualDateTimes_IncludingOvernight()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db);

        var night = (await ScheduleAsync(db, s.EmployeeId)).Single(x => x.WorkDate == new DateOnly(2026, 10, 14));

        night.ShiftStart.Should().Be(new DateTime(2026, 10, 14, 19, 0, 0));
        night.ShiftEnd.Should().Be(new DateTime(2026, 10, 15, 7, 0, 0));
    }

    [Fact]
    public async Task WeeklyRestDays_AreFlagged()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db);

        var schedule = await ScheduleAsync(db, s.EmployeeId);

        schedule.Where(x => x.IsRestDay).Select(x => x.WorkDate)
            .Should().Equal(new DateOnly(2026, 10, 11), new DateOnly(2026, 10, 18));
    }

    [Fact]
    public async Task WithoutAPermanentShift_OtherDaysAreOpenShift()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db, withPermanentShift: false);

        var monday = (await ScheduleAsync(db, s.EmployeeId)).Single(x => x.WorkDate == Monday);

        monday.ScheduleSource.Should().Be(ScheduleSource.OpenShift);
    }

    [Fact]
    public void Range_DefaultsToTwoWeeksFromToday()
    {
        MyScheduleService.Range(null, null, Monday).Should().Be((Monday, Monday.AddDays(13)));
    }

    [Fact]
    public void Range_IsNeverBackwards_AndNeverLongerThanTheCap()
    {
        MyScheduleService.Range(Monday.AddDays(5), Monday, Monday).Should().Be((Monday, Monday.AddDays(5)));
        MyScheduleService.Range(Monday, Monday.AddYears(1), Monday).To
            .Should().Be(Monday.AddDays(MyScheduleService.MaxDays - 1));
    }

    [Theory]
    [InlineData("2026-09-30T15:59:00", "2026-09-30")] // 11:59 PM in Manila
    [InlineData("2026-09-30T16:00:00", "2026-10-01")] // midnight in Manila, still Sept 30 in UTC
    public void Today_IsTheLocalDate_NotTheUtcOne(string utc, string expected)
    {
        MyScheduleService.LocalToday(DateTime.Parse(utc)).Should().Be(DateOnly.Parse(expected));
    }
}
