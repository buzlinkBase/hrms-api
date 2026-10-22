using Hrms.Core.Services.Approvals;
using Hrms.Core.Services.ClientPortal;
using Hrms.Domain.Entities.EmployeeEntities;
using hrms.test.TestSupport;
using Mapster;
using MapsterMapper;
using MassTransit;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Onepunch.Common.Lib.DTO;

namespace hrms.test.ServiceTests;

/// <summary>
/// Posted attendance that changes after it reached the Client Portal: unposted or deleted rows are removed there, and the posted days an
/// edited or deleted punch belongs to are sent again.
/// </summary>
public class ClientPortalAttendanceChangeTests
{
    private static readonly DateOnly Day = new(2026, 10, 1);

    private sealed record Fixture(SqliteHrmsContext Db, EphemeralDataProtectionProvider Protection, IPublishEndpoint Publisher, Guid TenantId);

    private static Fixture NewFixture() => new(new SqliteHrmsContext(), new EphemeralDataProtectionProvider(), Substitute.For<IPublishEndpoint>(), Guid.NewGuid());

    private static async Task EnableAsync(Fixture f)
    {
        await using var ctx = f.Db.NewContext();
        await new ClientPortalSettingsService(new UnitOfWorkService(ctx), f.Protection)
            .SaveAsync(new ClientPortalSettingsRequest(true, "https://portal.example.com", "cpk_abcd.secret"), CancellationToken.None);
    }

    private static ClientPortalAttendanceRemovalService Removal(UnitOfWorkService uow, Fixture f)
    {
        var tenant = Substitute.For<ITenantProvider>();
        tenant.TenantId.Returns(f.TenantId);
        var services = new ServiceCollection().AddSingleton(f.Publisher).BuildServiceProvider();
        return new ClientPortalAttendanceRemovalService(new ClientPortalSettingsService(uow, f.Protection), tenant, services, NullLogger<ClientPortalAttendanceRemovalService>.Instance);
    }

    // The approval engine gets its own bus so only the portal messages land on the fixture's.
    private static DailyRecordService DtrService(UnitOfWorkService uow, Fixture f) => new(
        uow, new TypeAdapterConfig(), Substitute.For<IMapper>(),
        Substitute.For<ILogger<DailyRecordService>>(),
        new LeaveDtrReconciliationService(uow, Substitute.For<ILogger<LeaveDtrReconciliationService>>()),
        new PayrollBatchService(uow), new DTRBatchService(uow),
        new ApprovalEngineService(uow, Substitute.For<IPublishEndpoint>()),
        Removal(uow, f));

    private static AttendanceService Attendance(UnitOfWorkService uow, Fixture f)
    {
        var sync = new ClientPortalSyncRequestService(new ClientPortalSettingsService(uow, f.Protection), new DTRBatchService(uow), f.Publisher);
        return new AttendanceService(uow, portalPunches: new ClientPortalPunchChangeService(uow, sync, NullLogger<ClientPortalPunchChangeService>.Instance));
    }

    /// <summary>A posted batch: two posted days and one unposted day for the generator. Returns the batch id and the posted row ids.</summary>
    private static async Task<(Guid BatchId, string Code, Employee Generator, Employee Approver, Guid[] Posted, Guid Unposted)> SeedAsync(Fixture f, string code)
    {
        var group = new PayrollGroup { Id = Guid.NewGuid() };
        var generator = new Employee { Id = Guid.NewGuid(), PayrollGroupId = group.Id };
        var approver = new Employee { Id = Guid.NewGuid(), PayrollGroupId = group.Id };
        var batch = new DTRBatch
        {
            Id = Guid.NewGuid(), BatchCode = code, PayPeriodStart = Day, PayPeriodEnd = Day.AddDays(14), GeneratedByEmployeeId = generator.Id,
            ApprovalStatus = ApprovalStatus.Approved, IsPosted = true, PostedAt = DateTime.UtcNow, PostedBy = generator.Id,
        };
        var posted = new[]
        {
            new DailyRecord { Id = Guid.NewGuid(), EmployeeId = generator.Id, WorkDate = Day, BatchCode = code, Posted = true },
            new DailyRecord { Id = Guid.NewGuid(), EmployeeId = generator.Id, WorkDate = Day.AddDays(1), BatchCode = code, Posted = true },
        };
        var unposted = new DailyRecord { Id = Guid.NewGuid(), EmployeeId = generator.Id, WorkDate = Day.AddDays(2), BatchCode = code, Posted = false };

        await using var seed = f.Db.NewContext();
        seed.PayrollGroups.Add(group);
        seed.Employees.AddRange(generator, approver);
        seed.DTRBatches.Add(batch);
        seed.DailyTimeRecords.AddRange(posted);
        seed.DailyTimeRecords.Add(unposted);
        await seed.SaveChangesAsync();
        return (batch.Id, code, generator, approver, posted.Select(p => p.Id).ToArray(), unposted.Id);
    }

    private static bool SameIds(IEnumerable<string> actual, IEnumerable<Guid> expected) =>
        actual.OrderBy(x => x).SequenceEqual(expected.Select(g => g.ToString("D")).OrderBy(x => x));

    // ---- removals ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Unposting_a_batch_tells_the_portal_to_drop_the_rows_that_were_posted()
    {
        var f = NewFixture();
        using var _ = f.Db;
        var (_, code, _, _, posted, _) = await SeedAsync(f, "DTR-unpost");
        await EnableAsync(f);

        await using (var ctx = f.Db.NewContext())
            await DtrService(new UnitOfWorkService(ctx), f).UnpostAsync(code, CancellationToken.None);

        await f.Publisher.Received(1).Publish(
            Arg.Is<ClientPortalAttendanceRemoved>(m => m.TenantId == f.TenantId && m.BatchCode == code && m.Reason == "Unposted" && SameIds(m.RecordExternalIds, posted)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Approving_the_deletion_of_a_posted_batch_removes_its_rows_from_the_portal()
    {
        var f = NewFixture();
        using var _ = f.Db;
        var (batchId, code, generator, approver, posted, _) = await SeedAsync(f, "DTR-delete");
        await EnableAsync(f);

        await using (var ctx = f.Db.NewContext())
            await DtrService(new UnitOfWorkService(ctx), f).RequestDeletionAsync(batchId, generator.Id, CancellationToken.None);
        await using (var ctx = f.Db.NewContext())
            await DtrService(new UnitOfWorkService(ctx), f).ApproveDeletionAsync(batchId, approver.Id, approverHasOverride: false, note: null, CancellationToken.None);

        await f.Publisher.Received(1).Publish(
            Arg.Is<ClientPortalAttendanceRemoved>(m => m.BatchCode == code && m.Reason == "BatchDeleted" && SameIds(m.RecordExternalIds, posted)),
            Arg.Any<CancellationToken>());
        await using var verify = f.Db.NewContext();
        (await verify.DailyTimeRecords.AnyAsync(r => r.BatchCode == code)).Should().BeFalse();
    }

    [Fact]
    public async Task Deleting_an_unposted_draft_sends_nothing_because_the_portal_never_had_it()
    {
        var f = NewFixture();
        using var _ = f.Db;
        var (_, code, _, _, _, _) = await SeedAsync(f, "DTR-draft");
        await using (var seed = f.Db.NewContext())
        {
            foreach (var r in seed.DailyTimeRecords.Where(r => r.BatchCode == code))
                r.Posted = false;
            await seed.SaveChangesAsync();
        }
        await EnableAsync(f);

        await using (var ctx = f.Db.NewContext())
            await DtrService(new UnitOfWorkService(ctx), f).DeleteAsync(code, CancellationToken.None);

        await f.Publisher.DidNotReceiveWithAnyArgs().Publish<ClientPortalAttendanceRemoved>(default!, default);
    }

    [Fact]
    public async Task A_leave_change_that_reopens_posted_days_removes_them_from_the_portal()
    {
        var f = NewFixture();
        using var _ = f.Db;
        var (_, code, generator, _, posted, _) = await SeedAsync(f, "DTR-leave");
        await EnableAsync(f);

        await using (var ctx = f.Db.NewContext())
        {
            var service = DtrService(new UnitOfWorkService(ctx), f);
            (await service.UnpostByDateRangeAsync(generator.Id, Day, Day, CancellationToken.None)).Should().Be(1);
            await service.CommitChangesAsync(CancellationToken.None);
        }

        var reopened = new[] { posted[0] };
        await f.Publisher.Received(1).Publish(
            Arg.Is<ClientPortalAttendanceRemoved>(m => m.Reason == "LeaveChanged" && m.BatchCode == code && SameIds(m.RecordExternalIds, reopened)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Nothing_is_sent_while_the_connection_is_off_and_a_bus_failure_never_blocks_the_unpost()
    {
        var f = NewFixture();
        using var _ = f.Db;
        var (_, code, _, _, _, _) = await SeedAsync(f, "DTR-off");

        await using (var ctx = f.Db.NewContext())
            await DtrService(new UnitOfWorkService(ctx), f).UnpostAsync(code, CancellationToken.None);
        await f.Publisher.DidNotReceiveWithAnyArgs().Publish<ClientPortalAttendanceRemoved>(default!, default);

        // Back on, but the bus is down: the unpost still happens.
        await using (var repost = f.Db.NewContext())
        {
            foreach (var r in repost.DailyTimeRecords.Where(r => r.BatchCode == code))
                r.Posted = true;
            await repost.SaveChangesAsync();
        }
        await EnableAsync(f);
        f.Publisher.Publish(Arg.Any<ClientPortalAttendanceRemoved>(), Arg.Any<CancellationToken>()).Returns(_ => throw new InvalidOperationException("bus down"));

        await using (var ctx = f.Db.NewContext())
            await DtrService(new UnitOfWorkService(ctx), f).UnpostAsync(code, CancellationToken.None);

        await using var verify = f.Db.NewContext();
        (await verify.DailyTimeRecords.AnyAsync(r => r.BatchCode == code && r.Posted)).Should().BeFalse();
    }

    [Fact]
    public async Task A_large_removal_is_split_into_parts()
    {
        var f = NewFixture();
        using var _ = f.Db;
        await EnableAsync(f);
        var ids = Enumerable.Range(0, ClientPortalAttendanceRemoved.MaxRecords + 1).Select(_ => Guid.NewGuid()).ToList();

        await using (var ctx = f.Db.NewContext())
            (await Removal(new UnitOfWorkService(ctx), f).StageAsync(ids, "b", ClientPortalAttendanceRemovalService.Unposted, CancellationToken.None)).Should().Be(2);

        await f.Publisher.Received(1).Publish(Arg.Is<ClientPortalAttendanceRemoved>(m => m.Part == 1 && m.Parts == 2 && m.RecordExternalIds.Count == 1000), Arg.Any<CancellationToken>());
        await f.Publisher.Received(1).Publish(Arg.Is<ClientPortalAttendanceRemoved>(m => m.Part == 2 && m.Parts == 2 && m.RecordExternalIds.Count == 1), Arg.Any<CancellationToken>());
    }

    // ---- punches -------------------------------------------------------------------------------------------------

    private static async Task<(Guid Employee, Guid Other, Dictionary<DateOnly, Guid> Rows, Guid Punch)> SeedPunchAsync(Fixture f)
    {
        var group = new PayrollGroup { Id = Guid.NewGuid() };
        var employee = new Employee { Id = Guid.NewGuid(), PayrollGroupId = group.Id };
        var other = new Employee { Id = Guid.NewGuid(), PayrollGroupId = group.Id };
        var rows = new Dictionary<DateOnly, Guid>();
        await using var seed = f.Db.NewContext();
        seed.PayrollGroups.Add(group);
        seed.Employees.AddRange(employee, other);
        // Posted days Sep 29 .. Oct 4 for the employee, and Oct 1 for someone else.
        for (var d = Day.AddDays(-2); d <= Day.AddDays(3); d = d.AddDays(1))
        {
            var row = new DailyRecord { Id = Guid.NewGuid(), EmployeeId = employee.Id, WorkDate = d, BatchCode = "DTR-p", Posted = true };
            seed.DailyTimeRecords.Add(row);
            rows[d] = row.Id;
        }
        seed.DailyTimeRecords.Add(new DailyRecord { Id = Guid.NewGuid(), EmployeeId = other.Id, WorkDate = Day, BatchCode = "DTR-p", Posted = true });
        var punch = new Attendance { Id = Guid.NewGuid(), EmployeeId = employee.Id, WorkDateTime = Day.ToDateTime(new TimeOnly(6, 0)), LogSource = LOGSOURCE.MANUAL, BatchCode = "MANUAL-1" };
        seed.Attendances.Add(punch);
        await seed.SaveChangesAsync();
        return (employee.Id, other.Id, rows, punch.Id);
    }

    [Fact]
    public async Task Deleting_a_punch_resends_the_employees_posted_days_around_it_only()
    {
        var f = NewFixture();
        using var _ = f.Db;
        var (_, _, rows, punch) = await SeedPunchAsync(f);
        await EnableAsync(f);

        await using (var ctx = f.Db.NewContext())
        {
            var service = Attendance(new UnitOfWorkService(ctx), f);
            await service.Remove(punch, AttendanceLogCaller.System, null, CancellationToken.None);
            await service.CommitChangesAsync(CancellationToken.None);
        }

        var expected = new[] { rows[Day.AddDays(-1)], rows[Day], rows[Day.AddDays(1)] };
        await f.Publisher.Received(1).Publish(
            Arg.Is<ClientPortalSyncRequested>(m => m.Reason == "PunchChanged" && m.DailyRecordIds != null && m.DailyRecordIds.OrderBy(x => x).SequenceEqual(expected.OrderBy(x => x))),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Moving_a_punch_to_another_day_resends_both_the_old_and_the_new_days()
    {
        var f = NewFixture();
        using var _ = f.Db;
        var (_, _, rows, punch) = await SeedPunchAsync(f);
        await EnableAsync(f);

        await using (var ctx = f.Db.NewContext())
        {
            var service = Attendance(new UnitOfWorkService(ctx), f);
            await service.Update(new UpdateAttendance { Id = punch, WorkTime = Day.AddDays(3).ToDateTime(new TimeOnly(6, 0)), Remarks = "wrong day" }, AttendanceLogCaller.System, CancellationToken.None);
            await service.CommitChangesAsync(CancellationToken.None);
        }

        // Old day Oct 1 -> Sep 30..Oct 2; new day Oct 4 -> Oct 3..Oct 4 (Oct 5 has no row).
        await f.Publisher.Received(1).Publish(
            Arg.Is<ClientPortalSyncRequested>(m => m.DailyRecordIds != null && m.DailyRecordIds.Count == 5 && !m.DailyRecordIds.Contains(rows[Day.AddDays(-2)])),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Punch_changes_send_nothing_while_the_connection_is_off()
    {
        var f = NewFixture();
        using var _ = f.Db;
        var (_, _, _, punch) = await SeedPunchAsync(f);

        await using (var ctx = f.Db.NewContext())
        {
            var service = Attendance(new UnitOfWorkService(ctx), f);
            await service.RemoveBatch("MANUAL-1", CancellationToken.None);
            await service.CommitChangesAsync(CancellationToken.None);
        }

        await f.Publisher.DidNotReceiveWithAnyArgs().Publish<ClientPortalSyncRequested>(default!, default);
        await using var verify = f.Db.NewContext();
        (await verify.Attendances.AnyAsync(a => a.Id == punch)).Should().BeFalse();
    }
}
