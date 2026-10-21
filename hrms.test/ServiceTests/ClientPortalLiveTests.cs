using Hrms.Core.Services.ClientPortal;
using Hrms.Domain.Entities.EmployeeEntities;
using hrms.test.TestSupport;
using MassTransit;
using Microsoft.AspNetCore.DataProtection;
using NSubstitute;
using Onepunch.Common.Lib.DTO;

namespace hrms.test.ServiceTests;

/// <summary>The live feed to the Client Portal: punches as they arrive and the manning planned for the coming days.</summary>
public class ClientPortalLiveTests
{
    private static readonly DateOnly Day = new(2026, 10, 1); // a Thursday
    private static readonly DateTime Now = new(2026, 10, 1, 8, 0, 0);

    private static readonly Client Waterfront = new() { Id = Guid.NewGuid(), Code = "WF", Name = "Waterfront" };
    private static readonly CostCenters Lahug = new() { Id = Guid.NewGuid(), Code = "LHG", Name = "Lahug" };
    private static readonly ClientSite Agwa = new() { Id = Guid.NewGuid(), ClientId = Waterfront.Id, AreaId = Lahug.Id, Code = "AGWA", Name = "AGWA Building" };
    private static readonly ClientPost Gate = new() { Id = Guid.NewGuid(), ClientSiteId = Agwa.Id, Code = "G1", Name = "Main Gate", Position = "Regular guard" };

    private static Attendance Punch(Guid employee, DateTime at, Guid? client = null, Guid? area = null) => new()
    {
        Id = Guid.NewGuid(), EmployeeId = employee, WorkDateTime = at, ClientId = client ?? Waterfront.Id, OperationAreaId = area ?? Lahug.Id,
        LogSource = LOGSOURCE.SYNC, DeviceName = "Gate reader",
    };

    private static LivePunchSource Source(IReadOnlyList<Attendance> punches, IReadOnlyList<Deployment>? deployments = null) => new(
        punches, new Dictionary<Guid, Client> { [Waterfront.Id] = Waterfront }, new Dictionary<Guid, CostCenters> { [Lahug.Id] = Lahug },
        new Dictionary<Guid, Employee>(), [Agwa], new Dictionary<Guid, ClientPost> { [Gate.Id] = Gate }, deployments ?? []);

    // ---- punches -------------------------------------------------------------------------------------------------

    [Fact]
    public void A_punch_is_sent_under_the_site_and_post_the_person_is_deployed_to()
    {
        var emp = Guid.NewGuid();
        var deployment = new Deployment { EmployeeId = emp, PostId = Gate.Id, Role = DeploymentRole.Regular, StartDate = Day.AddDays(-10) };

        var message = ClientPortalLiveBuilder.Punches(Source([Punch(emp, Now.AddMinutes(-5))], [deployment]), Guid.NewGuid(), Now).Single();

        var punch = message.Punches.Single();
        punch.Should().Match<PortalLivePunchRow>(p => p.SiteExternalId == Agwa.Id.ToString() && p.PostExternalId == Gate.Id.ToString()
            && p.GuardExternalId == emp.ToString() && p.Source == "Device" && p.Device == "Gate reader" && p.At == Now.AddMinutes(-5));
        message.Posts.Should().ContainSingle(p => p.ExternalId == Gate.Id.ToString() && p.Position == "Regular guard");
        message.Clients.Should().ContainSingle(c => c.Code == "WF");
        message.Guards.Should().ContainSingle(g => g.ClientExternalId == Waterfront.Id.ToString());
    }

    [Fact]
    public void Without_a_deployment_the_punch_goes_to_the_site_s_default_post()
    {
        var message = ClientPortalLiveBuilder.Punches(Source([Punch(Guid.NewGuid(), Now)]), Guid.NewGuid(), Now).Single();

        message.Posts.Should().ContainSingle().Which.Should().Match<PortalPostRow>(p => p.Code == "MAIN" && p.SiteExternalId == Agwa.Id.ToString());
        message.Punches.Single().PostExternalId.Should().Be(message.Posts[0].ExternalId);
    }

    [Fact]
    public void Old_punches_and_punches_without_a_client_site_or_owner_are_not_live()
    {
        var emp = Guid.NewGuid();
        var noClient = Punch(emp, Now);
        noClient.ClientId = null;
        var unknownSite = Punch(emp, Now, area: Guid.NewGuid());
        var noOwner = Punch(emp, Now);
        noOwner.EmployeeId = null;
        var punches = new[] { Punch(emp, Now.AddDays(-3)), noClient, unknownSite, noOwner };

        ClientPortalLiveBuilder.Punches(Source(punches), Guid.NewGuid(), Now).Should().BeEmpty();
    }

    [Fact]
    public void Many_punches_are_split_into_messages_of_at_most_the_limit()
    {
        var punches = Enumerable.Range(0, ClientPortalPunchesRecorded.MaxPunches + 5).Select(i => Punch(Guid.NewGuid(), Now.AddSeconds(-i))).ToList();

        var messages = ClientPortalLiveBuilder.Punches(Source(punches), Guid.NewGuid(), Now);

        messages.Select(m => m.Punches.Count).Should().Equal(ClientPortalPunchesRecorded.MaxPunches, 5);
        messages[1].Guards.Should().HaveCount(5, "each message carries only the guards of its own punches");
    }

    // ---- manning -------------------------------------------------------------------------------------------------

    private static ManningSource Manning(IReadOnlyList<PostRequirement> requirements, IReadOnlyList<Deployment>? deployments = null,
        IReadOnlyDictionary<Guid, (TimeSpan, TimeSpan)>? shifts = null) => new(
        new Dictionary<Guid, Client> { [Waterfront.Id] = Waterfront }, [Agwa], new Dictionary<Guid, ClientPost> { [Gate.Id] = Gate }, requirements,
        deployments ?? [], new Dictionary<Guid, Employee>(), shifts ?? new Dictionary<Guid, (TimeSpan, TimeSpan)>());

    [Fact]
    public void Manning_lists_each_shift_a_post_needs_with_its_times_and_a_night_shift_ends_the_next_day()
    {
        var day = new PostRequirement { PostId = Gate.Id, ShiftLabel = "DS", StartTime = new TimeOnly(6, 0), EndTime = new TimeOnly(18, 0), RequiredHeadcount = 2, EffectiveFrom = Day };
        var night = new PostRequirement { PostId = Gate.Id, ShiftLabel = "NS", StartTime = new TimeOnly(18, 0), EndTime = new TimeOnly(6, 0), RequiredHeadcount = 1, EffectiveFrom = Day };

        var message = ClientPortalLiveBuilder.Manning(Manning([day, night]), Guid.NewGuid(), Day, Day.AddDays(1)).Single();

        message.Shifts.Should().HaveCount(4);
        message.Shifts.Single(s => s.ShiftName == "DS" && s.WorkDate == Day)
            .Should().Match<PortalShiftPlanRow>(s => s.Start == Day.ToDateTime(new TimeOnly(6, 0)) && s.End == Day.ToDateTime(new TimeOnly(18, 0)) && s.RequiredHeadcount == 2);
        message.Shifts.Single(s => s.ShiftName == "NS" && s.WorkDate == Day).End.Should().Be(Day.AddDays(1).ToDateTime(new TimeOnly(6, 0)));
        message.Sites.Should().ContainSingle(s => s.Code == "AGWA");
        message.Posts.Should().ContainSingle(p => p.Position == "Regular guard");
    }

    [Fact]
    public void Shift_times_come_from_the_time_shift_when_the_requirement_has_none_and_days_off_are_skipped()
    {
        var shiftId = Guid.NewGuid();
        var weekdays = new PostRequirement
        {
            PostId = Gate.Id, ShiftLabel = "Day", TimeShiftId = shiftId, RequiredHeadcount = 1, DaysOfWeek = "Mon,Tue,Wed,Thu,Fri", EffectiveFrom = Day,
        };
        var untimed = new PostRequirement { PostId = Gate.Id, ShiftLabel = "Roving", RequiredHeadcount = 1, EffectiveFrom = Day, EffectiveTo = Day };

        var message = ClientPortalLiveBuilder.Manning(Manning([weekdays, untimed], shifts: new Dictionary<Guid, (TimeSpan, TimeSpan)> { [shiftId] = (TimeSpan.FromHours(8), TimeSpan.FromHours(17)) }),
            Guid.NewGuid(), Day, Day.AddDays(3)).Single();

        message.Shifts.Where(s => s.ShiftName == "Day").Select(s => s.WorkDate).Should().Equal([Day, Day.AddDays(1)], "Thursday and Friday; not the weekend");
        message.Shifts.Single(s => s.ShiftName == "Day" && s.WorkDate == Day).Start.Should().Be(Day.ToDateTime(new TimeOnly(8, 0)));
        message.Shifts.Single(s => s.ShiftName == "Roving").Should().Match<PortalShiftPlanRow>(s => s.Start == null && s.End == null);
    }

    [Fact]
    public void Manning_carries_everyone_deployed_in_the_days_and_skips_clients_without_posts()
    {
        var emp = Guid.NewGuid();
        var reliever = new Deployment { EmployeeId = emp, PostId = Gate.Id, Role = DeploymentRole.Reliever, StartDate = Day, EndDate = Day, EventTag = null };
        var ended = new Deployment { EmployeeId = Guid.NewGuid(), PostId = Gate.Id, Role = DeploymentRole.Regular, StartDate = Day.AddDays(-30), EndDate = Day.AddDays(-1) };
        var empty = new Client { Id = Guid.NewGuid(), Code = "X", Name = "No posts" };
        var source = Manning([], [reliever, ended]) with
        {
            Clients = new Dictionary<Guid, Client> { [Waterfront.Id] = Waterfront, [empty.Id] = empty },
            Sites = [Agwa, new ClientSite { Id = Guid.NewGuid(), ClientId = empty.Id, AreaId = Lahug.Id, Code = "E", Name = "Empty" }],
        };

        var messages = ClientPortalLiveBuilder.Manning(source, Guid.NewGuid(), Day, Day.AddDays(6));

        messages.Should().ContainSingle();
        messages[0].Assignments.Should().ContainSingle().Which.Should().Match<PortalAssignmentRow>(a => a.Role == "Reliever" && a.GuardExternalId == emp.ToString());
        messages[0].Guards.Should().ContainSingle(g => g.ExternalId == emp.ToString());
    }

    // ---- the service ---------------------------------------------------------------------------------------------

    private static ClientPortalLiveService LiveFor(UnitOfWorkService uow, EphemeralDataProtectionProvider protection, IPublishEndpoint publisher, Guid tenantId)
    {
        var tenant = Substitute.For<ITenantProvider>();
        tenant.TenantId.Returns(tenantId);
        return new ClientPortalLiveService(uow, new ClientPortalSettingsService(uow, protection), publisher, tenant);
    }

    private static async Task EnableAsync(SqliteHrmsContext db, EphemeralDataProtectionProvider protection, Guid tenantId)
    {
        // The production-like context stamps the company on what it saves, as the API does.
        await using var ctx = db.NewProductionLikeContext(tenantId);
        await new ClientPortalSettingsService(new UnitOfWorkService(ctx), protection)
            .SaveAsync(new ClientPortalSettingsRequest(true, "https://portal.example.com", "cpk_abcd.secret"), CancellationToken.None);
    }

    private static async Task SeedSiteAsync(SqliteHrmsContext db, Guid tenantId, DateOnly today)
    {
        var client = new Client { Id = Guid.NewGuid(), TenantId = tenantId, Code = "WF", Name = "Waterfront" };
        var area = new CostCenters { Id = Guid.NewGuid(), TenantId = tenantId, Code = "LHG", Name = "Lahug" };
        var site = new ClientSite { Id = Guid.NewGuid(), TenantId = tenantId, ClientId = client.Id, AreaId = area.Id, Code = "AGWA", Name = "AGWA" };
        var post = new ClientPost { Id = Guid.NewGuid(), TenantId = tenantId, ClientSiteId = site.Id, Code = "G1", Name = "Gate", Position = "Guard" };
        await using var seed = db.NewContext(tenantId);
        seed.Clients.Add(client);
        seed.Areas.Add(area);
        seed.ClientSites.Add(site);
        seed.ClientPosts.Add(post);
        seed.PostRequirements.Add(new PostRequirement
        {
            TenantId = tenantId, PostId = post.Id, ShiftLabel = "DS", StartTime = new TimeOnly(6, 0), EndTime = new TimeOnly(18, 0), RequiredHeadcount = 2, EffectiveFrom = today,
        });
        await seed.SaveChangesAsync();
    }

    [Fact]
    public async Task Punches_are_not_sent_while_the_portal_connection_is_off()
    {
        using var db = new SqliteHrmsContext();
        var publisher = Substitute.For<IPublishEndpoint>();
        await using var ctx = db.NewContext();

        var sent = await LiveFor(new UnitOfWorkService(ctx), new EphemeralDataProtectionProvider(), publisher, Guid.Empty)
            .PublishPunchesAsync([Punch(Guid.NewGuid(), DateTime.Now)], CancellationToken.None);

        sent.Should().Be(0);
        await publisher.DidNotReceiveWithAnyArgs().Publish<ClientPortalPunchesRecorded>(default!, default);
    }

    [Fact]
    public async Task The_nightly_send_covers_only_companies_with_the_portal_on_each_with_its_own_posts()
    {
        using var db = new SqliteHrmsContext();
        var protection = new EphemeralDataProtectionProvider();
        var on = Guid.NewGuid();
        var off = Guid.NewGuid();
        var today = new DateOnly(2026, 10, 1);
        await SeedSiteAsync(db, on, today);
        await SeedSiteAsync(db, off, today);
        await EnableAsync(db, protection, on);
        var publisher = Substitute.For<IPublishEndpoint>();

        await using var ctx = db.NewContext();
        var sent = await ClientPortalLiveService.PublishManningForAllCompaniesAsync(ctx, publisher, today, CancellationToken.None);

        sent.Should().Be(1);
        await publisher.Received(1).Publish(
            Arg.Is<ClientPortalManningPublished>(m => m.TenantId == on && m.From == today && m.To == today.AddDays(ClientPortalLiveService.ManningDays - 1)
                && m.Posts.Count == 1 && m.Shifts.Count == ClientPortalLiveService.ManningDays),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sending_manning_by_hand_is_refused_while_the_connection_is_off()
    {
        using var db = new SqliteHrmsContext();
        await using var ctx = db.NewContext();

        var act = () => LiveFor(new UnitOfWorkService(ctx), new EphemeralDataProtectionProvider(), Substitute.For<IPublishEndpoint>(), Guid.Empty)
            .PublishManningAsync(null, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }
}
