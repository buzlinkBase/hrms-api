using Hrms.Core.Extensions;
using Hrms.Core.Hubs;
using Hrms.Core.Messaging.Announcements;
using Hrms.Core.Services.Announcements.Lifecycle;
using MassTransit;
using Hrms.Core.Services.Announcements;
using Hrms.Core.Services.Announcements.Audience;
using Hrms.Core.Services.Approvals;
using Hrms.Domain.Entities.Announcements;
using Hrms.Domain.Entities.EmployeeEntities;
using hrms.test.TestSupport;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>
/// Company announcements end to end against real SQLite: audience snapshot at publish, the
/// employee's saved copy (views/acknowledgements), the publisher's report, and every lifecycle
/// action. Each step runs in its own context/unit of work, like separate requests.
/// </summary>
public class AnnouncementServiceTests : IDisposable
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly AnnouncementActor Publisher = new(Guid.NewGuid(), "Olivia Owner");

    private readonly SqliteHrmsContext _db = new();
    private readonly IHubContext<NotificationHub> _hub = Substitute.For<IHubContext<NotificationHub>>();
    private readonly FakeJobScheduler _jobs = new();

    // Ops: Ana (email), Ben (no login at all), Dan (resigned). HR: Cara (linked UserId).
    private Guid _ops, _hr, _manager, _branch, _ana, _ben, _cara, _dan;

    public AnnouncementServiceTests()
    {
        SeedOrganisation();
    }

    public void Dispose() => _db.Dispose();

    private void SeedOrganisation()
    {
        using var seed = _db.NewContext();
        var payrollGroup = new PayrollGroup { Id = Guid.NewGuid() };
        seed.PayrollGroups.Add(payrollGroup);
        var ops = new Department { Id = Guid.NewGuid(), Code = "OPS", Name = "Operations" };
        var hr = new Department { Id = Guid.NewGuid(), Code = "HR", Name = "Human Resources" };
        seed.Departments.AddRange(ops, hr);
        var manager = new Position { Id = Guid.NewGuid(), Name = "Manager" };
        seed.Positions.Add(manager);
        var branch = new Branch { Id = Guid.NewGuid(), Code = "MAIN", Name = "Main" };
        seed.Branches.Add(branch);

        Employee Emp(string first, Guid dept, string? email = null, Guid? userId = null,
            EmploymentStatus status = EmploymentStatus.Regular, Guid? positionId = null) => new()
        {
            Id = Guid.NewGuid(),
            FirstName = first,
            LastName = "Tester",
            EmployeeNo = first.ToUpperInvariant(),
            PayrollGroupId = payrollGroup.Id,
            DepartmentId = dept,
            PositionId = positionId,
            BranchId = branch.Id,
            Email = email,
            UserId = userId,
            EmploymentStatus = status,
        };

        var ana = Emp("Ana", ops.Id, email: "ana@company.com", positionId: manager.Id);
        var ben = Emp("Ben", ops.Id);
        var cara = Emp("Cara", hr.Id, userId: Guid.NewGuid());
        var dan = Emp("Dan", ops.Id, email: "dan@company.com", status: EmploymentStatus.Resigned);
        seed.Employees.AddRange(ana, ben, cara, dan);
        seed.SaveChanges();

        (_ops, _hr, _manager, _branch) = (ops.Id, hr.Id, manager.Id, branch.Id);
        (_ana, _ben, _cara, _dan) = (ana.Id, ben.Id, cara.Id, dan.Id);
    }

    private static readonly IAudienceRuleStrategy[] Strategies =
    [
        new AllEmployeesAudienceRule(), new DepartmentAudienceRule(), new PositionAudienceRule(),
        new BranchAudienceRule(), new ProjectSiteAudienceRule(), new ClientAudienceRule(),
        new SectionAudienceRule(), new EmploymentStatusAudienceRule(), new SpecificEmployeeAudienceRule(),
    ];

    private async Task<T> Announcements<T>(Func<AnnouncementService, Task<T>> action)
    {
        await using var context = _db.NewContext();
        var tenantProvider = Substitute.For<ITenantProvider>();
        tenantProvider.TenantId.Returns(TenantId);
        var service = new AnnouncementService(
            new UnitOfWorkService(context),
            new AudienceResolver(Strategies),
            new AnnouncementPushService(_hub, tenantProvider),
            _jobs,
            NullLogger<AnnouncementService>.Instance);
        return await action(service);
    }

    private Task RunWorkerAsync(AnnouncementLifecycleDue message) =>
        Announcements(async s =>
        {
            var context = Substitute.For<ConsumeContext<AnnouncementLifecycleDue>>();
            context.Message.Returns(message);
            await new AnnouncementLifecycleWorker(s).Consume(context);
            return true;
        });

    // Stands in for Quartz: records what was booked and cancelled.
    private sealed class FakeJobScheduler : IAnnouncementJobScheduler
    {
        public List<(Guid Token, Guid AnnouncementId, AnnouncementLifecycleWork Work, DateTime DueUtc)> Scheduled { get; } = [];
        public List<Guid> Cancelled { get; } = [];
        public IEnumerable<(Guid Token, Guid AnnouncementId, AnnouncementLifecycleWork Work, DateTime DueUtc)> Active =>
            Scheduled.Where(j => !Cancelled.Contains(j.Token));

        public Task<Guid> ScheduleAsync(Guid announcementId, AnnouncementLifecycleWork work, DateTime dueUtc, CancellationToken token)
        {
            var jobToken = Guid.NewGuid();
            Scheduled.Add((jobToken, announcementId, work, dueUtc));
            return Task.FromResult(jobToken);
        }

        public Task CancelAsync(Guid jobToken, CancellationToken token)
        {
            Cancelled.Add(jobToken);
            return Task.CompletedTask;
        }
    }

    private Task Announcements(Func<AnnouncementService, Task> action) =>
        Announcements(async s => { await action(s); return true; });

    private async Task<T> Mine<T>(Func<MyAnnouncementService, Task<T>> action)
    {
        await using var context = _db.NewContext();
        return await action(new MyAnnouncementService(new UnitOfWorkService(context)));
    }

    private async Task<T> Reports<T>(Func<AnnouncementReportService, Task<T>> action)
    {
        await using var context = _db.NewContext();
        return await action(new AnnouncementReportService(new UnitOfWorkService(context)));
    }

    private static AudienceRuleRequest Rule(AudienceRuleKind kind, Guid? target = null, bool exclude = false, string? value = null) =>
        new() { Kind = kind, TargetId = target, TargetValue = value, TargetLabel = kind.ToString(), IsExclusion = exclude };

    private static AnnouncementUpsertRequest Draft(params AudienceRuleRequest[] rules) => new()
    {
        Title = "Office closed Friday",
        Body = "The office is closed this Friday.\nSee https://intranet/notice for details.",
        AudienceRules = rules.ToList(),
    };

    private async Task<Guid> PublishedAsync(AnnouncementUpsertRequest request)
    {
        var id = await Announcements(s => s.CreateDraftAsync(request, Publisher, CancellationToken.None));
        await Announcements(s => s.PublishNowAsync(id, Publisher, CancellationToken.None));
        return id;
    }

    private async Task UpdateRowAsync(Guid id, Action<Announcement> change)
    {
        await using var context = _db.NewContext();
        var announcement = await context.Announcements.SingleAsync(a => a.Id == id);
        change(announcement);
        await context.SaveChangesAsync();
    }

    // ── Audience & publishing ────────────────────────────────────────────────

    [Fact]
    public async Task Publish_SnapshotsTheAudience_WithExclusionsApplied_AndLeaversLeftOut()
    {
        var id = await PublishedAsync(Draft(
            Rule(AudienceRuleKind.Department, _ops),
            Rule(AudienceRuleKind.Employee, _ben, exclude: true)));

        await using var verify = _db.NewContext();
        var recipients = await verify.AnnouncementRecipients.Where(r => r.AnnouncementId == id).ToListAsync();
        recipients.Should().ContainSingle("Ben is excluded and Dan has resigned");
        var ana = recipients.Single();
        ana.EmployeeId.Should().Be(_ana);
        ana.DepartmentName.Should().Be("Operations");
        ana.PositionName.Should().Be("Manager");
        ana.BranchName.Should().Be("Main");
        ana.EmployeeName.Should().Be("Ana Tester");
        (await verify.Announcements.SingleAsync(a => a.Id == id)).State.Should().Be(AnnouncementState.Published);
    }

    [Fact]
    public async Task Preview_MatchesWhatPublishWouldSend()
    {
        var preview = await Announcements(s => s.PreviewAudienceAsync(new AudiencePreviewRequest
        {
            AudienceRules = [Rule(AudienceRuleKind.AllEmployees), Rule(AudienceRuleKind.Department, _hr, exclude: true)],
        }, CancellationToken.None));

        preview.Total.Should().Be(2, "Ana and Ben -- Cara is in the excluded HR department, Dan has resigned");
        preview.Sample.Should().BeEquivalentTo("Ana Tester", "Ben Tester");
        preview.ByDepartment.Should().ContainSingle(d => d.Name == "Operations" && d.Count == 2);
    }

    [Fact]
    public async Task Preview_CanIncludeLeavers_WhenAskedTo()
    {
        var preview = await Announcements(s => s.PreviewAudienceAsync(new AudiencePreviewRequest
        {
            AudienceRules = [Rule(AudienceRuleKind.EmploymentStatus, value: "Resigned")],
            ExcludeInactive = false,
        }, CancellationToken.None));

        preview.Total.Should().Be(1);
        preview.Sample.Should().Equal("Dan Tester");
    }

    [Fact]
    public async Task PublishTwice_DoesNotDuplicateRecipients()
    {
        var id = await PublishedAsync(Draft(Rule(AudienceRuleKind.AllEmployees)));

        var again = await Announcements(s => s.PublishNowAsync(id, Publisher, CancellationToken.None));

        again.Should().BeFalse();
        await using var verify = _db.NewContext();
        (await verify.AnnouncementRecipients.CountAsync(r => r.AnnouncementId == id)).Should().Be(3);
    }

    [Fact]
    public async Task ManualPublish_RefusesAnEmptyAudience()
    {
        var id = await Announcements(s => s.CreateDraftAsync(Draft(Rule(AudienceRuleKind.Position, Guid.NewGuid())), Publisher, CancellationToken.None));

        var act = () => Announcements(s => s.PublishNowAsync(id, Publisher, CancellationToken.None));

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Publish_PushesLiveToEachRecipient()
    {
        var group = Substitute.For<IClientProxy>();
        _hub.Clients.Groups(Arg.Any<IReadOnlyList<string>>()).Returns(group);
        var users = Substitute.For<IClientProxy>();
        _hub.Clients.Users(Arg.Any<IReadOnlyList<string>>()).Returns(users);

        await PublishedAsync(Draft(Rule(AudienceRuleKind.AllEmployees)));

        // Ana by her login email, Cara by her linked account; Ben has neither.
        _hub.Clients.Received().Groups(Arg.Is<IReadOnlyList<string>>(g => g.SequenceEqual(new[] { ApprovalRecipientGroups.For(TenantId, "ana@company.com") })));
        _hub.Clients.Received().Users(Arg.Is<IReadOnlyList<string>>(u => u.Count == 1));
        await group.Received(1).SendCoreAsync(AnnouncementPushService.EventName, Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }

    // ── Scheduling ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Schedule_SendsNothingYet_AndUnscheduleReturnsToDraft()
    {
        var id = await Announcements(s => s.CreateDraftAsync(Draft(Rule(AudienceRuleKind.AllEmployees)), Publisher, CancellationToken.None));

        await Announcements(s => s.ScheduleAsync(id, DateTime.UtcNow.AddHours(2), CancellationToken.None));

        await using (var verify = _db.NewContext())
        {
            (await verify.Announcements.SingleAsync(a => a.Id == id)).State.Should().Be(AnnouncementState.Scheduled);
            (await verify.AnnouncementRecipients.AnyAsync(r => r.AnnouncementId == id)).Should().BeFalse();
        }
        (await Mine(m => m.GetUnreadCountAsync(_ana, CancellationToken.None))).Should().Be(0);

        await Announcements(s => s.UnscheduleAsync(id, CancellationToken.None));
        await using var after = _db.NewContext();
        var unscheduled = await after.Announcements.SingleAsync(a => a.Id == id);
        unscheduled.State.Should().Be(AnnouncementState.Draft);
        unscheduled.PublishAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task Schedule_InThePast_IsRejected()
    {
        var id = await Announcements(s => s.CreateDraftAsync(Draft(Rule(AudienceRuleKind.AllEmployees)), Publisher, CancellationToken.None));

        var act = () => Announcements(s => s.ScheduleAsync(id, DateTime.UtcNow.AddMinutes(-1), CancellationToken.None));

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Schedule_BooksAQuartzPublishJob_AndUnscheduleCancelsIt()
    {
        var id = await Announcements(s => s.CreateDraftAsync(Draft(Rule(AudienceRuleKind.AllEmployees)), Publisher, CancellationToken.None));
        var publishAt = DateTime.UtcNow.AddHours(2);

        await Announcements(s => s.ScheduleAsync(id, publishAt, CancellationToken.None));

        var job = _jobs.Active.Should().ContainSingle().Subject;
        job.AnnouncementId.Should().Be(id);
        job.Work.Should().Be(AnnouncementLifecycleWork.Publish);
        job.DueUtc.Should().Be(publishAt);
        await using (var verify = _db.NewContext())
            (await verify.Announcements.SingleAsync(a => a.Id == id)).PublishJobToken.Should().Be(job.Token);

        await Announcements(s => s.UnscheduleAsync(id, CancellationToken.None));

        _jobs.Cancelled.Should().Contain(job.Token);
        _jobs.Active.Should().BeEmpty();
    }

    [Fact]
    public async Task Rescheduling_ReplacesThePendingPublishJob()
    {
        var id = await Announcements(s => s.CreateDraftAsync(Draft(Rule(AudienceRuleKind.AllEmployees)), Publisher, CancellationToken.None));
        await Announcements(s => s.ScheduleAsync(id, DateTime.UtcNow.AddHours(2), CancellationToken.None));
        await Announcements(s => s.UnscheduleAsync(id, CancellationToken.None));
        var later = DateTime.UtcNow.AddDays(1);

        await Announcements(s => s.ScheduleAsync(id, later, CancellationToken.None));

        _jobs.Active.Should().ContainSingle(j => j.Work == AnnouncementLifecycleWork.Publish && j.DueUtc == later);
    }

    [Fact]
    public async Task DuePublishJob_AutoPublishes_ForTheWholeAudienceAtThatMoment()
    {
        var id = await Announcements(s => s.CreateDraftAsync(Draft(Rule(AudienceRuleKind.Department, _ops)), Publisher, CancellationToken.None));
        await Announcements(s => s.ScheduleAsync(id, DateTime.UtcNow.AddHours(1), CancellationToken.None));
        await UpdateRowAsync(id, a => a.PublishAtUtc = DateTime.UtcNow.AddSeconds(-5)); // time passes

        await RunWorkerAsync(new AnnouncementLifecycleDue(id, AnnouncementLifecycleWork.Publish));

        await using var verify = _db.NewContext();
        var announcement = await verify.Announcements.SingleAsync(a => a.Id == id);
        announcement.State.Should().Be(AnnouncementState.Published);
        announcement.PublishedByName.Should().Be("System");
        announcement.PublishJobToken.Should().BeNull();
        (await verify.AnnouncementRecipients.CountAsync(r => r.AnnouncementId == id)).Should().Be(2);
    }

    [Fact]
    public async Task StalePublishJob_ForAnEarlierTime_IsIgnored()
    {
        var id = await Announcements(s => s.CreateDraftAsync(Draft(Rule(AudienceRuleKind.AllEmployees)), Publisher, CancellationToken.None));
        await Announcements(s => s.ScheduleAsync(id, DateTime.UtcNow.AddHours(3), CancellationToken.None));

        var published = await Announcements(s => s.PublishScheduledAsync(id, CancellationToken.None));

        published.Should().BeFalse();
        (await Announcements(s => s.GetAsync(id, CancellationToken.None))).State.Should().Be(AnnouncementState.Scheduled);
    }

    [Fact]
    public async Task PublishingAScheduledOneByHand_CancelsItsJob_AndBooksTheExpiry()
    {
        var request = Draft(Rule(AudienceRuleKind.AllEmployees));
        request.ExpiresAtUtc = DateTime.UtcNow.AddDays(7);
        var id = await Announcements(s => s.CreateDraftAsync(request, Publisher, CancellationToken.None));
        await Announcements(s => s.ScheduleAsync(id, DateTime.UtcNow.AddDays(1), CancellationToken.None));
        var publishJob = _jobs.Active.Single().Token;

        await Announcements(s => s.PublishNowAsync(id, Publisher, CancellationToken.None));

        _jobs.Cancelled.Should().Contain(publishJob);
        var expire = _jobs.Active.Should().ContainSingle().Subject;
        expire.Work.Should().Be(AnnouncementLifecycleWork.Expire);
        expire.DueUtc.Should().BeCloseTo(request.ExpiresAtUtc.Value, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ExpiryJob_FollowsEditsUnpublishAndRepublish()
    {
        var id = await PublishedAsync(Draft(Rule(AudienceRuleKind.AllEmployees)));
        _jobs.Active.Should().BeEmpty("no expiry yet");

        var request = Draft(Rule(AudienceRuleKind.AllEmployees));
        request.ExpiresAtUtc = DateTime.UtcNow.AddDays(3);
        await Announcements(s => s.UpdateAsync(id, request, CancellationToken.None));
        _jobs.Active.Should().ContainSingle(j => j.Work == AnnouncementLifecycleWork.Expire);

        await Announcements(s => s.UnpublishAsync(id, null, Publisher, CancellationToken.None));
        _jobs.Active.Should().BeEmpty("an unpublished announcement can't expire");

        await Announcements(s => s.RepublishAsync(id, CancellationToken.None));
        _jobs.Active.Should().ContainSingle(j => j.Work == AnnouncementLifecycleWork.Expire);

        await Announcements(s => s.ArchiveAsync(id, CancellationToken.None));
        _jobs.Active.Should().BeEmpty();
    }

    [Fact]
    public async Task DeletingASentOne_BooksThePurge_AndRestoringCancelsIt()
    {
        var id = await PublishedAsync(Draft(Rule(AudienceRuleKind.AllEmployees)));

        await Announcements(s => s.DeleteAsync(id, CancellationToken.None));

        var purge = _jobs.Active.Should().ContainSingle().Subject;
        purge.Work.Should().Be(AnnouncementLifecycleWork.Purge);
        purge.DueUtc.Should().BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromMinutes(1));

        await Announcements(s => s.RestoreDeletedAsync(id, CancellationToken.None));

        _jobs.Cancelled.Should().Contain(purge.Token);
        _jobs.Active.Should().BeEmpty();
    }

    [Fact]
    public async Task PurgeJob_RemovesTheAnnouncementOnceDue()
    {
        var id = await PublishedAsync(Draft(Rule(AudienceRuleKind.AllEmployees)));
        await Announcements(s => s.DeleteAsync(id, CancellationToken.None));
        await UpdateRowAsync(id, a => a.BinnedAtUtc = DateTime.UtcNow.AddDays(-30).AddMinutes(-1));

        await RunWorkerAsync(new AnnouncementLifecycleDue(id, AnnouncementLifecycleWork.Purge));
        await RunWorkerAsync(new AnnouncementLifecycleDue(id, AnnouncementLifecycleWork.Purge)); // a repeat is harmless

        await using var verify = _db.NewContext();
        (await verify.Announcements.IgnoreQueryFilters().AnyAsync(a => a.Id == id)).Should().BeFalse();
    }

    // ── Employee copy, views and acknowledgement ────────────────────────────

    [Fact]
    public async Task Open_RecordsTheView_AndTheCopyStaysAfterReading()
    {
        var id = await PublishedAsync(Draft(Rule(AudienceRuleKind.AllEmployees)));
        (await Mine(m => m.GetUnreadCountAsync(_ana, CancellationToken.None))).Should().Be(1);

        var opened = await Mine(m => m.OpenAsync(_ana, id, CancellationToken.None));
        await Mine(m => m.OpenAsync(_ana, id, CancellationToken.None));

        opened.Body.Should().Contain("closed this Friday");
        (await Mine(m => m.GetUnreadCountAsync(_ana, CancellationToken.None))).Should().Be(0);
        var mine = await Mine(m => m.GetMineAsync(_ana, new MyAnnouncementQuery(), CancellationToken.None));
        mine.Data.Should().ContainSingle(x => x.Id == id && x.IsRead);

        await using var verify = _db.NewContext();
        var receipt = await verify.AnnouncementRecipients.SingleAsync(r => r.AnnouncementId == id && r.EmployeeId == _ana);
        receipt.ViewCount.Should().Be(2);
        receipt.FirstViewedAt.Should().NotBeNull();
    }

    // Serialized without a UTC marker, the browser would read these as local time.
    [Fact]
    public async Task Timestamps_ComeBackMarkedAsUtc()
    {
        var id = await PublishedAsync(Draft(Rule(AudienceRuleKind.AllEmployees)));

        var opened = await Mine(m => m.OpenAsync(_ana, id, CancellationToken.None));
        var listed = (await Mine(m => m.GetMineAsync(_ana, new MyAnnouncementQuery(), CancellationToken.None))).Data!.Single();
        var detail = await Announcements(s => s.GetAsync(id, CancellationToken.None));
        var receipt = (await Reports(r => r.GetRecipientsAsync(id, new RecipientListQuery(), CancellationToken.None))).Data!.First(x => x.EmployeeId == _ana);

        opened.PublishedAtUtc!.Value.Kind.Should().Be(DateTimeKind.Utc);
        listed.PublishedAtUtc!.Value.Kind.Should().Be(DateTimeKind.Utc);
        detail.PublishedAtUtc!.Value.Kind.Should().Be(DateTimeKind.Utc);
        detail.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
        receipt.FirstViewedAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task Open_SomeoneElsesAnnouncement_IsNotFound()
    {
        var id = await PublishedAsync(Draft(Rule(AudienceRuleKind.Department, _hr)));

        var act = () => Mine(m => m.OpenAsync(_ana, id, CancellationToken.None));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Acknowledge_OnlyWhenRequired_AndOnlyOnce()
    {
        var plain = await PublishedAsync(Draft(Rule(AudienceRuleKind.AllEmployees)));
        var notAsked = () => Mine(async m => { await m.AcknowledgeAsync(_ana, plain, CancellationToken.None); return true; });
        await notAsked.Should().ThrowAsync<ValidationException>();

        var request = Draft(Rule(AudienceRuleKind.AllEmployees));
        request.RequiresAcknowledgement = true;
        var required = await PublishedAsync(request);
        (await Mine(m => m.GetPendingAcknowledgementsAsync(_ana, CancellationToken.None))).Should().ContainSingle(x => x.Id == required);

        await Mine(async m => { await m.AcknowledgeAsync(_ana, required, CancellationToken.None); return true; });
        var twice = () => Mine(async m => { await m.AcknowledgeAsync(_ana, required, CancellationToken.None); return true; });

        await twice.Should().ThrowAsync<ValidationException>();
        (await Mine(m => m.GetPendingAcknowledgementsAsync(_ana, CancellationToken.None))).Should().BeEmpty();
    }

    [Fact]
    public async Task EditAfterPublish_BumpsVersion_AndReacknowledgementKeepsTheOldRecord()
    {
        var request = Draft(Rule(AudienceRuleKind.AllEmployees));
        request.RequiresAcknowledgement = true;
        var id = await PublishedAsync(request);
        await Mine(async m => { await m.AcknowledgeAsync(_ana, id, CancellationToken.None); return true; });

        request.Body = "Correction: the office is closed on Thursday.";
        request.RequireReacknowledgement = true;
        await Announcements(s => s.UpdateAsync(id, request, CancellationToken.None));

        var detail = await Announcements(s => s.GetAsync(id, CancellationToken.None));
        detail.Version.Should().Be(2);
        detail.AcknowledgedCount.Should().Be(0, "everyone must acknowledge the new version");

        var ana = (await Reports(r => r.GetRecipientsAsync(id, new RecipientListQuery(), CancellationToken.None)))
            .Data!.Single(x => x.EmployeeId == _ana);
        ana.IsAcknowledged.Should().BeFalse();
        ana.AcknowledgedVersion.Should().Be(1, "the earlier acknowledgement stays on record");

        var item = (await Mine(m => m.GetMineAsync(_ana, new MyAnnouncementQuery(), CancellationToken.None))).Data!.Single();
        item.IsEdited.Should().BeTrue();
        item.NeedsReacknowledgement.Should().BeTrue();
    }

    [Fact]
    public async Task EditAfterPublish_KeepsTheAudienceAsSent()
    {
        var request = Draft(Rule(AudienceRuleKind.Department, _hr));
        var id = await PublishedAsync(request);

        request.AudienceRules = [Rule(AudienceRuleKind.AllEmployees)];
        await Announcements(s => s.UpdateAsync(id, request, CancellationToken.None));

        var detail = await Announcements(s => s.GetAsync(id, CancellationToken.None));
        detail.AudienceRules.Should().ContainSingle(r => r.Kind == AudienceRuleKind.Department);
        detail.RecipientCount.Should().Be(1);
    }

    // ── Unpublish / archive / delete ────────────────────────────────────────

    [Fact]
    public async Task Unpublish_HidesIt_WithAWithdrawnNotice_AndRepublishKeepsTheReceipts()
    {
        var id = await PublishedAsync(Draft(Rule(AudienceRuleKind.AllEmployees)));
        await Mine(m => m.OpenAsync(_ana, id, CancellationToken.None));

        await Announcements(s => s.UnpublishAsync(id, "Sent to the wrong group", Publisher, CancellationToken.None));

        (await Mine(m => m.GetMineAsync(_ana, new MyAnnouncementQuery(), CancellationToken.None))).Data.Should().BeEmpty();
        var notice = await Mine(m => m.OpenAsync(_ana, id, CancellationToken.None));
        notice.IsWithdrawn.Should().BeTrue();
        notice.Body.Should().BeEmpty();
        notice.WithdrawReason.Should().Be("Sent to the wrong group");
        notice.WithdrawnByName.Should().Be("Olivia Owner");

        await Announcements(s => s.RepublishAsync(id, CancellationToken.None));

        var mine = await Mine(m => m.GetMineAsync(_ana, new MyAnnouncementQuery(), CancellationToken.None));
        mine.Data.Should().ContainSingle(x => x.Id == id && x.IsRead, "the earlier view is kept");
        await using var verify = _db.NewContext();
        (await verify.AnnouncementRecipients.SingleAsync(r => r.AnnouncementId == id && r.EmployeeId == _ana))
            .ViewCount.Should().Be(1, "opening the withdrawn notice isn't a view");
    }

    [Fact]
    public async Task Archive_MovesItToTheEmployeesArchive_ReadOnly_AndRestoreBringsItBack()
    {
        var request = Draft(Rule(AudienceRuleKind.AllEmployees));
        request.RequiresAcknowledgement = true;
        var id = await PublishedAsync(request);

        await Announcements(s => s.ArchiveAsync(id, CancellationToken.None));

        (await Mine(m => m.GetMineAsync(_ana, new MyAnnouncementQuery(), CancellationToken.None))).Data.Should().BeEmpty();
        var archived = await Mine(m => m.GetMineAsync(_ana, new MyAnnouncementQuery { Filter = MyAnnouncementFilter.Archived }, CancellationToken.None));
        archived.Data.Should().ContainSingle(x => x.Id == id && x.IsArchived && !x.NeedsAcknowledgement);
        var acknowledge = () => Mine(async m => { await m.AcknowledgeAsync(_ana, id, CancellationToken.None); return true; });
        await acknowledge.Should().ThrowAsync<ValidationException>();

        await Announcements(s => s.RestoreAsync(id, CancellationToken.None));
        (await Mine(m => m.GetMineAsync(_ana, new MyAnnouncementQuery(), CancellationToken.None))).Data.Should().ContainSingle();
    }

    [Fact]
    public async Task Expire_ArchivesAPublishedAnnouncementPastItsExpiry()
    {
        var id = await PublishedAsync(Draft(Rule(AudienceRuleKind.AllEmployees)));
        (await Announcements(s => s.ExpireAsync(id, CancellationToken.None))).Should().BeFalse("it has no expiry yet");

        await UpdateRowAsync(id, a => a.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1));
        (await Mine(m => m.GetUnreadCountAsync(_ana, CancellationToken.None))).Should().Be(0, "expired ones already leave the feed");

        (await Announcements(s => s.ExpireAsync(id, CancellationToken.None))).Should().BeTrue();
        (await Announcements(s => s.GetAsync(id, CancellationToken.None))).State.Should().Be(AnnouncementState.Archived);
    }

    [Fact]
    public async Task Delete_ADraft_RemovesItForGood()
    {
        var id = await Announcements(s => s.CreateDraftAsync(Draft(Rule(AudienceRuleKind.AllEmployees)), Publisher, CancellationToken.None));

        await Announcements(s => s.DeleteAsync(id, CancellationToken.None));

        await using var verify = _db.NewContext();
        (await verify.Announcements.IgnoreQueryFilters().AnyAsync(a => a.Id == id)).Should().BeFalse();
        (await verify.AnnouncementAudienceRules.IgnoreQueryFilters().AnyAsync(r => r.AnnouncementId == id)).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_ASentOne_GoesToTheBin_AndCanBeRestored()
    {
        var id = await PublishedAsync(Draft(Rule(AudienceRuleKind.AllEmployees)));

        await Announcements(s => s.DeleteAsync(id, CancellationToken.None));

        var binned = await Announcements(s => s.GetAsync(id, CancellationToken.None));
        binned.State.Should().Be(AnnouncementState.Deleted);
        binned.PurgeAtUtc.Should().NotBeNull();
        var open = () => Mine(m => m.OpenAsync(_ana, id, CancellationToken.None));
        await open.Should().ThrowAsync<NotFoundException>();
        var deletedTab = await Announcements(s => s.ListAsync(new AnnouncementListQuery { Tab = AnnouncementListTab.Deleted }, CancellationToken.None));
        deletedTab.Data.Should().ContainSingle(x => x.Id == id);

        await Announcements(s => s.RestoreDeletedAsync(id, CancellationToken.None));

        (await Announcements(s => s.GetAsync(id, CancellationToken.None))).State.Should().Be(AnnouncementState.Published);
        (await Mine(m => m.GetMineAsync(_ana, new MyAnnouncementQuery(), CancellationToken.None))).Data.Should().ContainSingle();
    }

    [Fact]
    public async Task Purge_RemovesABinnedAnnouncementPastRetention_WithItsReceipts()
    {
        var id = await PublishedAsync(Draft(Rule(AudienceRuleKind.AllEmployees)));
        await Announcements(s => s.DeleteAsync(id, CancellationToken.None));
        (await Announcements(s => s.PurgeAsync(id, CancellationToken.None))).Should().BeFalse("still within retention");

        await UpdateRowAsync(id, a => a.BinnedAtUtc = DateTime.UtcNow.AddDays(-31));
        (await Announcements(s => s.PurgeAsync(id, CancellationToken.None))).Should().BeTrue();

        await using var verify = _db.NewContext();
        (await verify.Announcements.IgnoreQueryFilters().AnyAsync(a => a.Id == id)).Should().BeFalse();
        (await verify.AnnouncementRecipients.IgnoreQueryFilters().AnyAsync(r => r.AnnouncementId == id)).Should().BeFalse();
    }

    [Fact]
    public async Task Duplicate_CopiesContentAndAudience_IntoANewDraft()
    {
        var id = await PublishedAsync(Draft(Rule(AudienceRuleKind.Department, _ops), Rule(AudienceRuleKind.Employee, _ben, exclude: true)));

        var copyId = await Announcements(s => s.DuplicateAsync(id, Publisher, CancellationToken.None));

        var copy = await Announcements(s => s.GetAsync(copyId, CancellationToken.None));
        copy.State.Should().Be(AnnouncementState.Draft);
        copy.Title.Should().Be("Office closed Friday (copy)");
        copy.AudienceRules.Should().HaveCount(2);
        copy.RecipientCount.Should().Be(0);
    }

    // ── Reminders, pinning, reports ─────────────────────────────────────────

    [Fact]
    public async Task Remind_NotViewed_TargetsOnlyThoseWhoHaveNotOpenedIt()
    {
        var id = await PublishedAsync(Draft(Rule(AudienceRuleKind.AllEmployees)));
        await Mine(m => m.OpenAsync(_ana, id, CancellationToken.None));

        var reminded = await Announcements(s => s.RemindAsync(id, RemindTarget.NotViewed, CancellationToken.None));

        reminded.Should().Be(2, "Ben and Cara haven't opened it");
        await using var verify = _db.NewContext();
        var receipts = await verify.AnnouncementRecipients.Where(r => r.AnnouncementId == id).ToListAsync();
        receipts.Single(r => r.EmployeeId == _ana).RemindCount.Should().Be(0);
        receipts.Where(r => r.EmployeeId != _ana).Should().OnlyContain(r => r.RemindCount == 1 && r.LastRemindedAt != null);
    }

    [Fact]
    public async Task PinnedAnnouncements_ComeFirst_InTheEmployeesFeed()
    {
        var older = Draft(Rule(AudienceRuleKind.AllEmployees));
        older.Title = "Pinned policy";
        older.IsPinned = true;
        var pinnedId = await PublishedAsync(older);
        var newer = Draft(Rule(AudienceRuleKind.AllEmployees));
        newer.Title = "Newer notice";
        await PublishedAsync(newer);

        var feed = await Mine(m => m.GetMineAsync(_ana, new MyAnnouncementQuery(), CancellationToken.None));

        feed.Data!.First().Id.Should().Be(pinnedId);
        feed.Data!.First().IsPinned.Should().BeTrue();
    }

    [Fact]
    public async Task Stats_AndRecipientFilters_ShowWhoViewedAndAcknowledged()
    {
        var request = Draft(Rule(AudienceRuleKind.AllEmployees));
        request.RequiresAcknowledgement = true;
        request.AcknowledgeByUtc = DateTime.UtcNow.AddDays(1);
        var id = await PublishedAsync(request);
        await Mine(m => m.OpenAsync(_ana, id, CancellationToken.None));
        await Mine(async m => { await m.AcknowledgeAsync(_ana, id, CancellationToken.None); return true; });
        await Mine(m => m.OpenAsync(_cara, id, CancellationToken.None));

        var stats = await Reports(r => r.GetStatsAsync(id, CancellationToken.None));

        stats.Reach.Should().Be(3);
        stats.Viewed.Should().Be(2);
        stats.NotViewed.Should().Be(1);
        stats.ViewRate.Should().Be(66.7);
        stats.Acknowledged.Should().Be(1);
        stats.PendingAcknowledgement.Should().Be(2);
        stats.Overdue.Should().Be(0, "the due date hasn't passed");
        stats.Unreachable.Should().Be(1, "Ben has no login and no email");
        stats.ByDepartment.Should().ContainEquivalentOf(new BreakdownRow { Name = "Operations", Total = 2, Viewed = 1, Acknowledged = 1 });
        stats.ViewsPerDay.Should().ContainSingle().Which.Count.Should().Be(2);

        var notViewed = await Reports(r => r.GetRecipientsAsync(id, new RecipientListQuery { Status = RecipientStatusFilter.NotViewed }, CancellationToken.None));
        notViewed.Data.Should().ContainSingle(x => x.EmployeeId == _ben && !x.IsReachable);
        var notAcknowledged = await Reports(r => r.GetRecipientsAsync(id, new RecipientListQuery { Status = RecipientStatusFilter.NotAcknowledged }, CancellationToken.None));
        notAcknowledged.Data!.Select(x => x.EmployeeId).Should().BeEquivalentTo(new[] { _ben, _cara });

        await UpdateRowAsync(id, a => a.AcknowledgeByUtc = DateTime.UtcNow.AddMinutes(-1));
        (await Reports(r => r.GetStatsAsync(id, CancellationToken.None))).Overdue.Should().Be(2);
        (await Mine(m => m.GetMineAsync(_cara, new MyAnnouncementQuery(), CancellationToken.None))).Data!.Single().IsOverdue.Should().BeTrue();
    }

    [Fact]
    public async Task List_ShowsCountsAndTheActionsTheStateAllows()
    {
        var id = await PublishedAsync(Draft(Rule(AudienceRuleKind.Department, _ops)));
        await Mine(m => m.OpenAsync(_ana, id, CancellationToken.None));

        var list = await Announcements(s => s.ListAsync(new AnnouncementListQuery(), CancellationToken.None));

        var row = list.Data!.Single();
        row.RecipientCount.Should().Be(2);
        row.ViewedCount.Should().Be(1);
        row.AudienceSummary.Should().Equal("Department");
        row.AllowedActions.Should().Contain(Hrms.Core.Services.Announcements.Lifecycle.AnnouncementAction.Unpublish)
            .And.NotContain(Hrms.Core.Services.Announcements.Lifecycle.AnnouncementAction.Schedule);
    }

    [Fact]
    public void EveryAudienceRuleKind_HasARegisteredStrategy()
    {
        var services = new ServiceCollection();
        LibServicesRegistrations.AddAnnouncementAudience(services);
        using var provider = services.BuildServiceProvider();

        provider.GetServices<IAudienceRuleStrategy>().Select(s => s.Kind)
            .Should().BeEquivalentTo(Enum.GetValues<AudienceRuleKind>());
        provider.GetRequiredService<AudienceResolver>().Should().NotBeNull();
    }
}
