using BuzlinkRepository;
using Hrms.Core.Services;
using Hrms.Core.Services.Storage;
using Hrms.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using hrms.test.TestSupport;

namespace hrms.test.ServiceTests;

/// <summary>
/// Birthday greetings: coworkers only hear about opted-in, current employees; HR sees everyone.
/// </summary>
public class BirthdayServiceTests
{
    private static readonly DateOnly Today = new(2026, 10, 17);

    [Theory]
    [InlineData("1990-10-17", 0)]
    [InlineData("1990-10-18", 1)]
    [InlineData("1990-10-16", 364)] // just passed: next one is a year away
    [InlineData("1990-01-01", 76)] // wraps into next year
    [InlineData("1992-02-29", 134)] // leap-day birthday -> Feb 28 in 2027
    public void DaysUntilNextBirthday_CountsToTheNextOccurrence(string dob, int expected) =>
        BirthdayService.DaysUntilNextBirthday(DateOnly.Parse(dob), Today).Should().Be(expected);

    [Fact]
    public void DaysUntilNextBirthday_LeapDayIsObservedOnTheRealDayInLeapYears() =>
        BirthdayService.DaysUntilNextBirthday(new DateOnly(1992, 2, 29), new DateOnly(2028, 2, 29)).Should().Be(0);

    private static BirthdayService Service(HrmsContext context) =>
        new(new UnitOfWorkService(context), new ImageUrlResolver(new LocalFileStorage(Path.GetTempPath())));

    private static Employee Emp(Guid group, string first, DateTime? dob, bool share, DateTime? resigned = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            PayrollGroupId = group,
            FirstName = first,
            LastName = "Test",
            DOB = dob,
            ShareBirthday = share,
            DateResigned = resigned,
        };

    [Fact]
    public async Task GetUpcomingForCoworkers_ListsOnlyOptedInCurrentEmployeesWithinTheWeek_NearestFirst()
    {
        using var db = new SqliteHrmsContext();
        await using (var seed = db.NewContext())
        {
            var group = new PayrollGroup { Id = Guid.NewGuid() };
            seed.PayrollGroups.Add(group);
            seed.Employees.AddRange(
                Emp(group.Id, "Today", new DateTime(1990, 10, 17), share: true),
                Emp(group.Id, "Private", new DateTime(1991, 10, 17), share: false),
                Emp(group.Id, "Resigned", new DateTime(1992, 10, 17), share: true, resigned: new DateTime(2026, 1, 1)),
                Emp(group.Id, "Edge", new DateTime(1993, 10, 24), share: true), // day 7
                Emp(group.Id, "Soon", new DateTime(1994, 10, 19), share: true), // day 2
                Emp(group.Id, "TooFar", new DateTime(1995, 10, 25), share: true), // day 8
                Emp(group.Id, "Passed", new DateTime(1996, 10, 16), share: true), // a year away
                Emp(group.Id, "NoDob", null, share: true));
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var week = await Service(context).GetUpcomingForCoworkersAsync(Today, BirthdayService.CoworkerLookAheadDays, default);

        week.Select(c => (c.FullName, c.DaysUntil)).Should().Equal(
            ("Today Test", 0), ("Soon Test", 2), ("Edge Test", 7));
    }

    [Fact]
    public async Task GetUpcomingForCoworkers_LooksAcrossTheYearEnd()
    {
        using var db = new SqliteHrmsContext();
        await using (var seed = db.NewContext())
        {
            var group = new PayrollGroup { Id = Guid.NewGuid() };
            seed.PayrollGroups.Add(group);
            seed.Employees.Add(Emp(group.Id, "NewYear", new DateTime(1990, 1, 2), share: true));
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var week = await Service(context).GetUpcomingForCoworkersAsync(new DateOnly(2026, 12, 29), 7, default);

        week.Select(c => (c.FullName, c.DaysUntil)).Should().Equal(("NewYear Test", 4));
    }

    [Fact]
    public async Task GetUpcoming_IncludesOptedOutEmployees_FlagsThem_AndOrdersByNearest()
    {
        using var db = new SqliteHrmsContext();
        await using (var seed = db.NewContext())
        {
            var group = new PayrollGroup { Id = Guid.NewGuid() };
            seed.PayrollGroups.Add(group);
            seed.Employees.AddRange(
                Emp(group.Id, "Later", new DateTime(1990, 11, 10), share: true),
                Emp(group.Id, "Soon", new DateTime(1991, 10, 19), share: false),
                Emp(group.Id, "TooFar", new DateTime(1992, 3, 1), share: true),
                Emp(group.Id, "Resigned", new DateTime(1993, 10, 18), share: true, resigned: new DateTime(2026, 1, 1)));
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var upcoming = await Service(context).GetUpcomingAsync(Today, 30, 10, default);

        upcoming.Select(u => (u.FullName, u.ShareBirthday)).Should().Equal(
            ("Soon Test", false), ("Later Test", true));
        upcoming[0].DaysUntil.Should().Be(2);
    }

    [Fact]
    public async Task SetShare_ChangesOnlyThatEmployee()
    {
        using var db = new SqliteHrmsContext();
        Guid mine, other;
        await using (var seed = db.NewContext())
        {
            var group = new PayrollGroup { Id = Guid.NewGuid() };
            seed.PayrollGroups.Add(group);
            var a = Emp(group.Id, "Mine", new DateTime(1990, 10, 17), share: false);
            var b = Emp(group.Id, "Other", new DateTime(1990, 10, 17), share: false);
            mine = a.Id;
            other = b.Id;
            seed.Employees.AddRange(a, b);
            await seed.SaveChangesAsync();
        }

        await using (var context = db.NewContext())
            (await Service(context).SetShareAsync(mine, true, default)).Should().BeTrue();

        await using var verify = db.NewContext();
        verify.Employees.Single(e => e.Id == mine).ShareBirthday.Should().BeTrue();
        verify.Employees.Single(e => e.Id == other).ShareBirthday.Should().BeFalse();
        (await Service(verify).SetShareAsync(Guid.NewGuid(), true, default)).Should().BeFalse();
    }
}
