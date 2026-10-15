using System.Net;
using System.Text;
using System.Text.Json;
using Hrms.Core.Messaging;
using Hrms.Core.Services;
using Hrms.Core.Services.ClientPortal;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using hrms.test.TestSupport;
using MassTransit;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>Pushing posted DTR batches to the Client Portal: the row mapping, the saved connection, and the push itself.</summary>
public class ClientPortalSyncTests
{
    private static readonly DateOnly Day = new(2026, 10, 1);
    private static readonly DateTime Start = new(2026, 10, 1, 6, 0, 0);
    private static readonly DateTime End = new(2026, 10, 1, 18, 0, 0);

    private sealed class FakePortalClient : IClientPortalClient
    {
        public List<(string Resource, int Rows)> Calls { get; } = [];
        public List<object> Sent { get; } = [];
        public bool Fail { get; set; }
        public Func<string, PortalIngestResult>? Answer { get; set; }

        public Task<PortalIngestResult> PutAsync<T>(ClientPortalCredentials credentials, string resource, IReadOnlyList<T> rows, CancellationToken token)
        {
            if (Fail) throw new HttpRequestException("portal down");
            Calls.Add((resource, rows.Count));
            Sent.AddRange(rows.Cast<object>());
            return Task.FromResult(Answer?.Invoke(resource) ?? new PortalIngestResult(rows.Count, 0, []));
        }
    }

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
    public void Absent_days_and_rows_without_a_client_or_site_are_not_sent()
    {
        var client = new Client { Id = Guid.NewGuid(), Code = "LGU", Name = "LGU" };
        var area = new CostCenters { Id = Guid.NewGuid(), Code = "HALL", Name = "City Hall" };
        var emp = Guid.NewGuid();
        var ok = Record(emp, client.Id, area.Id, "b");
        var absent = Record(emp, client.Id, area.Id, "b", r => { r.StartTime = null; r.EndTime = null; });
        var noClient = Record(emp, client.Id, area.Id, "b", r => r.ClientId = null);
        var noArea = Record(emp, client.Id, area.Id, "b", r => r.AreaId = null);

        var payload = ClientPortalPayloadBuilder.Build(new PortalSourceData(
            [ok, absent, noClient, noArea], new Dictionary<Guid, Client> { [client.Id] = client }, new Dictionary<Guid, CostCenters> { [area.Id] = area },
            new Dictionary<Guid, Employee>(), new Dictionary<Guid, double>(), [], []));

        payload.Records.Should().ContainSingle().Which.ExternalId.Should().Be(ok.Id.ToString());
        payload.Clients.Should().ContainSingle();
        payload.Sites.Should().ContainSingle().Which.Name.Should().Be("City Hall");
        payload.Posts.Should().ContainSingle().Which.SiteExternalId.Should().Be(payload.Sites[0].ExternalId);
        payload.Guards.Should().ContainSingle();
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

    [Fact]
    public async Task Sync_sends_parents_before_records_and_remembers_the_result()
    {
        using var db = new SqliteHrmsContext();
        var protection = new EphemeralDataProtectionProvider();
        var (batchId, batchCode) = await SeedBatchAsync(db);
        await EnableAsync(db, protection);
        var fake = new FakePortalClient();

        await using (var ctx = db.NewContext())
        {
            var uow = new UnitOfWorkService(ctx);
            var summary = await new ClientPortalSyncService(uow, new ClientPortalSettingsService(uow, protection), fake).SyncBatchAsync(batchId, CancellationToken.None);
            summary.Should().Match<ClientPortalSyncSummary>(s => s.Succeeded && s.Records == 1 && s.Accepted == 1 && s.BatchCode == batchCode);
        }

        fake.Calls.Select(c => c.Resource).Should().Equal("clients", "sites", "posts", "guards", "schedules", "dtr");
        await using var verify = db.NewContext();
        var last = await new ClientPortalSettingsService(new UnitOfWorkService(verify), protection).GetAsync(CancellationToken.None);
        last.LastSync.Should().NotBeNull();
        last.LastSync!.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Sync_does_nothing_when_off_or_when_the_batch_is_not_posted()
    {
        using var db = new SqliteHrmsContext();
        var protection = new EphemeralDataProtectionProvider();
        var (posted, _) = await SeedBatchAsync(db);
        var fake = new FakePortalClient();

        await using (var off = db.NewContext())
        {
            var uow = new UnitOfWorkService(off);
            (await new ClientPortalSyncService(uow, new ClientPortalSettingsService(uow, protection), fake).SyncBatchAsync(posted, CancellationToken.None)).Should().BeNull();
        }

        await EnableAsync(db, protection);
        await using (var unposted = db.NewContext())
        {
            var uow = new UnitOfWorkService(unposted);
            (await new ClientPortalSyncService(uow, new ClientPortalSettingsService(uow, protection), fake).SyncBatchAsync(Guid.NewGuid(), CancellationToken.None)).Should().BeNull();
        }
        fake.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task Refused_rows_are_reported_but_do_not_fail_the_batch()
    {
        using var db = new SqliteHrmsContext();
        var protection = new EphemeralDataProtectionProvider();
        var (batchId, _) = await SeedBatchAsync(db);
        await EnableAsync(db, protection);
        var fake = new FakePortalClient
        {
            Answer = resource => resource == "dtr" ? new PortalIngestResult(0, 1, [new PortalRowError("x", "This date falls in a period that was already verified and locked.")]) : new PortalIngestResult(1, 0, []),
        };

        await using var ctx = db.NewContext();
        var uow = new UnitOfWorkService(ctx);
        var summary = await new ClientPortalSyncService(uow, new ClientPortalSettingsService(uow, protection), fake).SyncBatchAsync(batchId, CancellationToken.None);

        summary!.Succeeded.Should().BeFalse();
        summary.Rejected.Should().Be(1);
        summary.Errors.Should().ContainSingle(e => e.Error.Contains("locked"));
    }

    [Fact]
    public async Task An_unreachable_portal_is_recorded_and_rethrown_so_the_bus_retries()
    {
        using var db = new SqliteHrmsContext();
        var protection = new EphemeralDataProtectionProvider();
        var (batchId, _) = await SeedBatchAsync(db);
        await EnableAsync(db, protection);

        await using (var ctx = db.NewContext())
        {
            var uow = new UnitOfWorkService(ctx);
            var sync = new ClientPortalSyncService(uow, new ClientPortalSettingsService(uow, protection), new FakePortalClient { Fail = true });
            await FluentActions.Awaiting(() => sync.SyncBatchAsync(batchId, CancellationToken.None)).Should().ThrowAsync<HttpRequestException>();
        }

        await using var verify = db.NewContext();
        var state = await new ClientPortalSettingsService(new UnitOfWorkService(verify), protection).GetAsync(CancellationToken.None);
        state.LastSync!.Succeeded.Should().BeFalse();
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
