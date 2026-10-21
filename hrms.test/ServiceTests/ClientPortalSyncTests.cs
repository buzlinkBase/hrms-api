using System.Net;
using System.Text;
using System.Text.Json;
using Hrms.Core.Messaging;
using Hrms.Core.Services;
using Hrms.Core.Services.ClientPortal;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Domain.ValueObjects;
using hrms.test.TestSupport;
using MassTransit;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Onepunch.Common.Lib.DTO;

namespace hrms.test.ServiceTests;

/// <summary>Sending posted DTR rows to the Client Portal: the row mapping, the saved connection, the bus messages and the portal's answers.</summary>
public class ClientPortalSyncTests
{
    private static readonly DateOnly Day = new(2026, 10, 1);
    private static readonly DateTime Start = new(2026, 10, 1, 6, 0, 0);
    private static readonly DateTime End = new(2026, 10, 1, 18, 0, 0);

    private static ClientPortalSettingsService Settings(IUnitOfWorkService uow) => new(uow, new EphemeralDataProtectionProvider());

    private static DailyRecord Record(Guid employeeId, Guid clientId, Guid areaId, string batch, Action<DailyRecord>? tweak = null)
    {
        var r = new DailyRecord
        {
            Id = Guid.NewGuid(), EmployeeId = employeeId, ClientId = clientId, AreaId = areaId, BatchCode = batch, WorkDate = Day,
            ShiftName = "Day", ShiftStartTime = Start, ShiftEndTime = End, StartTime = Start, EndTime = End, FullName = "Juan Dela Cruz",
            RegularNetHours = 11,
        };
        tweak?.Invoke(r);
        return r;
    }

    // ---- mapping -------------------------------------------------------------------------------------------------

    [Fact]
    public void Hours_are_bucketed_without_double_counting_night_differential()
    {
        var r = new DailyRecord
        {
            RegularNetHours = 8, RestDayHours = 2, RegularOTHours = 1, RestDayOTHours = 0.5,
            RegularNDHours = 3, RegularNDOTHours = 1, LegalHolHours = 4, SpecialHolHours = 1, DoubleLegalHours = 2, LegalHolOTHours = 0.25,
        };

        ClientPortalPayloadBuilder.RegularHours(r).Should().Be(10, "rest-day work counts as regular in the portal");
        ClientPortalPayloadBuilder.OvertimeHours(r).Should().Be(1.75);
        ClientPortalPayloadBuilder.NightDiffHours(r).Should().Be(4);
        ClientPortalPayloadBuilder.HolidayHours(r).Should().Be(7);
    }

    [Fact]
    public void Rows_without_a_client_or_site_are_not_sent_and_without_deployment_setup_each_site_gets_a_default_post()
    {
        var client = new Client { Id = Guid.NewGuid(), Code = "LGU", Name = "LGU" };
        var area = new CostCenters { Id = Guid.NewGuid(), Code = "HALL", Name = "City Hall" };
        var emp = Guid.NewGuid();
        var ok = Record(emp, client.Id, area.Id, "b");
        var noClient = Record(emp, client.Id, area.Id, "b", r => r.ClientId = null);
        var noArea = Record(emp, client.Id, area.Id, "b", r => r.AreaId = null);

        var payload = ClientPortalPayloadBuilder.Build(new PortalSourceData(
            [ok, noClient, noArea], new Dictionary<Guid, Client> { [client.Id] = client }, new Dictionary<Guid, CostCenters> { [area.Id] = area },
            new Dictionary<Guid, Employee>(), new Dictionary<Guid, double>(), [], []));

        payload.Records.Should().ContainSingle().Which.ExternalId.Should().Be(ok.Id.ToString());
        payload.Clients.Should().ContainSingle();
        payload.Sites.Should().ContainSingle().Which.Name.Should().Be("City Hall");
        payload.Posts.Should().ContainSingle().Which.Should().Match<PortalPostRow>(p => p.SiteExternalId == payload.Sites[0].ExternalId && p.Code == "MAIN");
        payload.Guards.Should().ContainSingle();
        payload.Records[0].DeploymentRole.Should().BeNull();
    }

    [Fact]
    public void Days_not_worked_are_sent_as_absent_leave_or_rest_day()
    {
        var client = new Client { Id = Guid.NewGuid(), Code = "LGU", Name = "LGU" };
        var area = new CostCenters { Id = Guid.NewGuid(), Code = "HALL", Name = "City Hall" };
        var emp = Guid.NewGuid();
        void NoTimes(DailyRecord r) { r.StartTime = null; r.EndTime = null; r.RegularNetHours = 0; }
        var absent = Record(emp, client.Id, area.Id, "b", r => { NoTimes(r); r.AbsentCount = 1; r.WorkTypeEnum = WorkType.Absent; });
        var leave = Record(emp, client.Id, area.Id, "b", r =>
        {
            NoTimes(r); r.WorkDate = Day.AddDays(1); r.WorkTypeEnum = WorkType.PaidLeave; r.PaidLeaveHours = 8;
            r.LeavesInfo = [new DtrLeaveMetaData { Name = "Vacation Leave", Hours = 8 }];
        });
        var rest = Record(emp, client.Id, area.Id, "b", r => { NoTimes(r); r.WorkDate = Day.AddDays(2); r.WorkTypeEnum = WorkType.RestDay; });

        var payload = ClientPortalPayloadBuilder.Build(new PortalSourceData(
            [absent, leave, rest], new Dictionary<Guid, Client> { [client.Id] = client }, new Dictionary<Guid, CostCenters> { [area.Id] = area },
            new Dictionary<Guid, Employee>(), new Dictionary<Guid, double>(), [], []));

        payload.Records.Should().HaveCount(3);
        payload.Records.Single(r => r.ExternalId == absent.Id.ToString()).Should().Match<PortalDtrRow>(r => r.Absent && r.LeaveHours == 0 && r.Punches.Count == 0);
        payload.Records.Single(r => r.ExternalId == leave.Id.ToString())
            .Should().Match<PortalDtrRow>(r => !r.Absent && r.LeaveHours == 8 && r.LeaveType == "Vacation Leave" && r.LeavePaid);
        payload.Records.Single(r => r.ExternalId == rest.Id.ToString()).Should().Match<PortalDtrRow>(r => r.DayType == "RestDay" && !r.Absent);
    }

    [Fact]
    public void Day_types_holidays_scheduled_times_and_night_shifts_are_sent_with_four_decimal_hours()
    {
        var client = new Client { Id = Guid.NewGuid(), Code = "LGU", Name = "LGU" };
        var area = new CostCenters { Id = Guid.NewGuid(), Code = "HALL", Name = "City Hall" };
        var night = Record(Guid.NewGuid(), client.Id, area.Id, "b", r =>
        {
            r.WorkTypeEnum = WorkType.LegalHolidayDuty; r.ShiftName = "NS"; r.ShiftStartTime = Start.AddHours(12); r.ShiftEndTime = End.AddHours(12);
            r.RegularNetHours = 0; r.LegalHolHours = 7.9833333; r.LegalHolOTHours = 4; r.RestDayHours = 0;
        });
        var special = Record(Guid.NewGuid(), client.Id, area.Id, "b", r => { r.WorkTypeEnum = WorkType.RestDaySpecialHolidayDuty; r.RestSpecialDayHours = 8; });

        var payload = ClientPortalPayloadBuilder.Build(new PortalSourceData(
            [night, special], new Dictionary<Guid, Client> { [client.Id] = client }, new Dictionary<Guid, CostCenters> { [area.Id] = area },
            new Dictionary<Guid, Employee>(), new Dictionary<Guid, double>(), [], []));

        var n = payload.Records.Single(r => r.ExternalId == night.Id.ToString());
        n.Should().Match<PortalDtrRow>(r => r.DayType == "Legal" && r.ShiftKind == "Night" && r.LegalHolidayHours == 7.9833m
            && r.HolidayHours == 7.9833m && r.HolidayOvertimeHours == 4 && r.OvertimeHours == 4 && r.ScheduledStart == Start.AddHours(12));
        var s = payload.Records.Single(r => r.ExternalId == special.Id.ToString());
        s.Should().Match<PortalDtrRow>(r => r.DayType == "RestSpecial" && r.ShiftKind == "Day" && r.SpecialHolidayHours == 8 && r.LegalHolidayHours == 0);
    }

    [Fact]
    public void A_day_is_billed_to_the_site_where_the_person_punched_not_their_home_site()
    {
        var home = new Client { Id = Guid.NewGuid(), Code = "HOME", Name = "Home Client" };
        var other = new Client { Id = Guid.NewGuid(), Code = "PORT", Name = "Port" };
        var homeArea = new CostCenters { Id = Guid.NewGuid(), Code = "H", Name = "Home Site" };
        var otherArea = new CostCenters { Id = Guid.NewGuid(), Code = "P", Name = "Pier 4" };
        var emp = Guid.NewGuid();
        var record = Record(emp, home.Id, homeArea.Id, "b");
        var punches = new[]
        {
            new Attendance { EmployeeId = emp, WorkDateTime = Start, ClientId = other.Id, OperationAreaId = otherArea.Id },
            new Attendance { EmployeeId = emp, WorkDateTime = End, ClientId = other.Id, OperationAreaId = otherArea.Id },
        };

        var payload = ClientPortalPayloadBuilder.Build(new PortalSourceData(
            [record], new Dictionary<Guid, Client> { [home.Id] = home, [other.Id] = other },
            new Dictionary<Guid, CostCenters> { [homeArea.Id] = homeArea, [otherArea.Id] = otherArea },
            new Dictionary<Guid, Employee>(), new Dictionary<Guid, double>(), punches, []));

        payload.Records.Single().ClientExternalId.Should().Be(other.Id.ToString());
        payload.Sites.Should().ContainSingle().Which.Name.Should().Be("Pier 4");
        payload.Guards.Single().ClientExternalId.Should().Be(other.Id.ToString());
    }

    [Fact]
    public void A_deployed_person_is_sent_under_the_real_site_and_post_with_its_role_and_the_post_manning()
    {
        var client = new Client { Id = Guid.NewGuid(), Code = "WF", Name = "Waterfront" };
        var area = new CostCenters { Id = Guid.NewGuid(), Code = "WFA", Name = "Waterfront Area" };
        var site = new ClientSite { Id = Guid.NewGuid(), ClientId = client.Id, AreaId = area.Id, Code = "AGWA", Name = "AGWA Building" };
        var post = new ClientPost { Id = Guid.NewGuid(), ClientSiteId = site.Id, Code = "G1", Name = "Main Gate", Position = "Regular guard" };
        var emp = Guid.NewGuid();
        var regular = new Deployment { Id = Guid.NewGuid(), EmployeeId = emp, PostId = post.Id, Role = DeploymentRole.Regular, StartDate = Day.AddDays(-30) };
        var augmentation = new Deployment
        {
            Id = Guid.NewGuid(), EmployeeId = emp, PostId = post.Id, Role = DeploymentRole.Augmentation, StartDate = Day, EndDate = Day, EventTag = "CWE 2026",
        };
        var requirement = new PostRequirement { Id = Guid.NewGuid(), PostId = post.Id, ShiftLabel = "Day", RequiredHeadcount = 2, EffectiveFrom = Day.AddDays(-30) };
        var notOnSunday = new PostRequirement
        {
            Id = Guid.NewGuid(), PostId = post.Id, ShiftLabel = "Night", RequiredHeadcount = 1, DaysOfWeek = "Sun", EffectiveFrom = Day.AddDays(-30),
        };

        var payload = ClientPortalPayloadBuilder.Build(new PortalSourceData(
            [Record(emp, client.Id, area.Id, "b")], new Dictionary<Guid, Client> { [client.Id] = client }, new Dictionary<Guid, CostCenters> { [area.Id] = area },
            new Dictionary<Guid, Employee>(), new Dictionary<Guid, double>(), [], [new ScheduledHeadcount(client.Id, area.Id, Day, "Day", 9)],
            [site], new Dictionary<Guid, ClientPost> { [post.Id] = post }, [requirement, notOnSunday], [regular, augmentation]));

        payload.Sites.Should().ContainSingle().Which.Should().Match<PortalSiteRow>(s => s.ExternalId == site.Id.ToString() && s.Code == "AGWA");
        payload.Posts.Should().ContainSingle().Which.Should().Match<PortalPostRow>(p => p.ExternalId == post.Id.ToString() && p.Position == "Regular guard");
        payload.Records.Single().Should().Match<PortalDtrRow>(r => r.PostExternalId == post.Id.ToString() && r.DeploymentRole == "Augmentation" && r.EventTag == "CWE 2026");
        payload.Schedules.Should().ContainSingle("the 1st of October 2026 is a Thursday, and the work schedule only counts for default posts")
            .Which.Should().Match<PortalScheduleRow>(s => s.PostExternalId == post.Id.ToString() && s.RequiredHeadcount == 2 && s.ShiftName == "Day");
    }

    [Fact]
    public void Punches_come_from_the_device_log_inside_the_shift_window_else_from_the_recorded_times()
    {
        var client = new Client { Id = Guid.NewGuid(), Code = "LGU", Name = "LGU" };
        var area = new CostCenters { Id = Guid.NewGuid(), Code = "HALL", Name = "City Hall" };
        var withLog = Guid.NewGuid();
        var manual = Guid.NewGuid();
        var attendance = new[]
        {
            new Attendance { EmployeeId = withLog, WorkDateTime = Start.AddMinutes(-3) },
            new Attendance { EmployeeId = withLog, WorkDateTime = End.AddMinutes(4) },
            new Attendance { EmployeeId = withLog, WorkDateTime = End.AddDays(1) },
        };

        var payload = ClientPortalPayloadBuilder.Build(new PortalSourceData(
            [Record(withLog, client.Id, area.Id, "b"), Record(manual, client.Id, area.Id, "b")],
            new Dictionary<Guid, Client> { [client.Id] = client }, new Dictionary<Guid, CostCenters> { [area.Id] = area },
            new Dictionary<Guid, Employee>(), new Dictionary<Guid, double>(), attendance, []));

        payload.Records.Single(r => r.GuardExternalId == withLog.ToString()).Punches.Should().HaveCount(2).And.OnlyContain(p => p.Kind == "punch");
        payload.Records.Single(r => r.GuardExternalId == manual.ToString()).Punches.Select(p => p.Kind).Should().Equal("in", "out");
    }

    [Fact]
    public void Break_minutes_late_undertime_and_schedule_are_mapped_and_missing_codes_get_a_fallback()
    {
        var client = new Client { Id = Guid.NewGuid(), Code = "", Name = "LGU" };
        var area = new CostCenters { Id = Guid.NewGuid(), Code = "HALL", Name = "City Hall" };
        var shift = Guid.NewGuid();
        var emp = new Employee { Id = Guid.NewGuid(), EmployeeNo = "E-7", FirstName = "Maria", LastName = "Santos" };
        var record = Record(emp.Id, client.Id, area.Id, "b", r => { r.ShiftId = shift; r.LateMinutes = 12.4; r.UTMinutes = 30; r.FullName = null; });

        var payload = ClientPortalPayloadBuilder.Build(new PortalSourceData(
            [record], new Dictionary<Guid, Client> { [client.Id] = client }, new Dictionary<Guid, CostCenters> { [area.Id] = area },
            new Dictionary<Guid, Employee> { [emp.Id] = emp }, new Dictionary<Guid, double> { [shift] = 60 }, [],
            [new ScheduledHeadcount(client.Id, area.Id, Day, "Day", 3), new ScheduledHeadcount(client.Id, Guid.NewGuid(), Day, "Day", 9)]));

        var row = payload.Records.Single();
        (row.BreakMinutes, row.LateMinutes, row.UndertimeMinutes).Should().Be((60, 12, 30));
        payload.Clients.Single().Code.Should().HaveLength(8, "a blank client code falls back to a short id");
        payload.Guards.Single().Should().Match<PortalGuardRow>(g => g.Code == "E-7" && g.FullName == "Santos, Maria");
        payload.Schedules.Should().ContainSingle().Which.RequiredHeadcount.Should().Be(3);
    }

    // ---- settings ------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("https://portal.example.com/", "https://portal.example.com")]
    [InlineData("  https://portal.example.com/app/  ", "https://portal.example.com/app")]
    [InlineData("http://localhost:3300", "http://localhost:3300")]
    [InlineData("", "")]
    public void Portal_address_is_normalised(string input, string expected) =>
        ClientPortalSettingsService.NormalizeUrl(input).Should().Be(expected);

    [Theory]
    [InlineData("portal.example.com")]
    [InlineData("http://portal.example.com")]
    [InlineData("ftp://portal.example.com")]
    [InlineData("https://user:pass@portal.example.com")]
    public void Unsafe_or_malformed_portal_addresses_are_refused(string input) =>
        FluentActions.Invoking(() => ClientPortalSettingsService.NormalizeUrl(input)).Should().Throw<ValidationException>();

    [Fact]
    public async Task Saved_key_is_encrypted_at_rest_never_returned_and_kept_when_left_blank()
    {
        using var db = new SqliteHrmsContext();
        var protection = new EphemeralDataProtectionProvider();
        await using (var ctx = db.NewContext())
        {
            var service = new ClientPortalSettingsService(new UnitOfWorkService(ctx), protection);
            var saved = await service.SaveAsync(new ClientPortalSettingsRequest(true, "https://portal.example.com/", "cpk_abcd.secret"), CancellationToken.None);
            saved.Should().Match<ClientPortalSettingsModel>(s => s.Enabled && s.HasApiKey && s.PortalUrl == "https://portal.example.com");
        }

        await using (var verify = db.NewContext())
        {
            var stored = await verify.GeneralSettings.FirstAsync(x => x.IdentityType == "ClientPortal" && x.Description == "ApiKey");
            stored.Value.Should().NotContain("secret");
        }

        await using (var ctx = db.NewContext())
        {
            var service = new ClientPortalSettingsService(new UnitOfWorkService(ctx), protection);
            await service.SaveAsync(new ClientPortalSettingsRequest(true, "https://portal.example.com", null), CancellationToken.None);
            (await service.GetCredentialsAsync(CancellationToken.None)).Should().BeEquivalentTo(new ClientPortalCredentials("https://portal.example.com", "cpk_abcd.secret"));
        }
    }

    [Fact]
    public async Task Cannot_switch_on_without_a_key_and_switching_off_stops_the_push()
    {
        using var db = new SqliteHrmsContext();
        await using var ctx = db.NewContext();
        var service = Settings(new UnitOfWorkService(ctx));

        await FluentActions.Awaiting(() => service.SaveAsync(new ClientPortalSettingsRequest(true, "https://portal.example.com", null), CancellationToken.None))
            .Should().ThrowAsync<ValidationException>();
    }

    // ---- the push ------------------------------------------------------------------------------------------------

    private static async Task<(Guid BatchId, string BatchCode)> SeedBatchAsync(SqliteHrmsContext db, bool posted = true)
    {
        var client = new Client { Id = Guid.NewGuid(), Code = "LGU", Name = "LGU Cordova" };
        var area = new CostCenters { Id = Guid.NewGuid(), Code = "HALL", Name = "City Hall" };
        var payroll = new PayrollGroup { Id = Guid.NewGuid() };
        var emp = new Employee { Id = Guid.NewGuid(), PayrollGroupId = payroll.Id, EmployeeNo = "E-1", FirstName = "Juan", LastName = "Dela Cruz", ClientId = client.Id, AreaId = area.Id };
        var batch = new DTRBatch
        {
            Id = Guid.NewGuid(), BatchCode = "DTR-sync-1", PayPeriodStart = Day, PayPeriodEnd = Day, GeneratedByEmployeeId = emp.Id, IsPosted = posted,
        };
        await using var seed = db.NewContext();
        seed.PayrollGroups.Add(payroll);
        seed.Clients.Add(client);
        seed.Areas.Add(area);
        seed.Employees.Add(emp);
        seed.DTRBatches.Add(batch);
        seed.DailyTimeRecords.Add(Record(emp.Id, client.Id, area.Id, batch.BatchCode));
        await seed.SaveChangesAsync();
        return (batch.Id, batch.BatchCode);
    }

    private static async Task EnableAsync(SqliteHrmsContext db, EphemeralDataProtectionProvider protection)
    {
        await using var ctx = db.NewContext();
        await new ClientPortalSettingsService(new UnitOfWorkService(ctx), protection)
            .SaveAsync(new ClientPortalSettingsRequest(true, "https://portal.example.com", "cpk_abcd.secret"), CancellationToken.None);
    }

    private static ClientPortalSyncService SyncFor(UnitOfWorkService uow, EphemeralDataProtectionProvider protection, IPublishEndpoint publisher, Guid tenantId)
    {
        var tenant = Substitute.For<ITenantProvider>();
        tenant.TenantId.Returns(tenantId);
        return new ClientPortalSyncService(uow, new ClientPortalSettingsService(uow, protection), publisher, tenant);
    }

    [Fact]
    public async Task Sync_publishes_the_batch_with_its_parents_over_the_bus_and_remembers_it_is_waiting_for_the_portal()
    {
        using var db = new SqliteHrmsContext();
        var protection = new EphemeralDataProtectionProvider();
        var (batchId, batchCode) = await SeedBatchAsync(db);
        await EnableAsync(db, protection);
        var publisher = Substitute.For<IPublishEndpoint>();
        var tenantId = Guid.NewGuid();

        await using (var ctx = db.NewContext())
        {
            var summary = await SyncFor(new UnitOfWorkService(ctx), protection, publisher, tenantId).SyncBatchAsync(batchId, ClientPortalSyncRequested.Approved, CancellationToken.None);
            summary.Should().Match<ClientPortalSyncSummary>(s => s.Records == 1 && s.Waiting && s.Parts == 1 && s.BatchCode == batchCode && s.Operation == "Upsert");
        }

        await publisher.Received(1).Publish(
            Arg.Is<ClientPortalAttendanceUpserted>(m => m.TenantId == tenantId && m.BatchCode == batchCode && m.Reason == "Approved" && m.Part == 1 && m.Parts == 1
                && m.Clients.Count == 1 && m.Sites.Count == 1 && m.Posts.Count == 1 && m.Guards.Count == 1 && m.Records.Count == 1),
            Arg.Any<CancellationToken>());
        await using var verify = db.NewContext();
        var last = await new ClientPortalSettingsService(new UnitOfWorkService(verify), protection).GetAsync(CancellationToken.None);
        last.LastSync!.Waiting.Should().BeTrue();
    }

    [Fact]
    public async Task Sync_does_nothing_when_off_or_when_the_batch_is_not_posted()
    {
        using var db = new SqliteHrmsContext();
        var protection = new EphemeralDataProtectionProvider();
        var (posted, _) = await SeedBatchAsync(db);
        var publisher = Substitute.For<IPublishEndpoint>();

        await using (var off = db.NewContext())
            (await SyncFor(new UnitOfWorkService(off), protection, publisher, Guid.NewGuid()).SyncBatchAsync(posted, ClientPortalSyncRequested.Approved, CancellationToken.None)).Should().BeNull();

        await EnableAsync(db, protection);
        await using (var unposted = db.NewContext())
            (await SyncFor(new UnitOfWorkService(unposted), protection, publisher, Guid.NewGuid()).SyncBatchAsync(Guid.NewGuid(), ClientPortalSyncRequested.Approved, CancellationToken.None)).Should().BeNull();

        await publisher.DidNotReceiveWithAnyArgs().Publish<ClientPortalAttendanceUpserted>(default!, default);
    }

    [Fact]
    public async Task Sync_of_single_rows_sends_only_the_ones_still_posted()
    {
        using var db = new SqliteHrmsContext();
        var protection = new EphemeralDataProtectionProvider();
        var (_, batchCode) = await SeedBatchAsync(db);
        Guid posted, unposted;
        await using (var seed = db.NewContext())
        {
            var first = await seed.DailyTimeRecords.FirstAsync(r => r.BatchCode == batchCode);
            first.Posted = true;
            var copy = Record(first.EmployeeId, first.ClientId!.Value, first.AreaId!.Value, batchCode, r => r.WorkDate = Day.AddDays(1));
            seed.DailyTimeRecords.Add(copy);
            await seed.SaveChangesAsync();
            (posted, unposted) = (first.Id, copy.Id);
        }
        await EnableAsync(db, protection);
        var publisher = Substitute.For<IPublishEndpoint>();

        await using (var ctx = db.NewContext())
            await SyncFor(new UnitOfWorkService(ctx), protection, publisher, Guid.NewGuid()).SyncRecordsAsync([posted, unposted], ClientPortalSyncRequested.PunchChanged, CancellationToken.None);

        await publisher.Received(1).Publish(
            Arg.Is<ClientPortalAttendanceUpserted>(m => m.Reason == "PunchChanged" && m.Records.Count == 1 && m.Records[0].ExternalId == posted.ToString()),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void A_large_payload_is_split_into_parts_that_each_carry_only_their_own_parents()
    {
        var clientA = new Client { Id = Guid.NewGuid(), Code = "A", Name = "A" };
        var clientB = new Client { Id = Guid.NewGuid(), Code = "B", Name = "B" };
        var areaA = new CostCenters { Id = Guid.NewGuid(), Code = "SA", Name = "Site A" };
        var areaB = new CostCenters { Id = Guid.NewGuid(), Code = "SB", Name = "Site B" };
        var empA = Guid.NewGuid();
        var empB = Guid.NewGuid();
        var records = new[] { Record(empA, clientA.Id, areaA.Id, "b"), Record(empA, clientA.Id, areaA.Id, "b"), Record(empB, clientB.Id, areaB.Id, "b") };
        var payload = ClientPortalPayloadBuilder.Build(new PortalSourceData(
            records, new Dictionary<Guid, Client> { [clientA.Id] = clientA, [clientB.Id] = clientB },
            new Dictionary<Guid, CostCenters> { [areaA.Id] = areaA, [areaB.Id] = areaB },
            new Dictionary<Guid, Employee>(), new Dictionary<Guid, double>(), [],
            [new ScheduledHeadcount(clientA.Id, areaA.Id, Day, "Day", 2), new ScheduledHeadcount(clientB.Id, areaB.Id, Day, "Day", 1)]));

        var parts = ClientPortalPayloadBuilder.Split(payload, Guid.NewGuid(), "b", "Approved", maxRecords: 2);

        parts.Should().HaveCount(2);
        parts.Select(p => (p.Part, p.Parts)).Should().Equal((1, 2), (2, 2));
        parts[0].Records.Should().HaveCount(2);
        parts[0].Clients.Should().ContainSingle(c => c.Code == "A");
        parts[0].Schedules.Should().ContainSingle(s => s.RequiredHeadcount == 2);
        parts[1].Clients.Should().ContainSingle(c => c.Code == "B");
        parts[1].Sites.Should().ContainSingle(s => s.Name == "Site B");
        parts[1].Guards.Should().ContainSingle(g => g.ExternalId == empB.ToString());
    }

    [Fact]
    public void Nothing_worked_means_no_message()
    {
        var payload = new PortalPayload([], [], [], [], [], []);
        ClientPortalPayloadBuilder.Split(payload, Guid.NewGuid(), "b", "Approved").Should().BeEmpty();
    }

    // ---- the portal's answers ------------------------------------------------------------------------------------

    private static ClientPortalAttendanceResult Answer(string operation, int part, int parts, int records, int accepted, params PortalRowError[] errors) => new()
    {
        TenantId = Guid.NewGuid(), BatchCode = "b", Operation = operation, Part = part, Parts = parts,
        Records = records, Accepted = accepted, Rejected = errors.Length, Errors = errors.ToList(),
    };

    [Fact]
    public void Answers_to_the_parts_of_one_send_add_up_and_stop_waiting_when_all_are_in()
    {
        var sent = new ClientPortalSyncSummary("b", DateTime.UtcNow, 3, 0, 0, true, "Sent", null, "Upsert", Waiting: true, PartsReceived: 0, Parts: 2);

        var afterFirst = ClientPortalSettingsService.Merge(sent, Answer("Upsert", 1, 2, 2, 2));
        afterFirst.Should().Match<ClientPortalSyncSummary>(s => s.Waiting && s.PartsReceived == 1 && s.Accepted == 2 && s.Records == 3);

        var afterSecond = ClientPortalSettingsService.Merge(afterFirst, Answer("Upsert", 2, 2, 1, 0, new PortalRowError("r3", "This record is verified and locked; it can no longer be changed.")));
        afterSecond.Should().Match<ClientPortalSyncSummary>(s => !s.Waiting && !s.Succeeded && s.Accepted == 2 && s.Rejected == 1 && s.Records == 3);
        afterSecond.Errors.Should().ContainSingle(e => e.ExternalId == "r3");
    }

    [Fact]
    public void Removal_answers_count_their_own_rows_and_a_different_send_starts_over()
    {
        var removed = ClientPortalSettingsService.Merge(null, Answer("Remove", 1, 2, 1000, 1000));
        removed = ClientPortalSettingsService.Merge(removed, Answer("Remove", 2, 2, 5, 5));
        removed.Should().Match<ClientPortalSyncSummary>(s => s.Records == 1005 && s.Accepted == 1005 && s.Succeeded && !s.Waiting && s.Operation == "Remove");

        var next = ClientPortalSettingsService.Merge(removed, Answer("Upsert", 1, 1, 4, 4));
        next.Should().Match<ClientPortalSyncSummary>(s => s.Records == 4 && s.Accepted == 4 && s.Operation == "Upsert" && s.PartsReceived == 1);
    }

    [Fact]
    public async Task The_result_worker_saves_the_answer_as_the_last_sync()
    {
        using var db = new SqliteHrmsContext();
        var protection = new EphemeralDataProtectionProvider();
        var context = Substitute.For<ConsumeContext<ClientPortalAttendanceResult>>();
        context.Message.Returns(Answer("Remove", 1, 1, 2, 1, new PortalRowError("x", "This date falls in a period that was already verified and locked.")));

        await using (var ctx = db.NewContext())
            await new ClientPortalAttendanceResultWorker(new ClientPortalSettingsService(new UnitOfWorkService(ctx), protection)).Consume(context);

        await using var verify = db.NewContext();
        var last = (await new ClientPortalSettingsService(new UnitOfWorkService(verify), protection).GetAsync(CancellationToken.None)).LastSync;
        last.Should().Match<ClientPortalSyncSummary>(s => s.Operation == "Remove" && s.Rejected == 1 && !s.Succeeded);
    }

    // ---- requesting a push ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Staging_publishes_only_when_the_connection_is_on_and_never_commits()
    {
        using var db = new SqliteHrmsContext();
        var protection = new EphemeralDataProtectionProvider();
        var (batchId, batchCode) = await SeedBatchAsync(db, posted: false);
        var publisher = Substitute.For<IPublishEndpoint>();

        await using (var ctx = db.NewContext())
        {
            var uow = new UnitOfWorkService(ctx);
            var requests = new ClientPortalSyncRequestService(new ClientPortalSettingsService(uow, protection), new DTRBatchService(uow), publisher);
            (await requests.StageAsync(batchId, CancellationToken.None)).Should().BeFalse("the connection is off");
        }
        await publisher.DidNotReceiveWithAnyArgs().Publish<ClientPortalSyncRequested>(default!, default);

        await EnableAsync(db, protection);
        await using (var ctx = db.NewContext())
        {
            var uow = new UnitOfWorkService(ctx);
            var requests = new ClientPortalSyncRequestService(new ClientPortalSettingsService(uow, protection), new DTRBatchService(uow), publisher);
            (await requests.StageAsync(batchId, CancellationToken.None)).Should().BeTrue("staging is allowed before the approval posts the batch");
        }
        await publisher.Received(1).Publish(Arg.Is<ClientPortalSyncRequested>(m => m.DtrBatchId == batchId && m.BatchCode == batchCode), Arg.Any<CancellationToken>());
    }

    // ---- telling the portal a client changed ---------------------------------------------------------------------

    private static ClientService ClientsFor(UnitOfWorkService uow, EphemeralDataProtectionProvider protection, IPublishEndpoint publisher, Guid tenantId)
    {
        var services = new ServiceCollection().AddSingleton(publisher).BuildServiceProvider();
        var tenant = Substitute.For<ITenantProvider>();
        tenant.TenantId.Returns(tenantId);
        var change = new ClientPortalClientChangeService(
            new ClientPortalSettingsService(uow, protection), tenant, services, NullLogger<ClientPortalClientChangeService>.Instance);
        return new ClientService(uow, change);
    }

    [Fact]
    public async Task A_client_change_is_staged_for_the_portal_only_when_the_connection_is_on_and_is_committed_with_the_save()
    {
        using var db = new SqliteHrmsContext();
        var protection = new EphemeralDataProtectionProvider();
        var publisher = Substitute.For<IPublishEndpoint>();
        var tenantId = Guid.NewGuid();

        await using (var ctx = db.NewContext())
            await ClientsFor(new UnitOfWorkService(ctx), protection, publisher, tenantId).AddAsync(new Client { Code = "000001", Name = "Off Corp" }, CancellationToken.None);
        await publisher.DidNotReceiveWithAnyArgs().Publish<Onepunch.Common.Lib.DTO.ClientChanged>(default!, default);

        await EnableAsync(db, protection);
        Guid id;
        await using (var ctx = db.NewContext())
        {
            var client = new Client { Code = "000002", Name = "On Corp" };
            await ClientsFor(new UnitOfWorkService(ctx), protection, publisher, tenantId).AddAsync(client, CancellationToken.None);
            id = client.Id;
        }
        await publisher.Received(1).Publish(
            Arg.Is<Onepunch.Common.Lib.DTO.ClientChanged>(m => m.ClientId == id && m.TenantId == tenantId && m.Code == "000002" && m.Name == "On Corp" && m.ChangeType == "Created"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Editing_and_deleting_a_client_tell_the_portal_with_its_status()
    {
        using var db = new SqliteHrmsContext();
        var protection = new EphemeralDataProtectionProvider();
        var publisher = Substitute.For<IPublishEndpoint>();
        var id = Guid.NewGuid();
        await using (var seed = db.NewContext())
        {
            seed.Clients.Add(new Client { Id = id, Code = "000003", Name = "Acme" });
            await seed.SaveChangesAsync();
        }
        await EnableAsync(db, protection);

        await using (var ctx = db.NewContext())
            await ClientsFor(new UnitOfWorkService(ctx), protection, publisher, Guid.NewGuid())
                .UpdateAsync(new UpdateClient { Id = id, Code = "000003", Name = "Acme Renamed", Status = "INACTIVE" }, CancellationToken.None);
        await publisher.Received(1).Publish(
            Arg.Is<Onepunch.Common.Lib.DTO.ClientChanged>(m => m.ClientId == id && m.Name == "Acme Renamed" && m.Status == "INACTIVE" && m.ChangeType == "Updated"),
            Arg.Any<CancellationToken>());

        await using (var ctx = db.NewContext())
            await ClientsFor(new UnitOfWorkService(ctx), protection, publisher, Guid.NewGuid()).DeleteAsync(id, CancellationToken.None);
        await publisher.Received(1).Publish(
            Arg.Is<Onepunch.Common.Lib.DTO.ClientChanged>(m => m.ClientId == id && m.ChangeType == "Deleted"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_portal_message_that_cannot_be_queued_never_blocks_saving_the_client()
    {
        using var db = new SqliteHrmsContext();
        var protection = new EphemeralDataProtectionProvider();
        await EnableAsync(db, protection);
        var publisher = Substitute.For<IPublishEndpoint>();
        publisher.Publish(Arg.Any<Onepunch.Common.Lib.DTO.ClientChanged>(), Arg.Any<CancellationToken>()).Returns(_ => throw new InvalidOperationException("bus down"));

        await using (var ctx = db.NewContext())
            await ClientsFor(new UnitOfWorkService(ctx), protection, publisher, Guid.NewGuid()).AddAsync(new Client { Code = "000004", Name = "Saved Anyway" }, CancellationToken.None);

        await using var verify = db.NewContext();
        (await verify.Clients.AnyAsync(c => c.Name == "Saved Anyway")).Should().BeTrue();
    }

    // ---- sending every existing client ---------------------------------------------------------------------------

    private static (ClientPortalClientBackfillService Backfill, ClientPortalSettingsService Settings, ClientPortalClientChangeService Changes) BackfillFor(
        UnitOfWorkService uow, EphemeralDataProtectionProvider protection, IPublishEndpoint publisher, Guid tenantId)
    {
        var services = new ServiceCollection().AddSingleton(publisher).BuildServiceProvider();
        var tenant = Substitute.For<ITenantProvider>();
        tenant.TenantId.Returns(tenantId);
        var settings = new ClientPortalSettingsService(uow, protection);
        var changes = new ClientPortalClientChangeService(settings, tenant, services, NullLogger<ClientPortalClientChangeService>.Instance);
        return (new ClientPortalClientBackfillService(new ClientService(uow), changes, settings), settings, changes);
    }

    private static async Task<(Guid A, Guid B)> SeedTwoClientsAsync(SqliteHrmsContext db)
    {
        var a = new Client { Id = Guid.NewGuid(), Code = "000001", Name = "Alpha", Status = "Active" };
        var b = new Client { Id = Guid.NewGuid(), Code = "000002", Name = "Beta", Status = "INACTIVE" };
        await using var seed = db.NewContext();
        seed.Clients.AddRange(a, b);
        await seed.SaveChangesAsync();
        return (a.Id, b.Id);
    }

    [Fact]
    public async Task Sending_all_clients_queues_each_one_with_its_status_and_is_refused_while_the_connection_is_off()
    {
        using var db = new SqliteHrmsContext();
        var protection = new EphemeralDataProtectionProvider();
        var publisher = Substitute.For<IPublishEndpoint>();
        var tenantId = Guid.NewGuid();
        var (a, b) = await SeedTwoClientsAsync(db);

        await using (var ctx = db.NewContext())
            await FluentActions.Awaiting(() => BackfillFor(new UnitOfWorkService(ctx), protection, publisher, tenantId).Backfill.SendAllAsync(CancellationToken.None))
                .Should().ThrowAsync<ValidationException>();
        await publisher.DidNotReceiveWithAnyArgs().Publish<Onepunch.Common.Lib.DTO.ClientChanged>(default!, default);

        await EnableAsync(db, protection);
        await using (var ctx = db.NewContext())
            (await BackfillFor(new UnitOfWorkService(ctx), protection, publisher, tenantId).Backfill.SendAllAsync(CancellationToken.None)).Should().Be(2);

        await publisher.Received(1).Publish(
            Arg.Is<Onepunch.Common.Lib.DTO.ClientChanged>(m => m.ClientId == a && m.Name == "Alpha" && m.Status == "Active" && m.TenantId == tenantId && m.ChangeType == "Updated"),
            Arg.Any<CancellationToken>());
        await publisher.Received(1).Publish(
            Arg.Is<Onepunch.Common.Lib.DTO.ClientChanged>(m => m.ClientId == b && m.Status == "INACTIVE"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Switching_the_connection_on_for_the_first_time_sends_the_clients_that_already_exist_but_saving_it_again_does_not()
    {
        using var db = new SqliteHrmsContext();
        var protection = new EphemeralDataProtectionProvider();
        var publisher = Substitute.For<IPublishEndpoint>();
        await SeedTwoClientsAsync(db);
        var request = new ClientPortalSettingsRequest(true, "https://portal.example.com", "cpk_abcd.secret");

        await using (var ctx = db.NewContext())
        {
            var uow = new UnitOfWorkService(ctx);
            var (backfill, settings, _) = BackfillFor(uow, protection, publisher, Guid.NewGuid());
            var controller = new Hrms.Api.Controllers.ClientPortalController(
                settings, new ClientPortalSyncRequestService(settings, new DTRBatchService(uow), publisher), backfill);

            await controller.SaveSettings(request, CancellationToken.None);
        }
        await publisher.Received(2).Publish(Arg.Any<Onepunch.Common.Lib.DTO.ClientChanged>(), Arg.Any<CancellationToken>());

        publisher.ClearReceivedCalls();
        await using (var ctx = db.NewContext())
        {
            var uow = new UnitOfWorkService(ctx);
            var (backfill, settings, _) = BackfillFor(uow, protection, publisher, Guid.NewGuid());
            var controller = new Hrms.Api.Controllers.ClientPortalController(
                settings, new ClientPortalSyncRequestService(settings, new DTRBatchService(uow), publisher), backfill);

            await controller.SaveSettings(request with { PortalUrl = "https://other.example.com" }, CancellationToken.None);
        }
        await publisher.DidNotReceiveWithAnyArgs().Publish<Onepunch.Common.Lib.DTO.ClientChanged>(default!, default);
    }

    [Fact]
    public async Task Send_again_refuses_a_batch_that_is_not_posted()
    {
        using var db = new SqliteHrmsContext();
        var protection = new EphemeralDataProtectionProvider();
        var (batchId, _) = await SeedBatchAsync(db, posted: false);
        await EnableAsync(db, protection);

        await using var ctx = db.NewContext();
        var uow = new UnitOfWorkService(ctx);
        var requests = new ClientPortalSyncRequestService(new ClientPortalSettingsService(uow, protection), new DTRBatchService(uow), Substitute.For<IPublishEndpoint>());
        await FluentActions.Awaiting(() => requests.RequestAsync(batchId, CancellationToken.None)).Should().ThrowAsync<ValidationException>();
    }

    // ---- the HTTP client -----------------------------------------------------------------------------------------

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    private static HttpResponseMessage Json(object data, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(JsonSerializer.Serialize(new { message = "Success", status = (int)status, data }), Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Client_sends_the_key_to_the_ingest_url_and_splits_large_batches()
    {
        var handler = new RecordingHandler(_ => Json(new { accepted = 500, rejected = 0, errors = Array.Empty<object>() }));
        var client = new ClientPortalClient(new HttpClient(handler));
        var rows = Enumerable.Range(0, 1200).Select(i => new PortalClientRow($"c{i}", "C", "C")).ToList();

        var result = await client.PutAsync(new ClientPortalCredentials("https://portal.example.com/", "cpk_abcd.secret"), "clients", rows, CancellationToken.None);

        handler.Requests.Should().HaveCount(3, "500 rows per request");
        handler.Requests.Should().OnlyContain(r => r.Method == HttpMethod.Put && r.RequestUri!.ToString() == "https://portal.example.com/api/v1/ingest/clients");
        handler.Requests.Should().OnlyContain(r => r.Headers.GetValues("X-Api-Key").Single() == "cpk_abcd.secret");
        result.Accepted.Should().Be(1500);
    }

    [Fact]
    public async Task Client_throws_on_a_failed_answer_without_leaking_the_key()
    {
        var client = new ClientPortalClient(new HttpClient(new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized))));

        var act = () => client.PutAsync(new ClientPortalCredentials("https://portal.example.com", "cpk_abcd.secret"), "dtr", new[] { new PortalClientRow("a", "A", "A") }, CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<HttpRequestException>();
        thrown.Which.Message.Should().Contain("401").And.NotContain("secret");
    }

    [Fact]
    public async Task Client_reports_rows_the_portal_refused()
    {
        var client = new ClientPortalClient(new HttpClient(new RecordingHandler(_ =>
            Json(new { accepted = 0, rejected = 1, errors = new[] { new { externalId = "a", error = "Unknown guard" } } }))));

        var result = await client.PutAsync(new ClientPortalCredentials("https://portal.example.com", "k"), "dtr", new[] { new PortalClientRow("a", "A", "A") }, CancellationToken.None);

        result.Rejected.Should().Be(1);
        result.Errors.Should().ContainSingle(e => e.Error == "Unknown guard");
    }
}
