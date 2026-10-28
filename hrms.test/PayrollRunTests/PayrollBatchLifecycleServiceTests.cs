using Onepunch.Common.Lib.Exceptions;
using Hrms.Core.Services;
using Hrms.Core.Services.Approvals;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.Approvals;
using Hrms.Domain.Entities.EmployeeEntities;
using hrms.test.TestSupport;
using Mapster;
using MapsterMapper;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace hrms.test.PayrollRunTests;

/// <summary>
/// PayrollBatchLifecycleService.PostBatchAsync used to run PayrollBatchService.PostAsync and
/// PayrollService.PostBatchAsync as two independently-committed writes. If the second one threw,
/// the first one's commit (PayrollBatch.IsPosted = true) had already landed durably -- so a
/// retry would see the batch as already posted (PayrollBatchService.PostAsync's own
/// `if (batch.IsPosted) return;` guard) and only re-run the second half, which is exactly what
/// made a failed Post look like it needed "posting twice" to actually succeed. These tests
/// exercise the fix: the whole sequence now runs inside one DB transaction.
///
/// PostBatchAsync itself is gone -- posting a batch now only ever happens as the terminal step of
/// ApproveBatchAsync, once the shared ApprovalApplicationType.PayrollPosting approval instance
/// (started at Generate time -- see PayrollProcessorService.GenerateAsync) resolves to Approved.
/// See ApproveBatchAsync_* below for the transaction-safety coverage that used to live on
/// PostBatchAsync directly, plus new coverage for Decline and the approval-engine gating itself.
///
/// Needs a REAL relational provider (SQLite ":memory:", via SqliteHrmsContext) rather than this
/// project's usual EF InMemory/NSubstitute-mocked-repository style, since only a relational
/// provider supports Database.BeginTransactionAsync/CommitAsync/RollbackAsync -- the exact thing
/// under test.
/// </summary>
public class PayrollBatchLifecycleServiceTests
{
    private static PayrollBatchLifecycleService BuildService(IUnitOfWorkService uow)
    {
        var mapper = Substitute.For<IMapper>();
        var config = new TypeAdapterConfig();

        var payrollBatchService = new PayrollBatchService(uow);
        var payrollService = new PayrollService(uow, mapper);

        var employeeService = new EmployeeService(
            uow, mapper, config,
            new DepartmentService(uow),
            new PayrollGroupService(uow),
            new BranchService(uow),
            new CostCenterService(uow),
            new PositionService(uow),
            new SectionService(uow));

        var sssService = new SSSContributionService(uow, config, mapper, employeeService);
        var phicService = new PHICContributionService(uow, config, mapper, employeeService);
        var hdmfService = new HDMFContributionService(uow, mapper, config, employeeService);
        var taxService = new TaxContributionService(uow, config, mapper, employeeService);

        var consumptionService = new PayrollInputConsumptionService(uow);
        var yearLockService = new YearLockService(uow);
        var approvalEngine = new ApprovalEngineService(uow, Substitute.For<IPublishEndpoint>());
        var dtrBatchService = new DTRBatchService(uow);

        var dtrService = new DailyRecordService(
            uow, config, mapper,
            Substitute.For<ILogger<DailyRecordService>>(),
            new LeaveDtrReconciliationService(uow, Substitute.For<ILogger<LeaveDtrReconciliationService>>()),
            payrollBatchService, dtrBatchService, approvalEngine);

        return new PayrollBatchLifecycleService(
            uow, payrollBatchService, payrollService, sssService, phicService, hdmfService, taxService,
            dtrService, consumptionService, yearLockService, approvalEngine, dtrBatchService,
            new PayrollDeductionClaimService(uow));
    }

    [Fact]
    public async Task ApproveBatchAsync_PostsBatchAndPayrolls_WhenEverythingSucceeds()
    {
        using var db = new SqliteHrmsContext();
        var batchId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var generatedById = Guid.NewGuid();
        var approverId = Guid.NewGuid();

        // Seeded through a separate, already-committed context -- exactly like a real Generate
        // Payroll request that ran (and committed) before this separate Approve request's own
        // IUnitOfWorkService/ambient transaction even started. See SqliteHrmsContext's doc
        // comment for why this can't go through the same IUnitOfWorkService the test exercises
        // below.
        await using (var seed = db.NewContext())
        {
            var payrollGroupId = Guid.NewGuid();
            seed.PayrollGroups.Add(new PayrollGroup { Id = payrollGroupId });
            seed.Employees.Add(new Employee { Id = generatedById, PayrollGroupId = payrollGroupId });
            seed.Employees.Add(new Employee { Id = approverId, PayrollGroupId = payrollGroupId });
            seed.PayrollBatches.Add(new PayrollBatch
            {
                Id = batchId,
                PayPeriodStart = new DateOnly(2026, 1, 1),
                PayPeriodEnd = new DateOnly(2026, 1, 15),
                GeneratedByEmployeeId = generatedById,
                ApprovalStatus = ApprovalStatus.ForApproval,
            });
            seed.Payrolls.Add(new Payroll
            {
                Id = Guid.NewGuid(),
                PayrollBatchId = batchId,
                EmployeeId = employeeId,
            });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var service = BuildService(uow);
        // No ApprovalWorkflow configured for PayrollPosting -- implicit single-approval fallback,
        // same as every other application type. Any employee (here, one distinct from the
        // applicant) is an eligible approver under the fallback.
        await service.ApproveBatchAsync(batchId, approverId, approverHasOverride: false, note: null, CancellationToken.None);

        await using var verify = db.NewContext();
        var batch = await verify.PayrollBatches.FindAsync(batchId);
        batch!.IsPosted.Should().BeTrue("the batch header should be posted once the approval instance resolves");
        batch.ApprovalStatus.Should().Be(ApprovalStatus.Approved);
        batch.PostedBy.Should().Be(approverId, "PostedBy is repurposed to record the final approver");
        var payroll = await verify.Payrolls.FirstAsync(x => x.PayrollBatchId == batchId);
        payroll.IsPosted.Should().BeTrue("each child payroll row should also be flagged posted");
        payroll.ApprovalStatus.Should().Be(ApprovalStatus.Approved);
    }

    [Fact]
    public async Task ApproveBatchAsync_RollsBackEverything_WhenTheSecondStageFails()
    {
        using var db = new SqliteHrmsContext();
        var batchId = Guid.NewGuid();
        var generatedById = Guid.NewGuid();
        var approverId = Guid.NewGuid();

        // Deliberately no Payroll rows for this batch -- PayrollService.PostBatchAsync throws
        // ValidationException("Payroll batch not found.") for an empty batch, simulating any
        // failure in the second stage (a real DbUpdateException from a unique-index race would
        // hit the same catch/rollback path; this is just the simplest reliable way to trigger it
        // without contriving a genuine constraint violation). Seeded through a separate,
        // already-committed context -- see the comment in the test above.
        await using (var seed = db.NewContext())
        {
            var payrollGroupId = Guid.NewGuid();
            seed.PayrollGroups.Add(new PayrollGroup { Id = payrollGroupId });
            seed.Employees.Add(new Employee { Id = generatedById, PayrollGroupId = payrollGroupId });
            seed.Employees.Add(new Employee { Id = approverId, PayrollGroupId = payrollGroupId });
            seed.PayrollBatches.Add(new PayrollBatch
            {
                Id = batchId,
                PayPeriodStart = new DateOnly(2026, 1, 1),
                PayPeriodEnd = new DateOnly(2026, 1, 15),
                GeneratedByEmployeeId = generatedById,
                ApprovalStatus = ApprovalStatus.ForApproval,
            });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var service = BuildService(uow);
        var act = () => service.ApproveBatchAsync(batchId, approverId, approverHasOverride: false, note: null, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();

        // The critical assertion: neither the batch's own commit (IsPosted = true, ApprovalStatus
        // = Approved) nor the ApprovalAction/ApprovalInstance writes RecordActionAsync made must
        // have survived -- before the transaction fix (originally proven on PostBatchAsync), this
        // would incorrectly read true/Approved, leaving the batch durably "posted" despite the
        // overall operation having failed.
        await using var verify = db.NewContext();
        var batch = await verify.PayrollBatches.FindAsync(batchId);
        batch!.IsPosted.Should().BeFalse(
            "the first stage's commit must roll back too when the second stage fails, " +
            "or a retry would silently skip re-posting the batch header (the 'must post twice' bug)");
        batch.ApprovalStatus.Should().Be(ApprovalStatus.ForApproval,
            "the approval action itself must roll back too, not just the posting side effects");
        (await verify.ApprovalInstances.AnyAsync(x => x.ApplicationId == batchId)).Should().BeFalse(
            "the ApprovalInstance StartAsync self-healed into existence for this action must roll back with everything else");
    }

    [Fact]
    public async Task ApproveBatchAsync_ThrowsInvalidOperationException_WhenBatchIsNotAwaitingApproval()
    {
        using var db = new SqliteHrmsContext();
        var batchId = Guid.NewGuid();
        var generatedById = Guid.NewGuid();
        var approverId = Guid.NewGuid();

        await using (var seed = db.NewContext())
        {
            var payrollGroupId = Guid.NewGuid();
            seed.PayrollGroups.Add(new PayrollGroup { Id = payrollGroupId });
            seed.Employees.Add(new Employee { Id = generatedById, PayrollGroupId = payrollGroupId });
            seed.Employees.Add(new Employee { Id = approverId, PayrollGroupId = payrollGroupId });
            seed.PayrollBatches.Add(new PayrollBatch
            {
                Id = batchId,
                PayPeriodStart = new DateOnly(2026, 1, 1),
                PayPeriodEnd = new DateOnly(2026, 1, 15),
                GeneratedByEmployeeId = generatedById,
                // Already resolved -- e.g. a second click on a batch already approved/declined.
                ApprovalStatus = ApprovalStatus.Approved,
            });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var service = BuildService(uow);
        var act = () => service.ApproveBatchAsync(batchId, approverId, approverHasOverride: false, note: null, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ApproveBatchAsync_ThrowsForbiddenException_WhenApproverIsIneligible()
    {
        using var db = new SqliteHrmsContext();
        var batchId = Guid.NewGuid();
        var generatedById = Guid.NewGuid();
        var designatedApproverId = Guid.NewGuid();
        var ineligibleApproverId = Guid.NewGuid();

        await using (var seed = db.NewContext())
        {
            var payrollGroupId = Guid.NewGuid();
            seed.PayrollGroups.Add(new PayrollGroup { Id = payrollGroupId });
            seed.Employees.Add(new Employee { Id = generatedById, PayrollGroupId = payrollGroupId });
            seed.Employees.Add(new Employee { Id = designatedApproverId, PayrollGroupId = payrollGroupId });
            seed.Employees.Add(new Employee { Id = ineligibleApproverId, PayrollGroupId = payrollGroupId });
            seed.PayrollBatches.Add(new PayrollBatch
            {
                Id = batchId,
                PayPeriodStart = new DateOnly(2026, 1, 1),
                PayPeriodEnd = new DateOnly(2026, 1, 15),
                GeneratedByEmployeeId = generatedById,
                ApprovalStatus = ApprovalStatus.ForApproval,
            });
            seed.Payrolls.Add(new Payroll
            {
                Id = Guid.NewGuid(),
                PayrollBatchId = batchId,
                EmployeeId = Guid.NewGuid(),
            });
            // A configured workflow for PayrollPosting (tenant-wide default, ScopeDepartmentId
            // null) with a single Person step naming ONE specific approver -- anyone else must be
            // rejected, same eligibility rule every other application type enforces.
            var workflowId = Guid.NewGuid();
            seed.ApprovalWorkflows.Add(new ApprovalWorkflow
            {
                Id = workflowId,
                ApplicationType = ApprovalApplicationType.PayrollPosting,
                Name = "Payroll Posting Approval",
                IsActive = true,
                ScopeDepartmentId = null,
            });
            seed.ApprovalWorkflowSteps.Add(new ApprovalWorkflowStep
            {
                Id = Guid.NewGuid(),
                ApprovalWorkflowId = workflowId,
                StepNumber = 1,
                ApproverType = ApproverType.Person,
                ApproverEmployeeId = designatedApproverId,
                MinApprovals = 1,
                NoteRequirement = NoteRequirement.None,
            });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var service = BuildService(uow);
        var act = () => service.ApproveBatchAsync(batchId, ineligibleApproverId, approverHasOverride: false, note: null, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();

        await using var verify = db.NewContext();
        var batch = await verify.PayrollBatches.FindAsync(batchId);
        batch!.ApprovalStatus.Should().Be(ApprovalStatus.ForApproval, "a rejected approval attempt must not advance the batch's status");
        batch.IsPosted.Should().BeFalse();
    }

    [Fact]
    public async Task DeclineBatchAsync_SetsDeclinedStatus_WithoutPosting()
    {
        using var db = new SqliteHrmsContext();
        var batchId = Guid.NewGuid();
        var generatedById = Guid.NewGuid();
        var approverId = Guid.NewGuid();

        await using (var seed = db.NewContext())
        {
            var payrollGroupId = Guid.NewGuid();
            seed.PayrollGroups.Add(new PayrollGroup { Id = payrollGroupId });
            seed.Employees.Add(new Employee { Id = generatedById, PayrollGroupId = payrollGroupId });
            seed.Employees.Add(new Employee { Id = approverId, PayrollGroupId = payrollGroupId });
            seed.PayrollBatches.Add(new PayrollBatch
            {
                Id = batchId,
                PayPeriodStart = new DateOnly(2026, 1, 1),
                PayPeriodEnd = new DateOnly(2026, 1, 15),
                GeneratedByEmployeeId = generatedById,
                ApprovalStatus = ApprovalStatus.ForApproval,
            });
            seed.Payrolls.Add(new Payroll
            {
                Id = Guid.NewGuid(),
                PayrollBatchId = batchId,
                EmployeeId = Guid.NewGuid(),
            });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var service = BuildService(uow);
        await service.DeclineBatchAsync(batchId, approverId, approverHasOverride: false, note: "Needs correction", CancellationToken.None);

        await using var verify = db.NewContext();
        var batch = await verify.PayrollBatches.FindAsync(batchId);
        batch!.ApprovalStatus.Should().Be(ApprovalStatus.Declined);
        batch.IsPosted.Should().BeFalse("a declined batch is never posted");
        // Still deletable -- DeleteBatchAsync's only gate is IsPosted, unaffected by a decline.
        await service.DeleteBatchAsync(batchId, CancellationToken.None);
        await using var verifyDeleted = db.NewContext();
        (await verifyDeleted.PayrollBatches.FindAsync(batchId)).Should().BeNull();
    }

    // DeleteBatchAsync had the exact same "multiple independent CommitChangesAsync calls"
    // shape as ApproveBatchAsync above, just with three culprits instead of two (the mid-method
    // PayrollService.CommitChangesAsync, DailyRecordService.UnpostAsync's own internal commit,
    // and PayrollBatchService.DeleteAsync's own internal commit) -- the PayrollBatch header row
    // removal was the very LAST operation in the method, so it always ran against an
    // already-closed ambient transaction and its delete was silently dropped, even though every
    // earlier step (contribution ledgers, Payroll rows) really did commit. These tests exercise
    // the fix the same way as the ApproveBatchAsync tests above: a real SQLite transaction.
    [Fact]
    public async Task DeleteBatchAsync_DeletesTheBatchHeaderRow_WhenEverythingSucceeds()
    {
        using var db = new SqliteHrmsContext();
        var batchId = Guid.NewGuid();
        var payrollId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();

        await using (var seed = db.NewContext())
        {
            seed.PayrollBatches.Add(new PayrollBatch
            {
                Id = batchId,
                PayPeriodStart = new DateOnly(2026, 1, 1),
                PayPeriodEnd = new DateOnly(2026, 1, 15),
            });
            seed.Payrolls.Add(new Payroll
            {
                Id = payrollId,
                PayrollBatchId = batchId,
                EmployeeId = employeeId,
                PostingPeriod = new DateOnly(2026, 1, 15),
            });
            // The other Payroll-child table this fix newly cleans up alongside
            // PayrollDeductionDetail (DeleteDtrDetailsByPayrollIdsAsync) -- previously left
            // orphaned even before the transaction bug, since nothing called it at all.
            seed.PayrollDtrDetails.Add(new PayrollDtrDetail
            {
                Id = Guid.NewGuid(),
                PayrollId = payrollId,
                EmployeeId = employeeId,
                Date = new DateOnly(2026, 1, 5),
            });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var service = BuildService(uow);
        await service.DeleteBatchAsync(batchId, CancellationToken.None);

        await using var verify = db.NewContext();
        (await verify.PayrollBatches.FindAsync(batchId)).Should().BeNull(
            "the batch header row must actually be gone, not just look deleted within the " +
            "same request -- this is the exact bug being fixed here");
        (await verify.Payrolls.AnyAsync(x => x.PayrollBatchId == batchId)).Should().BeFalse();
        (await verify.PayrollDtrDetails.AnyAsync(x => x.PayrollId == payrollId)).Should().BeFalse(
            "PayrollDtrDetail rows were never cleaned up on delete before this fix");
    }

    [Fact]
    public async Task DeleteBatchAsync_RollsBackEverything_WhenAYearLockBlocksIt()
    {
        using var db = new SqliteHrmsContext();
        var batchId = Guid.NewGuid();
        var payrollId = Guid.NewGuid();

        await using (var seed = db.NewContext())
        {
            seed.PayrollBatches.Add(new PayrollBatch
            {
                Id = batchId,
                PayPeriodStart = new DateOnly(2026, 1, 1),
                PayPeriodEnd = new DateOnly(2026, 1, 15),
            });
            seed.Payrolls.Add(new Payroll
            {
                Id = payrollId,
                PayrollBatchId = batchId,
                EmployeeId = Guid.NewGuid(),
                PostingPeriod = new DateOnly(2026, 1, 15),
            });
            // 2026 is locked -- DeleteBatchAsync must refuse to touch anything this draft's
            // PostingPeriod falls into.
            seed.YearLocks.Add(new YearLock { Id = Guid.NewGuid(), Year = 2026, IsLocked = true });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var service = BuildService(uow);
        var act = () => service.DeleteBatchAsync(batchId, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();

        await using var verify = db.NewContext();
        (await verify.PayrollBatches.FindAsync(batchId)).Should().NotBeNull(
            "nothing should be deleted when the year-lock check fails partway through");
        (await verify.Payrolls.AnyAsync(x => x.PayrollBatchId == batchId)).Should().BeTrue();
    }

    // Unreleasing one employee's posted 13th month payout: only that employee's Payroll row and
    // WTaxContribution go, the shared batch stays while other employees remain in it, and the
    // header goes with the last row. Year-locked or non-13th-month rows are refused.
    private static async Task SeedThirteenthMonthRunAsync(SqliteHrmsContext db, Guid batchId, Guid[] employeeIds, Guid[] payrollIds, PayrollType type = PayrollType.ThirteenthMonth)
    {
        await using var seed = db.NewContext();
        seed.PayrollBatches.Add(new PayrollBatch
        {
            Id = batchId,
            PayPeriodStart = new DateOnly(2026, 1, 1),
            PayPeriodEnd = new DateOnly(2026, 12, 31),
            PayrollType = type,
            IsPosted = true,
        });
        for (var i = 0; i < employeeIds.Length; i++)
        {
            seed.Payrolls.Add(new Payroll
            {
                Id = payrollIds[i],
                PayrollBatchId = batchId,
                EmployeeId = employeeIds[i],
                PayrollType = type,
                IsPosted = true,
                PostingPeriod = new DateOnly(2026, 10, 15),
            });
            seed.TaxContributions.Add(new WTaxContribution
            {
                Id = Guid.NewGuid(),
                PayrollBatchId = batchId,
                EmployeeId = employeeIds[i],
                Amount = 10,
            });
        }
        await seed.SaveChangesAsync();
    }

    [Fact]
    public async Task UnreleasePayoutAsync_RemovesOnlyThatEmployeesPayout_WhenOthersRemainInTheBatch()
    {
        using var db = new SqliteHrmsContext();
        var batchId = Guid.NewGuid();
        var employees = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var payrolls = new[] { Guid.NewGuid(), Guid.NewGuid() };
        await SeedThirteenthMonthRunAsync(db, batchId, employees, payrolls);

        await using var context = db.NewContext();
        var service = BuildService(new UnitOfWorkService(context));
        await service.UnreleasePayoutAsync(payrolls[0], CancellationToken.None);

        await using var verify = db.NewContext();
        (await verify.Payrolls.AnyAsync(x => x.Id == payrolls[0])).Should().BeFalse();
        (await verify.Payrolls.AnyAsync(x => x.Id == payrolls[1])).Should().BeTrue("the other employee's payout is untouched");
        (await verify.TaxContributions.AnyAsync(x => x.EmployeeId == employees[0])).Should().BeFalse();
        (await verify.TaxContributions.AnyAsync(x => x.EmployeeId == employees[1])).Should().BeTrue();
        (await verify.PayrollBatches.FindAsync(batchId)).Should().NotBeNull("the run still has an employee in it");
    }

    [Fact]
    public async Task UnreleasePayoutAsync_RemovesTheBatchHeader_WhenItWasTheLastRow()
    {
        using var db = new SqliteHrmsContext();
        var batchId = Guid.NewGuid();
        var payrollId = Guid.NewGuid();
        await SeedThirteenthMonthRunAsync(db, batchId, [Guid.NewGuid()], [payrollId]);

        await using var context = db.NewContext();
        var service = BuildService(new UnitOfWorkService(context));
        await service.UnreleasePayoutAsync(payrollId, CancellationToken.None);

        await using var verify = db.NewContext();
        (await verify.PayrollBatches.FindAsync(batchId)).Should().BeNull();
        (await verify.Payrolls.AnyAsync(x => x.PayrollBatchId == batchId)).Should().BeFalse();
    }

    [Fact]
    public async Task UnreleaseBatchAsync_RemovesEveryPayoutAndTheBatchHeader_InOneCommand()
    {
        using var db = new SqliteHrmsContext();
        var batchId = Guid.NewGuid();
        var employees = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var payrolls = new[] { Guid.NewGuid(), Guid.NewGuid() };
        await SeedThirteenthMonthRunAsync(db, batchId, employees, payrolls);

        await using var context = db.NewContext();
        var service = BuildService(new UnitOfWorkService(context));
        await service.UnreleaseBatchAsync(batchId, CancellationToken.None);

        await using var verify = db.NewContext();
        (await verify.Payrolls.AnyAsync(x => x.PayrollBatchId == batchId)).Should().BeFalse();
        (await verify.TaxContributions.AnyAsync(x => x.PayrollBatchId == batchId)).Should().BeFalse();
        (await verify.PayrollBatches.FindAsync(batchId)).Should().BeNull();
    }

    [Fact]
    public async Task UnreleaseBatchAsync_Throws_WhenTheRunIsNotAThirteenthMonthOrLastPayRun()
    {
        using var db = new SqliteHrmsContext();
        var batchId = Guid.NewGuid();
        await SeedThirteenthMonthRunAsync(db, batchId, [Guid.NewGuid()], [Guid.NewGuid()], PayrollType.Regular);

        await using var context = db.NewContext();
        var service = BuildService(new UnitOfWorkService(context));
        var act = () => service.UnreleaseBatchAsync(batchId, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        await using var verify = db.NewContext();
        (await verify.PayrollBatches.FindAsync(batchId)).Should().NotBeNull();
    }

    [Fact]
    public async Task UnreleaseBatchAsync_Throws_WhenTheYearIsLocked()
    {
        using var db = new SqliteHrmsContext();
        var batchId = Guid.NewGuid();
        await SeedThirteenthMonthRunAsync(db, batchId, [Guid.NewGuid()], [Guid.NewGuid()]);
        await using (var seed = db.NewContext())
        {
            seed.YearLocks.Add(new YearLock { Id = Guid.NewGuid(), Year = 2026, IsLocked = true });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var service = BuildService(new UnitOfWorkService(context));
        var act = () => service.UnreleaseBatchAsync(batchId, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        await using var verify = db.NewContext();
        (await verify.Payrolls.AnyAsync(x => x.PayrollBatchId == batchId)).Should().BeTrue();
    }

    [Fact]
    public async Task UnreleasePayoutAsync_Throws_WhenThePayoutIsNotAThirteenthMonthRow()
    {
        using var db = new SqliteHrmsContext();
        var payrollId = Guid.NewGuid();
        await SeedThirteenthMonthRunAsync(db, Guid.NewGuid(), [Guid.NewGuid()], [payrollId], PayrollType.Regular);

        await using var context = db.NewContext();
        var service = BuildService(new UnitOfWorkService(context));
        var act = () => service.UnreleasePayoutAsync(payrollId, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        await using var verify = db.NewContext();
        (await verify.Payrolls.AnyAsync(x => x.Id == payrollId)).Should().BeTrue();
    }

    [Fact]
    public async Task UnreleasePayoutAsync_Throws_WhenTheYearIsLocked()
    {
        using var db = new SqliteHrmsContext();
        var payrollId = Guid.NewGuid();
        await SeedThirteenthMonthRunAsync(db, Guid.NewGuid(), [Guid.NewGuid()], [payrollId]);
        await using (var seed = db.NewContext())
        {
            seed.YearLocks.Add(new YearLock { Id = Guid.NewGuid(), Year = 2026, IsLocked = true });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var service = BuildService(new UnitOfWorkService(context));
        var act = () => service.UnreleasePayoutAsync(payrollId, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        await using var verify = db.NewContext();
        (await verify.Payrolls.AnyAsync(x => x.Id == payrollId)).Should().BeTrue();
    }

    [Fact]
    public async Task UnreleasePayoutAsync_Throws_WhenThePayoutIsNotReleasedYet()
    {
        using var db = new SqliteHrmsContext();
        var payrollId = Guid.NewGuid();
        await SeedThirteenthMonthRunAsync(db, Guid.NewGuid(), [Guid.NewGuid()], [payrollId]);
        await using (var seed = db.NewContext())
        {
            var draft = await seed.Payrolls.FirstAsync(x => x.Id == payrollId);
            draft.IsPosted = false;
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var service = BuildService(new UnitOfWorkService(context));
        var act = () => service.UnreleasePayoutAsync(payrollId, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task UnreleasePayoutAsync_ReversesLoanBalanceAndReleasesTheRow_ForALastPay()
    {
        using var db = new SqliteHrmsContext();
        var batchId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var payrollId = Guid.NewGuid();
        var detailId = Guid.NewGuid();
        await SeedThirteenthMonthRunAsync(db, batchId, [employeeId], [payrollId], PayrollType.LastPay);
        await using (var seed = db.NewContext())
        {
            // The posted Last Pay took 300 off a loan installment that was then fully paid.
            var deductionId = Guid.NewGuid();
            var applicationId = Guid.NewGuid();
            seed.Add(new Deduction { Id = deductionId, Code = "COLOAN", Name = "Company Loan" });
            seed.Add(new DeductionApplication
            {
                Id = applicationId, EmployeeId = employeeId, DeductionId = deductionId, Note = "", Remarks = "", Breakdown = [],
            });
            seed.Add(new DeductionApplicationDetail
            {
                Id = detailId, EmployeeId = employeeId, DeductionId = deductionId, DeductionApplicationId = applicationId,
                Amount = 300, Balance = 0, Status = "Paid",
            });
            seed.Add(new PayrollDeductionDetail
            {
                Id = Guid.NewGuid(), PayrollId = payrollId, DeductionApplicationDetailId = detailId, DeductionId = deductionId, Amount = 300,
            });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var service = BuildService(new UnitOfWorkService(context));
        await service.UnreleasePayoutAsync(payrollId, CancellationToken.None);

        await using var verify = db.NewContext();
        (await verify.Payrolls.AnyAsync(x => x.Id == payrollId)).Should().BeFalse();
        (await verify.PayrollDeductionDetails.AnyAsync(x => x.PayrollId == payrollId)).Should().BeFalse();
        var detail = await verify.DeductionApplicationDetails.FirstAsync(x => x.Id == detailId);
        detail.Balance.Should().Be(300m, "the installment the last pay paid is owed again");
        detail.Status.Should().Be("Unpaid");
    }

    [Fact]
    public async Task UnreleasePayoutAsync_Throws_WhenThePayoutDoesNotExist()
    {
        using var db = new SqliteHrmsContext();
        await using var context = db.NewContext();
        var service = BuildService(new UnitOfWorkService(context));

        var act = () => service.UnreleasePayoutAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // Deleting an already-posted batch: RequestDeletionAsync/ApproveDeletionAsync/
    // DeclineDeletionAsync -- a separate ApprovalApplicationType.PayrollPostingDeletion approval
    // cycle from the original PayrollPosting instance (which is already Approved/resolved and
    // can't be reopened). The batch stays fully visible/usable (ApprovalStatus stays Approved)
    // while a deletion request is pending -- see PayrollBatch.PendingDeletion. Mirrors
    // DailyRecordServiceApprovalTests' DTR deletion-approval coverage exactly.

    private async Task<(Guid BatchId, Employee Generator, Employee Approver)> SeedPostedBatchAsync(SqliteHrmsContext db)
    {
        var batchId = Guid.NewGuid();
        var payrollGroupId = Guid.NewGuid();
        var generator = new Employee { Id = Guid.NewGuid(), PayrollGroupId = payrollGroupId };
        var approver = new Employee { Id = Guid.NewGuid(), PayrollGroupId = payrollGroupId };
        await using (var seed = db.NewContext())
        {
            seed.PayrollGroups.Add(new PayrollGroup { Id = payrollGroupId });
            seed.Employees.Add(generator);
            seed.Employees.Add(approver);
            seed.PayrollBatches.Add(new PayrollBatch
            {
                Id = batchId,
                PayPeriodStart = new DateOnly(2026, 1, 1),
                PayPeriodEnd = new DateOnly(2026, 1, 15),
                GeneratedByEmployeeId = generator.Id,
                ApprovalStatus = ApprovalStatus.Approved,
                IsPosted = true,
                PostedAt = DateTime.UtcNow,
                PostedBy = generator.Id,
            });
            seed.Payrolls.Add(new Payroll
            {
                Id = Guid.NewGuid(),
                PayrollBatchId = batchId,
                EmployeeId = generator.Id,
                PostingPeriod = new DateOnly(2026, 1, 15),
            });
            await seed.SaveChangesAsync();
        }
        return (batchId, generator, approver);
    }

    [Fact]
    public async Task RequestDeletionAsync_SetsPendingDeletionAndStartsAPayrollPostingDeletionInstance()
    {
        using var db = new SqliteHrmsContext();
        var (batchId, generator, _) = await SeedPostedBatchAsync(db);

        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var service = BuildService(uow);
        await service.RequestDeletionAsync(batchId, generator.Id, CancellationToken.None);

        await using var verify = db.NewContext();
        var batch = await verify.PayrollBatches.FindAsync(batchId);
        batch!.PendingDeletion.Should().BeTrue();
        batch.RequestedDeletionByEmployeeId.Should().Be(generator.Id);
        batch.ApprovalStatus.Should().Be(ApprovalStatus.Approved, "the batch stays visible/usable while the deletion request is pending");
        batch.IsPosted.Should().BeTrue("nothing about the batch itself changes until the deletion is actually approved");
        (await verify.ApprovalInstances.AnyAsync(x => x.ApplicationId == batchId && x.ApplicationType == ApprovalApplicationType.PayrollPostingDeletion))
            .Should().BeTrue();
    }

    [Fact]
    public async Task RequestDeletionAsync_ThrowsInvalidOperationException_WhenBatchIsNotPosted()
    {
        using var db = new SqliteHrmsContext();
        var batchId = Guid.NewGuid();
        var generatedById = Guid.NewGuid();

        await using (var seed = db.NewContext())
        {
            var payrollGroupId = Guid.NewGuid();
            seed.PayrollGroups.Add(new PayrollGroup { Id = payrollGroupId });
            seed.Employees.Add(new Employee { Id = generatedById, PayrollGroupId = payrollGroupId });
            seed.PayrollBatches.Add(new PayrollBatch
            {
                Id = batchId,
                PayPeriodStart = new DateOnly(2026, 1, 1),
                PayPeriodEnd = new DateOnly(2026, 1, 15),
                GeneratedByEmployeeId = generatedById,
                ApprovalStatus = ApprovalStatus.ForApproval,
            });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var service = BuildService(uow);
        var act = () => service.RequestDeletionAsync(batchId, generatedById, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ApproveDeletionAsync_DeletesTheBatch_WhenEverythingSucceeds()
    {
        using var db = new SqliteHrmsContext();
        var (batchId, generator, approver) = await SeedPostedBatchAsync(db);

        await using (var context1 = db.NewContext())
        {
            var uow1 = new UnitOfWorkService(context1);
            await BuildService(uow1).RequestDeletionAsync(batchId, generator.Id, CancellationToken.None);
        }

        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var service = BuildService(uow);
        // No ApprovalWorkflow configured for PayrollPostingDeletion -- implicit single-approval
        // fallback, same as every other application type.
        await service.ApproveDeletionAsync(batchId, approver.Id, approverHasOverride: false, note: null, CancellationToken.None);

        await using var verify = db.NewContext();
        (await verify.PayrollBatches.FindAsync(batchId)).Should().BeNull("a fully-approved deletion request actually deletes the PayrollBatch header row");
        (await verify.Payrolls.AnyAsync(x => x.PayrollBatchId == batchId)).Should().BeFalse(
            "a fully-approved deletion request actually deletes the Payroll rows too");
    }

    // Regression: deleting a posted run failed with FK_RetirementLedgers_Payrolls_PayrollId --
    // posting had written retirement ledger entries pointing at its Payroll rows -- and it never
    // gave back the loan installments posting had paid.
    [Fact]
    public async Task ApproveDeletionAsync_ReversesWhatPostingDid_RetirementAndLoanBalances()
    {
        using var db = new SqliteHrmsContext();
        var (batchId, generator, approver) = await SeedPostedBatchAsync(db);
        var fundId = Guid.NewGuid();
        var installmentId = Guid.NewGuid();
        await using (var seed = db.NewContext())
        {
            var payrollId = (await seed.Payrolls.SingleAsync(p => p.PayrollBatchId == batchId)).Id;
            var deductionId = Guid.NewGuid();
            var applicationId = Guid.NewGuid();
            // Posting accrued 500 into a fund that already held 1,000...
            seed.RetirementFunds.Add(new RetirementFund { Id = fundId, EmployeeId = generator.Id, Balance = 1500 });
            seed.RetirementLedgers.Add(new RetirementLedger
            {
                Id = Guid.NewGuid(), EmployeeId = generator.Id, RetirementFundId = fundId, PayrollId = payrollId,
                EntryType = RetirementLedgerEntryType.Accrual, EntryDate = new DateOnly(2026, 1, 15),
                Amount = 500, Balance = 1500, Particulars = "Retirement accrual for payroll Jan 1-15",
            });
            // ...and paid off a 1,000 loan installment.
            seed.Add(new Deduction { Id = deductionId, Code = "COLOAN", Name = "Company Loan" });
            seed.Add(new DeductionApplication
            {
                Id = applicationId, EmployeeId = generator.Id, DeductionId = deductionId, Note = "", Remarks = "", Breakdown = [],
            });
            seed.Add(new DeductionApplicationDetail
            {
                Id = installmentId, EmployeeId = generator.Id, DeductionId = deductionId, DeductionApplicationId = applicationId,
                Amount = 1000, Balance = 0, Status = "Paid",
            });
            seed.Add(new PayrollDeductionDetail
            {
                Id = Guid.NewGuid(), PayrollId = payrollId, DeductionApplicationDetailId = installmentId, DeductionId = deductionId, Amount = 1000,
            });
            await seed.SaveChangesAsync();
        }

        await using (var context1 = db.NewContext())
            await BuildService(new UnitOfWorkService(context1)).RequestDeletionAsync(batchId, generator.Id, CancellationToken.None);
        await using (var context = db.NewContext())
            await BuildService(new UnitOfWorkService(context))
                .ApproveDeletionAsync(batchId, approver.Id, approverHasOverride: false, note: null, CancellationToken.None);

        await using var verify = db.NewContext();
        (await verify.PayrollBatches.FindAsync(batchId)).Should().BeNull();
        (await verify.RetirementFunds.SingleAsync(f => f.Id == fundId)).Balance.Should().Be(1000, "the run's accrual is taken back out");
        var ledger = (await verify.RetirementLedgers.Where(l => l.RetirementFundId == fundId).ToListAsync())
            .OrderBy(l => l.Amount).ToList(); // SQLite can't ORDER BY decimal
        ledger.Should().HaveCount(2, "the ledger is an audit trail: the original entry stays, plus a reversal");
        ledger[0].Should().Match<RetirementLedger>(l => l.Amount == -500 && l.Balance == 1000 && l.PayrollId == null);
        ledger[1].Should().Match<RetirementLedger>(l => l.Amount == 500 && l.PayrollId == null && l.Particulars.EndsWith("(payroll run deleted)"));
        var installment = await verify.Set<DeductionApplicationDetail>().SingleAsync(d => d.Id == installmentId);
        installment.Balance.Should().Be(1000, "the installment the run paid is owed again");
        installment.Status.Should().Be("Unpaid");
    }

    // An approver without confidential clearance: the employee's pay records are hidden from
    // them; the profile stays visible so the request can name its filer.
    private sealed class CantSeePayOf(Guid employeeId) : Hrms.Infrastructure.IConfidentialAccess
    {
        public bool Restricted => true;
        public Guid[] HiddenEmployeeIdsFor(ConfidentialDataClass dataClass) =>
            dataClass == ConfidentialDataClass.Profile ? [] : [employeeId];
        public Guid[] ApprovalVisibleRecordIds => [];
        public Guid[] ApprovalVisibleEmployeeIds => [];
    }

    // Regression: deleting a run only removed the rows the approver could see -- a confidential
    // employee's payroll row survived the deletion of its own run (and, with the posting reversal,
    // would have had its balances given back while the row stayed).
    [Fact]
    public async Task ApproveDeletionAsync_RemovesAndReversesEmployeesTheApproverCantSee()
    {
        using var db = new SqliteHrmsContext();
        var (batchId, generator, approver) = await SeedPostedBatchAsync(db);
        var fundId = Guid.NewGuid();
        await using (var seed = db.NewContext())
        {
            var payrollId = (await seed.Payrolls.SingleAsync(p => p.PayrollBatchId == batchId)).Id;
            seed.RetirementFunds.Add(new RetirementFund { Id = fundId, EmployeeId = generator.Id, Balance = 500 });
            seed.RetirementLedgers.Add(new RetirementLedger
            {
                Id = Guid.NewGuid(), EmployeeId = generator.Id, RetirementFundId = fundId, PayrollId = payrollId,
                EntryType = RetirementLedgerEntryType.Accrual, EntryDate = new DateOnly(2026, 1, 15), Amount = 500, Balance = 500,
            });
            await seed.SaveChangesAsync();
        }
        var uncleared = new CantSeePayOf(generator.Id);

        await using (var context1 = db.NewContext(uncleared))
            await BuildService(new UnitOfWorkService(context1)).RequestDeletionAsync(batchId, generator.Id, CancellationToken.None);
        await using (var context = db.NewContext(uncleared))
            await BuildService(new UnitOfWorkService(context))
                .ApproveDeletionAsync(batchId, approver.Id, approverHasOverride: false, note: null, CancellationToken.None);

        await using var verify = db.NewContext();
        (await verify.PayrollBatches.FindAsync(batchId)).Should().BeNull();
        (await verify.Payrolls.AnyAsync(p => p.PayrollBatchId == batchId)).Should().BeFalse("the hidden employee's row goes with its run");
        (await verify.RetirementFunds.SingleAsync(f => f.Id == fundId)).Balance.Should().Be(0);
    }

    [Fact]
    public async Task DeclineDeletionAsync_RevertsPendingDeletion_WithoutDeletingTheBatch()
    {
        using var db = new SqliteHrmsContext();
        var (batchId, generator, approver) = await SeedPostedBatchAsync(db);

        await using (var context1 = db.NewContext())
        {
            var uow1 = new UnitOfWorkService(context1);
            await BuildService(uow1).RequestDeletionAsync(batchId, generator.Id, CancellationToken.None);
        }

        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var service = BuildService(uow);
        await service.DeclineDeletionAsync(batchId, approver.Id, approverHasOverride: false, note: "Keep it", CancellationToken.None);

        await using var verify = db.NewContext();
        var batch = await verify.PayrollBatches.FindAsync(batchId);
        batch!.PendingDeletion.Should().BeFalse("declining the deletion request reverts the batch to normal");
        batch.ApprovalStatus.Should().Be(ApprovalStatus.Approved, "the batch is still posted -- nothing about its own approval was touched");
        batch.IsPosted.Should().BeTrue();
        (await verify.Payrolls.AnyAsync(x => x.PayrollBatchId == batchId)).Should().BeTrue("declining must not delete anything");
    }

    [Fact]
    public async Task ApproveDeletionAsync_ThrowsInvalidOperationException_WhenNoDeletionIsPending()
    {
        using var db = new SqliteHrmsContext();
        var (batchId, _, approver) = await SeedPostedBatchAsync(db);

        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var service = BuildService(uow);
        var act = () => service.ApproveDeletionAsync(batchId, approver.Id, approverHasOverride: false, note: null, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetBatchesAsync_FiltersByOverlappingDateRangeAndProjectsEmployeeCount()
    {
        using var db = new SqliteHrmsContext();
        var inRangeBatchId = Guid.NewGuid();
        var outOfRangeBatchId = Guid.NewGuid();

        await using (var seed = db.NewContext())
        {
            seed.PayrollBatches.Add(new PayrollBatch
            {
                Id = inRangeBatchId,
                PayPeriodStart = new DateOnly(2026, 1, 1),
                PayPeriodEnd = new DateOnly(2026, 1, 15),
            });
            seed.Payrolls.Add(new Payroll { Id = Guid.NewGuid(), PayrollBatchId = inRangeBatchId, EmployeeId = Guid.NewGuid() });
            seed.Payrolls.Add(new Payroll { Id = Guid.NewGuid(), PayrollBatchId = inRangeBatchId, EmployeeId = Guid.NewGuid() });

            seed.PayrollBatches.Add(new PayrollBatch
            {
                Id = outOfRangeBatchId,
                PayPeriodStart = new DateOnly(2025, 1, 1),
                PayPeriodEnd = new DateOnly(2025, 1, 15),
            });
            seed.Payrolls.Add(new Payroll { Id = Guid.NewGuid(), PayrollBatchId = outOfRangeBatchId, EmployeeId = Guid.NewGuid() });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var service = new PayrollBatchService(uow);
        var batches = await service.GetBatchesAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), CancellationToken.None);

        var found = batches.Should().ContainSingle().Subject;
        found.Id.Should().Be(inRangeBatchId);
        found.EmployeeCount.Should().Be(2);
    }

    // The controller no longer applies a hidden 5-month window: an open-ended range must reach
    // batches from previous years.
    [Fact]
    public async Task GetBatchesAsync_OpenEndedRange_ReturnsBatchesFromPreviousYears()
    {
        using var db = new SqliteHrmsContext();

        await using (var seed = db.NewContext())
        {
            foreach (var year in new[] { 2024, 2025, 2026 })
            {
                var id = Guid.NewGuid();
                seed.PayrollBatches.Add(new PayrollBatch
                {
                    Id = id,
                    PayPeriodStart = new DateOnly(year, 1, 1),
                    PayPeriodEnd = new DateOnly(year, 1, 15),
                });
                seed.Payrolls.Add(new Payroll { Id = Guid.NewGuid(), PayrollBatchId = id, EmployeeId = Guid.NewGuid() });
            }
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var service = new PayrollBatchService(new UnitOfWorkService(context));
        var batches = await service.GetBatchesAsync(DateOnly.MinValue, DateOnly.MaxValue, CancellationToken.None);

        batches.Should().HaveCount(3);
    }
}
