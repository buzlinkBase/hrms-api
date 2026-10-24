using BuzlinkRepository;
using DTR.Core;
using Hrms.Core.Services;
using Hrms.Core.Services.Storage;
using Hrms.Domain.ValueObjects;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Infrastructure;
using Microsoft.EntityFrameworkCore;
using hrms.test.TestSupport;

namespace hrms.test.ServiceTests;

/// <summary>
/// Portal Coworkers extras: every widget is off until the company enables it, lists show current
/// employees only, away status never reveals why, and contact details need the employee's opt-in.
/// </summary>
public class CoworkerServiceTests
{
    private static readonly DateOnly Today = new(2026, 10, 17);

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(5, true)]
    [InlineData(7, false)]
    [InlineData(10, true)]
    [InlineData(40, true)]
    [InlineData(0, false)]
    public void IsMilestone_OnlyForServiceAnniversariesWorthCelebrating(int years, bool expected) =>
        CoworkerService.IsMilestone(years).Should().Be(expected);

    [Fact]
    public void BuildOrgTree_NestsReportsAndSurvivesCyclesAndMissingManagers()
    {
        OrgNodeModel Node(Guid id) => new() { Id = id, FullName = id.ToString() };
        var boss = Guid.NewGuid();
        var mid = Guid.NewGuid();
        var leaf = Guid.NewGuid();
        var orphan = Guid.NewGuid();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var roots = CoworkerService.BuildOrgTree(
        [
            (boss, null, Node(boss)),
            (mid, boss, Node(mid)),
            (leaf, mid, Node(leaf)),
            (orphan, Guid.NewGuid(), Node(orphan)), // manager not in the list
            (a, b, Node(a)), // a <-> b cycle
            (b, a, Node(b)),
        ]);

        roots.Select(r => r.Id).Should().BeEquivalentTo([boss, orphan, a, b]);
        roots.Single(r => r.Id == boss).Reports.Single().Reports.Single().Id.Should().Be(leaf);
    }

    private static CoworkerService Service(HrmsContext context) =>
        new(new UnitOfWorkService(context),
            new ImageUrlResolver(new LocalFileStorage(Path.GetTempPath())),
            new GeneralSettingService(new UnitOfWorkService(context)));

    private static async Task Enable(HrmsContext seed, params SettingKey[] keys)
    {
        foreach (var key in keys)
            seed.Add(new GeneralSetting { IdentityType = "Company", Description = key.ToString(), Value = "true" });
        await seed.SaveChangesAsync();
    }

    private static Employee Emp(Guid group, string first, DateOnly hired, DateTime? resigned = null) => new()
    {
        Id = Guid.NewGuid(),
        PayrollGroupId = group,
        FirstName = first,
        LastName = "Test",
        HireDate = hired,
        DateResigned = resigned,
    };

    [Fact]
    public async Task EveryWidgetIsEmptyUntilTheCompanySwitchesItOn()
    {
        using var db = new SqliteHrmsContext();
        await using (var seed = db.NewContext())
        {
            var group = new PayrollGroup { Id = Guid.NewGuid() };
            seed.PayrollGroups.Add(group);
            seed.Employees.Add(Emp(group.Id, "Fresh", Today.AddDays(-3)));
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var service = Service(context);

        (await service.GetNewHiresAsync(Today, default)).Should().BeEmpty();
        (await service.GetAnniversariesAsync(Today, default)).Should().BeEmpty();
        (await service.GetOutTodayAsync(Today, default)).Should().BeEmpty();
        (await service.SearchDirectoryAsync(null, null, 1, default)).Items.Should().BeEmpty();
        (await service.GetOrgChartAsync(default)).Should().BeEmpty();
    }

    [Fact]
    public async Task NewHires_AreTheLast7DaysOfCurrentEmployees()
    {
        using var db = new SqliteHrmsContext();
        await using (var seed = db.NewContext())
        {
            var group = new PayrollGroup { Id = Guid.NewGuid() };
            seed.PayrollGroups.Add(group);
            seed.Employees.AddRange(
                Emp(group.Id, "Recent", Today.AddDays(-3)),
                Emp(group.Id, "Edge", Today.AddDays(-7)),
                Emp(group.Id, "JustPast", Today.AddDays(-8)),
                Emp(group.Id, "Old", Today.AddDays(-90)),
                Emp(group.Id, "Left", Today.AddDays(-5), resigned: new DateTime(2026, 10, 10)));
            await Enable(seed, SettingKey.CoworkerShowNewHires);
        }

        await using var context = db.NewContext();
        var hires = await Service(context).GetNewHiresAsync(Today, default);

        hires.Select(h => h.FullName).Should().Equal("Recent Test", "Edge Test");
    }

    [Fact]
    public async Task Anniversaries_ListOnlyMilestoneYearsComingUpThisWeek()
    {
        using var db = new SqliteHrmsContext();
        await using (var seed = db.NewContext())
        {
            var group = new PayrollGroup { Id = Guid.NewGuid() };
            seed.PayrollGroups.Add(group);
            seed.Employees.AddRange(
                Emp(group.Id, "FiveToday", new DateOnly(2021, 10, 17)),
                Emp(group.Id, "ThreeSoon", new DateOnly(2023, 10, 20)),
                Emp(group.Id, "TwoYears", new DateOnly(2024, 10, 17)), // not a milestone
                Emp(group.Id, "TooFar", new DateOnly(2016, 11, 30)),
                Emp(group.Id, "Left", new DateOnly(2016, 10, 17), resigned: new DateTime(2026, 1, 1)));
            await Enable(seed, SettingKey.CoworkerShowAnniversaries);
        }

        await using var context = db.NewContext();
        var list = await Service(context).GetAnniversariesAsync(Today, default);

        list.Select(a => (a.FullName, a.Years, a.DaysUntil)).Should().Equal(
            ("FiveToday Test", 5, 0), ("ThreeSoon Test", 3, 3));
    }

    [Fact]
    public async Task OutToday_CountsApprovedLeaveAndTravelOnly_AndNeverSaysWhy()
    {
        using var db = new SqliteHrmsContext();
        await using (var seed = db.NewContext())
        {
            var group = new PayrollGroup { Id = Guid.NewGuid() };
            seed.PayrollGroups.Add(group);
            var leaveType = new Leave { Id = Guid.NewGuid(), Code = "SL", Description = "Sick" };
            seed.Leaves.Add(leaveType);
            var onLeave = Emp(group.Id, "OnLeave", Today.AddYears(-3));
            var pending = Emp(group.Id, "Pending", Today.AddYears(-3));
            var travelling = Emp(group.Id, "Travelling", Today.AddYears(-3));
            var other = Emp(group.Id, "OtherDay", Today.AddYears(-3));
            seed.Employees.AddRange(onLeave, pending, travelling, other);
            seed.leaveApplications.AddRange(
                new LeaveApplication { Id = Guid.NewGuid(), Leave = leaveType, LeaveId = leaveType.Id, EmployeeId = onLeave.Id, LeaveDateFrom = Today.AddDays(-1), LeaveDateTo = Today.AddDays(1), ApprovalStatus = ApprovalStatus.Approved },
                new LeaveApplication { Id = Guid.NewGuid(), Leave = leaveType, LeaveId = leaveType.Id, EmployeeId = pending.Id, LeaveDateFrom = Today, LeaveDateTo = Today, ApprovalStatus = ApprovalStatus.ForApproval },
                new LeaveApplication { Id = Guid.NewGuid(), Leave = leaveType, LeaveId = leaveType.Id, EmployeeId = other.Id, LeaveDateFrom = Today.AddDays(5), LeaveDateTo = Today.AddDays(6), ApprovalStatus = ApprovalStatus.Approved });
            seed.TravelOrderApplications.Add(new TravelOrderApplication { Id = Guid.NewGuid(), EmployeeId = travelling.Id, StartDate = Today, EndDate = Today, Destination = "Secret site", ApprovalStatus = ApprovalStatus.Approved });
            await Enable(seed, SettingKey.CoworkerShowWhosOut);
        }

        await using var context = db.NewContext();
        var away = await Service(context).GetOutTodayAsync(Today, default);

        away.Select(a => (a.FullName, a.Note)).Should().BeEquivalentTo(
            [("OnLeave Test", "Out"), ("Travelling Test", "Away")]);
    }

    [Fact]
    public async Task Directory_HidesEmailAndContactUnlessTheEmployeeOptedIn()
    {
        using var db = new SqliteHrmsContext();
        await using (var seed = db.NewContext())
        {
            var group = new PayrollGroup { Id = Guid.NewGuid() };
            seed.PayrollGroups.Add(group);
            var open = Emp(group.Id, "Open", Today.AddYears(-1));
            open.Email = "open@x.com"; open.Contact = "0917"; open.ShareContact = true;
            var closed = Emp(group.Id, "Closed", Today.AddYears(-1));
            closed.Email = "closed@x.com"; closed.Contact = "0918";
            seed.Employees.AddRange(open, closed,
                Emp(group.Id, "Left", Today.AddYears(-1), resigned: new DateTime(2026, 1, 1)));
            await Enable(seed, SettingKey.CoworkerShowDirectory);
        }

        await using var context = db.NewContext();
        var page = await Service(context).SearchDirectoryAsync(null, null, 1, default);

        page.Total.Should().Be(2);
        page.Items.Single(i => i.FullName == "Open Test").Email.Should().Be("open@x.com");
        var closedEntry = page.Items.Single(i => i.FullName == "Closed Test");
        closedEntry.Email.Should().BeNull();
        closedEntry.Contact.Should().BeNull();

        (await Service(context).SearchDirectoryAsync("Clos", null, 1, default)).Items
            .Select(i => i.FullName).Should().Equal("Closed Test");
    }

    [Fact]
    public async Task SetShareContact_ChangesOnlyThatEmployee()
    {
        using var db = new SqliteHrmsContext();
        Guid mine, other;
        await using (var seed = db.NewContext())
        {
            var group = new PayrollGroup { Id = Guid.NewGuid() };
            seed.PayrollGroups.Add(group);
            var a = Emp(group.Id, "Mine", Today.AddYears(-1));
            var b = Emp(group.Id, "Other", Today.AddYears(-1));
            mine = a.Id; other = b.Id;
            seed.Employees.AddRange(a, b);
            await seed.SaveChangesAsync();
        }

        await using (var context = db.NewContext())
            (await Service(context).SetShareContactAsync(mine, true, default)).Should().BeTrue();

        await using var verify = db.NewContext();
        verify.Employees.Single(e => e.Id == mine).ShareContact.Should().BeTrue();
        verify.Employees.Single(e => e.Id == other).ShareContact.Should().BeFalse();
    }
}
