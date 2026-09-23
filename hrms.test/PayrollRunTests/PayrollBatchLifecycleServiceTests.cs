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
            dtrService, consumptionService, yearLockService, approvalEngine, dtrBatchService);
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
    public async Task ApproveBatchAsync_ThrowsUnauthorizedAccessException_WhenApproverIsIneligible()
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

        await act.Should().ThrowAsync<UnauthorizedAccessException>();

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
}
