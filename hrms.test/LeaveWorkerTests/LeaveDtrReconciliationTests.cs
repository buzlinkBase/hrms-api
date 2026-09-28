using Hrms.Core.Services.Approvals;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Infrastructure;
using hrms.test.TestSupport;
using Mapster;
using MapsterMapper;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace hrms.test.LeaveWorkerTests;

/// <summary>
/// Leave ledger vs DTR: posting charges approved leave days (in days), unposting or deleting a
/// DTR gives back exactly what that batch charged, and cancelling/deleting the leave reverses
/// every batch once. Real SQLite ledger rows, so balances and entries are checked end to end.
/// </summary>
public class LeaveDtrReconciliationTests
{
    private const string BatchA = "DTR-A";
    private const string BatchB = "DTR-B";
    private static readonly DateOnly Jan5 = new(2026, 1, 5);
    private static readonly DateOnly Jan6 = new(2026, 1, 6);

    private sealed record Seeded(Guid EmployeeId, Guid ApproverId, Guid LeaveId, Guid CreditsId, Guid ApplicationId);

    // A 2-day approved leave (Jan 5-6) with its approval-time reservation, 10 days granted.
    // Each batch gets one 8-hour leave day per date given.
    private static async Task<Seeded> SeedAsync(SqliteHrmsContext db, params (string Batch, DateOnly Date)[] dtrDays)
    {
        await using var seed = db.NewContext();
        var payrollGroupId = Guid.NewGuid();
        seed.PayrollGroups.Add(new PayrollGroup { Id = payrollGroupId });
        var employee = new Employee { Id = Guid.NewGuid(), PayrollGroupId = payrollGroupId };
        var approver = new Employee { Id = Guid.NewGuid(), PayrollGroupId = payrollGroupId };
        seed.Employees.AddRange(employee, approver);

        var leave = new Leave { Id = Guid.NewGuid(), Code = "VL", Description = "Vacation Leave" };
        seed.Leaves.Add(leave);
        var credits = new LeaveCredits
        {
            Id = Guid.NewGuid(), EmployeeId = employee.Id, LeaveId = leave.Id, PeriodYear = 2026,
            FromDate = new DateTime(2026, 1, 1), ToDate = new DateTime(2026, 12, 31),
            Granted = 10m, Balance = 10m, Reserved = 2m,
        };
        seed.LeaveCredits.Add(credits);
        var app = new LeaveApplication
        {
            Id = Guid.NewGuid(), EmployeeId = employee.Id, LeaveId = leave.Id, Leave = null!,
            DurationType = DurationType.MultiDay, LeaveDateFrom = Jan5, LeaveDateTo = Jan6,
            ApprovalStatus = ApprovalStatus.Approved,
        };
        seed.leaveApplications.Add(app);
        seed.LeaveLedgers.Add(new LeaveLedger
        {
            Id = Guid.NewGuid(), EmployeeId = employee.Id, LeaveId = leave.Id, LeaveCreditsId = credits.Id,
            EntryType = LedgerEntryType.Reserved, EntryDate = Jan5, Less = 2m, Balance = 10m,
            ReferenceApplicationId = app.Id, Particulars = "Leave approved",
        });

        foreach (var (batch, date) in dtrDays)
        {
            seed.DailyTimeRecords.Add(new DailyRecord
            {
                Id = Guid.NewGuid(), EmployeeId = employee.Id, WorkDate = date, BatchCode = batch,
                ShiftWorkingHour = 8, PaidLeaveHours = 8,
                LeavesInfo = [new DtrLeaveMetaData { Id = Guid.NewGuid(), LeaveId = leave.Id, Hours = 8, Name = "Vacation Leave" }],
            });
        }

        await seed.SaveChangesAsync();
        return new Seeded(employee.Id, approver.Id, leave.Id, credits.Id, app.Id);
    }

    private static DailyRecordService DtrService(IUnitOfWorkService uow) => new(
        uow, new TypeAdapterConfig(), Substitute.For<IMapper>(),
        Substitute.For<ILogger<DailyRecordService>>(),
        Reconciliation(uow),
        new PayrollBatchService(uow), new DTRBatchService(uow),
        new ApprovalEngineService(uow, Substitute.For<IPublishEndpoint>()));

    private static LeaveDtrReconciliationService Reconciliation(IUnitOfWorkService uow) =>
        new(uow, Substitute.For<ILogger<LeaveDtrReconciliationService>>());

    private static async Task RunAsync(SqliteHrmsContext db, Func<IUnitOfWorkService, Task> action)
    {
        await using var context = db.NewContext();
        await action(new UnitOfWorkService(context));
    }

    private static async Task<LeaveCredits> CreditsAsync(SqliteHrmsContext db, Seeded s)
    {
        await using var verify = db.NewContext();
        return await verify.LeaveCredits.AsNoTracking().SingleAsync(x => x.Id == s.CreditsId);
    }

    // The ledger's own running sum must agree with LeaveCredits (the seed has no Grant entry,
    // so the entries alone should total -(Used + Reserved)).
    private static async Task AssertLedgerMatchesCreditsAsync(SqliteHrmsContext db, Seeded s)
    {
        await using var verify = db.NewContext();
        var entries = await verify.LeaveLedgers.AsNoTracking().Where(x => x.LeaveCreditsId == s.CreditsId).ToListAsync();
        var credits = await verify.LeaveCredits.AsNoTracking().SingleAsync(x => x.Id == s.CreditsId);
        entries.Sum(x => x.Add - x.Less).Should().Be(-(credits.Used + credits.Reserved));
    }

    [Fact]
    public async Task Posting_ChargesDays_NotHours_AndReleasesTheReservation()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db, (BatchA, Jan5), (BatchA, Jan6));

        await RunAsync(db, uow => DtrService(uow).PostAsync(BatchA, CancellationToken.None));

        var credits = await CreditsAsync(db, s);
        credits.Used.Should().Be(2m, "two 8-hour leave days are 2 days, not 16");
        credits.Balance.Should().Be(8m);
        credits.Reserved.Should().Be(0m);
        await AssertLedgerMatchesCreditsAsync(db, s);
    }

    [Fact]
    public async Task Posting_SkipsLeaveThatIsNoLongerApproved()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db, (BatchA, Jan5), (BatchA, Jan6));
        await using (var edit = db.NewContext())
        {
            var app = await edit.leaveApplications.SingleAsync(x => x.Id == s.ApplicationId);
            app.ApprovalStatus = ApprovalStatus.Cancelled;
            await edit.SaveChangesAsync();
        }

        await RunAsync(db, uow => DtrService(uow).PostAsync(BatchA, CancellationToken.None));

        var credits = await CreditsAsync(db, s);
        credits.Used.Should().Be(0m);
        credits.Balance.Should().Be(10m);
    }

    [Fact]
    public async Task Posting_SkipsDeletedLeave()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db, (BatchA, Jan5));
        await using (var edit = db.NewContext())
        {
            var app = await edit.leaveApplications.SingleAsync(x => x.Id == s.ApplicationId);
            app.DeletedAt = DateTime.UtcNow;
            await edit.SaveChangesAsync();
        }

        await RunAsync(db, uow => DtrService(uow).PostAsync(BatchA, CancellationToken.None));

        (await CreditsAsync(db, s)).Used.Should().Be(0m);
    }

    [Fact]
    public async Task Unposting_GivesTheDaysBack_AsAReservation()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db, (BatchA, Jan5), (BatchA, Jan6));
        await RunAsync(db, uow => DtrService(uow).PostAsync(BatchA, CancellationToken.None));

        await RunAsync(db, uow => DtrService(uow).UnpostAsync(BatchA, CancellationToken.None));

        var credits = await CreditsAsync(db, s);
        credits.Used.Should().Be(0m);
        credits.Balance.Should().Be(10m);
        credits.Reserved.Should().Be(2m, "the leave is still approved, waiting for a DTR again");
        await AssertLedgerMatchesCreditsAsync(db, s);
    }

    [Fact]
    public async Task RepostingAfterUnpost_ChargesAgain()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db, (BatchA, Jan5), (BatchA, Jan6));
        await RunAsync(db, uow => DtrService(uow).PostAsync(BatchA, CancellationToken.None));
        await RunAsync(db, uow => DtrService(uow).UnpostAsync(BatchA, CancellationToken.None));

        await RunAsync(db, uow => DtrService(uow).PostAsync(BatchA, CancellationToken.None));

        var credits = await CreditsAsync(db, s);
        credits.Used.Should().Be(2m);
        credits.Reserved.Should().Be(0m);
        await AssertLedgerMatchesCreditsAsync(db, s);
    }

    [Fact]
    public async Task LeaveAcrossTwoBatches_EachBatchReversesOnlyItsOwnDays()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db, (BatchA, Jan5), (BatchB, Jan6));
        await RunAsync(db, uow => DtrService(uow).PostAsync(BatchA, CancellationToken.None));

        var afterA = await CreditsAsync(db, s);
        afterA.Used.Should().Be(1m);
        afterA.Reserved.Should().Be(1m, "the second day's hold stays until its own batch posts");

        await RunAsync(db, uow => DtrService(uow).PostAsync(BatchB, CancellationToken.None));
        (await CreditsAsync(db, s)).Used.Should().Be(2m);

        await RunAsync(db, uow => DtrService(uow).UnpostAsync(BatchA, CancellationToken.None));
        (await CreditsAsync(db, s)).Used.Should().Be(1m);

        await RunAsync(db, uow => DtrService(uow).UnpostAsync(BatchB, CancellationToken.None));
        var credits = await CreditsAsync(db, s);
        credits.Used.Should().Be(0m);
        credits.Balance.Should().Be(10m);
        credits.Reserved.Should().Be(2m);
        await AssertLedgerMatchesCreditsAsync(db, s);
    }

    [Fact]
    public async Task DeletingAPostedDtrBatch_RollsBackItsLeaveCharge()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db, (BatchA, Jan5), (BatchA, Jan6));
        var batchId = Guid.NewGuid();
        await using (var seed = db.NewContext())
        {
            seed.DTRBatches.Add(new DTRBatch
            {
                Id = batchId, BatchCode = BatchA, PayPeriodStart = Jan5, PayPeriodEnd = Jan6,
                GeneratedByEmployeeId = s.EmployeeId, ApprovalStatus = ApprovalStatus.Approved, IsPosted = true,
            });
            await seed.SaveChangesAsync();
        }
        await RunAsync(db, uow => DtrService(uow).PostAsync(BatchA, CancellationToken.None));
        await RunAsync(db, uow => DtrService(uow).RequestDeletionAsync(batchId, s.EmployeeId, CancellationToken.None));

        await RunAsync(db, uow => DtrService(uow).ApproveDeletionAsync(batchId, s.ApproverId, approverHasOverride: false, note: null, CancellationToken.None));

        await using (var verify = db.NewContext())
            (await verify.DailyTimeRecords.AnyAsync(x => x.BatchCode == BatchA)).Should().BeFalse();
        var credits = await CreditsAsync(db, s);
        credits.Used.Should().Be(0m, "a deleted DTR must not keep charging leave");
        credits.Balance.Should().Be(10m);
        credits.Reserved.Should().Be(2m);
        await AssertLedgerMatchesCreditsAsync(db, s);
    }

    [Fact]
    public async Task DeletingAnApprovedLeave_AfterTwoBatchesPosted_ReversesEachBatchOnce()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db, (BatchA, Jan5), (BatchB, Jan6));
        await RunAsync(db, uow => DtrService(uow).PostAsync(BatchA, CancellationToken.None));
        await RunAsync(db, uow => DtrService(uow).PostAsync(BatchB, CancellationToken.None));

        await RunAsync(db, async uow =>
        {
            var leaveService = new LeaveApplicationService(
                uow, TypeAdapterConfig.GlobalSettings, Substitute.For<IMapper>(), Substitute.For<IPublishEndpoint>(),
                Substitute.For<ILogger<LeaveApplicationService>>(), DtrService(uow),
                new ApprovalEngineService(uow, Substitute.For<IPublishEndpoint>()), Reconciliation(uow));
            await leaveService.Delete(s.ApplicationId, CancellationToken.None);
            await leaveService.CommitChangesAsync(CancellationToken.None);
        });

        var credits = await CreditsAsync(db, s);
        credits.Used.Should().Be(0m);
        credits.Balance.Should().Be(10m);
        credits.Reserved.Should().Be(0m, "a deleted leave holds nothing");

        await using (var verify = db.NewContext())
        {
            var reversals = await verify.LeaveLedgers.AsNoTracking()
                .Where(x => x.ReferenceApplicationId == s.ApplicationId && x.EntryType == LedgerEntryType.Reversal)
                .ToListAsync();
            reversals.Select(x => x.DtrBatchCode).Should().BeEquivalentTo([BatchA, BatchB]);
            (await verify.DailyTimeRecords.AnyAsync(x => x.Posted)).Should().BeFalse("its DTR days are unposted to be re-run without it");
        }

        // A later unpost of either batch has nothing left to reverse.
        await RunAsync(db, uow => Reconciliation(uow).ReverseConsumptionAsync(BatchA, CancellationToken.None));
        (await CreditsAsync(db, s)).Balance.Should().Be(10m);
        await AssertLedgerMatchesCreditsAsync(db, s);
    }

    [Fact]
    public async Task Repair_FixesDeductionsPostedInHours_AndIsSafeToRerun()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db, (BatchA, Jan5));
        // Simulate the old bug: Jan 5's 8 leave hours were deducted as 8 days.
        await using (var seed = db.NewContext())
        {
            var dtr = await seed.DailyTimeRecords.SingleAsync(x => x.BatchCode == BatchA);
            dtr.Posted = true;
            var credits = await seed.LeaveCredits.SingleAsync(x => x.Id == s.CreditsId);
            credits.Used = 8m;
            credits.Balance = 2m;
            credits.Reserved = 0m;
            seed.LeaveLedgers.AddRange(
                new LeaveLedger
                {
                    Id = Guid.NewGuid(), EmployeeId = s.EmployeeId, LeaveId = s.LeaveId, LeaveCreditsId = s.CreditsId,
                    EntryType = LedgerEntryType.Released, EntryDate = Jan5, Add = 2m, Balance = 10m,
                    ReferenceApplicationId = s.ApplicationId, DtrBatchCode = BatchA, Particulars = "released",
                },
                new LeaveLedger
                {
                    Id = Guid.NewGuid(), EmployeeId = s.EmployeeId, LeaveId = s.LeaveId, LeaveCreditsId = s.CreditsId,
                    EntryType = LedgerEntryType.Deduction, EntryDate = Jan5, Less = 8m, Balance = 2m,
                    ReferenceApplicationId = s.ApplicationId, DtrBatchCode = BatchA, Particulars = "old deduction",
                });
            await seed.SaveChangesAsync();
        }

        List<LeaveDtrRepairLine> preview = [];
        await RunAsync(db, async uow => preview = await Reconciliation(uow).RepairPostedDeductionsAsync(2026, dryRun: true, CancellationToken.None));
        preview.Should().ContainSingle().Which.Delta.Should().Be(-7m);
        (await CreditsAsync(db, s)).Balance.Should().Be(2m, "a dry run writes nothing");

        await RunAsync(db, uow => Reconciliation(uow).RepairPostedDeductionsAsync(2026, dryRun: false, CancellationToken.None));
        var repaired = await CreditsAsync(db, s);
        repaired.Used.Should().Be(1m);
        repaired.Balance.Should().Be(9m);

        List<LeaveDtrRepairLine> rerun = [];
        await RunAsync(db, async uow => rerun = await Reconciliation(uow).RepairPostedDeductionsAsync(2026, dryRun: false, CancellationToken.None));
        rerun.Should().ContainSingle().Which.Delta.Should().Be(0m);
        (await CreditsAsync(db, s)).Balance.Should().Be(9m);
    }

    [Fact]
    public async Task Repair_RollsBackTheLeftoverChargeOfAnAlreadyDeletedBatch()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db); // no DTR rows -- the batch is already gone
        await using (var seed = db.NewContext())
        {
            var credits = await seed.LeaveCredits.SingleAsync(x => x.Id == s.CreditsId);
            credits.Used = 1m;
            credits.Balance = 9m;
            seed.LeaveLedgers.Add(new LeaveLedger
            {
                Id = Guid.NewGuid(), EmployeeId = s.EmployeeId, LeaveId = s.LeaveId, LeaveCreditsId = s.CreditsId,
                EntryType = LedgerEntryType.Deduction, EntryDate = Jan5, Less = 1m, Balance = 9m,
                ReferenceApplicationId = s.ApplicationId, DtrBatchCode = "DTR-deleted", Particulars = "orphan",
            });
            await seed.SaveChangesAsync();
        }

        await RunAsync(db, uow => Reconciliation(uow).RepairPostedDeductionsAsync(2026, dryRun: false, CancellationToken.None));

        var repaired = await CreditsAsync(db, s);
        repaired.Used.Should().Be(0m);
        repaired.Balance.Should().Be(10m);
    }
}
