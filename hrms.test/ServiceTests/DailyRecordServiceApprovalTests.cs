using Hrms.Core.Services;
using Hrms.Core.Services.Approvals;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.Approvals;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Infrastructure;
using hrms.test.TestSupport;
using Mapster;
using MapsterMapper;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>
/// DTR Posting Approval Flow — DailyRecordService.SaveDraftAsync/ApproveBatchAsync/
/// DeclineBatchAsync, mirroring the Payroll Posting flow's shape almost exactly (see
/// PayrollBatchLifecycleServiceTests) but for the new DTRBatch header entity. A "Save Draft"
/// (formerly "Post") persists DailyRecord rows as an unposted draft and starts a shared
/// ApprovalApplicationType.Dtr approval instance; only ApproveBatchAsync's final step actually
/// flips DailyRecord.Posted/DTRBatch.IsPosted.
///
/// Needs a REAL relational provider (SQLite ":memory:", via SqliteHrmsContext) since the
/// approval engine's ApprovalInstance/ApprovalAction rows carry real FK constraints to Employee
/// -- see ApprovalEngineService.RecordActionAsync's self-heal path, whose Repository.Update
/// call on a freshly-Added instance was found (via this exact coverage) to silently downgrade
/// it to Modified, skipping the INSERT and orphaning the ApprovalAction row. Fixed alongside
/// this test file.
/// </summary>
public class DailyRecordServiceApprovalTests
{
    private static DailyRecordService BuildService(IUnitOfWorkService uow)
    {
        var mapper = Substitute.For<IMapper>();
        var config = new TypeAdapterConfig();
        var payrollBatchService = new PayrollBatchService(uow);
        var dtrBatchService = new DTRBatchService(uow);
        var approvalEngine = new ApprovalEngineService(uow, Substitute.For<IPublishEndpoint>());

        return new DailyRecordService(
            uow, config, mapper,
            Substitute.For<ILogger<DailyRecordService>>(),
            new LeaveDtrReconciliationService(uow, Substitute.For<ILogger<LeaveDtrReconciliationService>>()),
            payrollBatchService, dtrBatchService, approvalEngine);
    }

    private static (Guid PayrollGroupId, Employee Generator, Employee Approver) SeedEmployees(HrmsContext seed)
    {
        var payrollGroupId = Guid.NewGuid();
        seed.PayrollGroups.Add(new PayrollGroup { Id = payrollGroupId });
        var generator = new Employee { Id = Guid.NewGuid(), PayrollGroupId = payrollGroupId };
        var approver = new Employee { Id = Guid.NewGuid(), PayrollGroupId = payrollGroupId };
        seed.Employees.Add(generator);
        seed.Employees.Add(approver);
        return (payrollGroupId, generator, approver);
    }

    [Fact]
    public async Task SaveDraftAsync_PersistsRecordsAsUnpostedAndStartsAForApprovalInstance()
    {
        using var db = new SqliteHrmsContext();
        Guid payrollGroupId;
        Employee generator;
        await using (var seed = db.NewContext())
        {
            (payrollGroupId, generator, _) = SeedEmployees(seed);
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var service = BuildService(uow);

        var batchCode = "DTRJan012026-Jan152026 TS:test";
        var records = new List<DailyRecord>
        {
            new() { Id = Guid.NewGuid(), EmployeeId = generator.Id, WorkDate = new DateOnly(2026, 1, 1), BatchCode = batchCode, PayrollGroupId = payrollGroupId },
        };
        var batch = await service.SaveDraftAsync(
            records, batchCode, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15),
            payrollGroupId, generator.Id, CancellationToken.None);

        await using var verify = db.NewContext();
        var savedBatch = await verify.DTRBatches.FindAsync(batch.Id);
        savedBatch!.ApprovalStatus.Should().Be(ApprovalStatus.ForApproval);
        savedBatch.IsPosted.Should().BeFalse();
        savedBatch.GeneratedByEmployeeId.Should().Be(generator.Id);
        var savedRecord = await verify.DailyTimeRecords.FirstAsync(x => x.BatchCode == batchCode);
        savedRecord.Posted.Should().BeFalse("Save Draft persists as an unposted draft until approved");
        (await verify.ApprovalInstances.AnyAsync(x => x.ApplicationId == batch.Id && x.ApplicationType == ApprovalApplicationType.Dtr))
            .Should().BeTrue("StartAsync should have created the approval instance at Save Draft time");
    }

    [Fact]
    public async Task ApproveBatchAsync_PostsDailyRecordsAndDtrBatch_WhenEverythingSucceeds()
    {
        using var db = new SqliteHrmsContext();
        var batchId = Guid.NewGuid();
        var batchCode = "DTR-approve-success";
        Employee generator;
        Employee approver;
        await using (var seed = db.NewContext())
        {
            (_, generator, approver) = SeedEmployees(seed);
            seed.DTRBatches.Add(new DTRBatch
            {
                Id = batchId,
                BatchCode = batchCode,
                PayPeriodStart = new DateOnly(2026, 1, 1),
                PayPeriodEnd = new DateOnly(2026, 1, 15),
                GeneratedByEmployeeId = generator.Id,
                ApprovalStatus = ApprovalStatus.ForApproval,
            });
            seed.DailyTimeRecords.Add(new DailyRecord
            {
                Id = Guid.NewGuid(),
                EmployeeId = generator.Id,
                WorkDate = new DateOnly(2026, 1, 1),
                BatchCode = batchCode,
                Posted = false,
            });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var service = BuildService(uow);
        // No ApprovalWorkflow configured for Dtr -- implicit single-approval fallback, same as
        // every other application type.
        await service.ApproveBatchAsync(batchId, approver.Id, approverHasOverride: false, note: null, CancellationToken.None);

        await using var verify = db.NewContext();
        var batch = await verify.DTRBatches.FindAsync(batchId);
        batch!.ApprovalStatus.Should().Be(ApprovalStatus.Approved);
        batch.IsPosted.Should().BeTrue();
        batch.PostedBy.Should().Be(approver.Id);
        var record = await verify.DailyTimeRecords.FirstAsync(x => x.BatchCode == batchCode);
        record.Posted.Should().BeTrue("each DailyRecord row should be flagged posted once approved");
    }

    [Fact]
    public async Task DeclineBatchAsync_SetsDeclinedStatus_WithoutPosting()
    {
        using var db = new SqliteHrmsContext();
        var batchId = Guid.NewGuid();
        var batchCode = "DTR-decline";
        Employee generator;
        Employee approver;
        await using (var seed = db.NewContext())
        {
            (_, generator, approver) = SeedEmployees(seed);
            seed.DTRBatches.Add(new DTRBatch
            {
                Id = batchId,
                BatchCode = batchCode,
                PayPeriodStart = new DateOnly(2026, 1, 1),
                PayPeriodEnd = new DateOnly(2026, 1, 15),
                GeneratedByEmployeeId = generator.Id,
                ApprovalStatus = ApprovalStatus.ForApproval,
            });
            seed.DailyTimeRecords.Add(new DailyRecord
            {
                Id = Guid.NewGuid(),
                EmployeeId = generator.Id,
                WorkDate = new DateOnly(2026, 1, 1),
                BatchCode = batchCode,
                Posted = false,
            });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var service = BuildService(uow);
        await service.DeclineBatchAsync(batchId, approver.Id, approverHasOverride: false, note: "Needs correction", CancellationToken.None);

        await using var verify = db.NewContext();
        var batch = await verify.DTRBatches.FindAsync(batchId);
        batch!.ApprovalStatus.Should().Be(ApprovalStatus.Declined);
        batch.IsPosted.Should().BeFalse("a declined batch is never posted");
        var record = await verify.DailyTimeRecords.FirstAsync(x => x.BatchCode == batchCode);
        record.Posted.Should().BeFalse();

        // Still deletable -- a declined-but-unposted batch stays deletable, same resolution as
        // Payroll's equivalent (only IsPosted blocks Delete, not ApprovalStatus).
        await service.DeleteAsync(batchCode, CancellationToken.None);
        await using var verifyDeleted = db.NewContext();
        (await verifyDeleted.DTRBatches.FindAsync(batchId)).Should().BeNull("DeleteAsync cleans up the DTRBatch header row alongside its DailyRecord rows");
        (await verifyDeleted.DailyTimeRecords.AnyAsync(x => x.BatchCode == batchCode)).Should().BeFalse();
    }

    // A DTR batch's own Posted flag is no longer a reliable signal that a Payroll run exists
    // for it -- DailyRecordsController's still-live Unpost endpoint can flip Posted back to
    // false without knowing (or caring) whether a Payroll run still references this batch code.
    // DeleteAsync must check the real source of truth (Payroll.DtrBatchCodes, via
    // PayrollBatchService.GetUsedDtrBatchCodesAsync) directly, independent of Posted.
    [Fact]
    public async Task DeleteAsync_ThrowsValidationException_WhenAPayrollRunHasAlreadyBeenSavedFromTheBatch_EvenIfUnposted()
    {
        using var db = new SqliteHrmsContext();
        var batchCode = "DTR-payroll-already-generated";
        await using (var seed = db.NewContext())
        {
            var (_, generator, _) = SeedEmployees(seed);
            seed.DailyTimeRecords.Add(new DailyRecord
            {
                Id = Guid.NewGuid(),
                EmployeeId = generator.Id,
                WorkDate = new DateOnly(2026, 1, 1),
                BatchCode = batchCode,
                // Deliberately unposted -- simulates the batch having been unposted again after
                // a payroll run was already saved from it, via the still-live Unpost endpoint.
                Posted = false,
            });
            seed.PayrollBatches.Add(new PayrollBatch
            {
                Id = Guid.NewGuid(),
                PayPeriodStart = new DateOnly(2026, 1, 1),
                PayPeriodEnd = new DateOnly(2026, 1, 15),
                DtrBatchCodes = batchCode,
            });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var service = BuildService(uow);
        var act = () => service.DeleteAsync(batchCode, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();

        await using var verify = db.NewContext();
        (await verify.DailyTimeRecords.AnyAsync(x => x.BatchCode == batchCode)).Should().BeTrue(
            "nothing should be deleted when a payroll run already references this batch");
    }

    [Fact]
    public async Task ApproveBatchAsync_ThrowsInvalidOperationException_WhenBatchIsNotAwaitingApproval()
    {
        using var db = new SqliteHrmsContext();
        var batchId = Guid.NewGuid();
        Employee generator;
        Employee approver;
        await using (var seed = db.NewContext())
        {
            (_, generator, approver) = SeedEmployees(seed);
            seed.DTRBatches.Add(new DTRBatch
            {
                Id = batchId,
                BatchCode = "DTR-already-resolved",
                PayPeriodStart = new DateOnly(2026, 1, 1),
                PayPeriodEnd = new DateOnly(2026, 1, 15),
                GeneratedByEmployeeId = generator.Id,
                // Already resolved -- e.g. a second click on a batch already approved/declined.
                ApprovalStatus = ApprovalStatus.Approved,
            });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var service = BuildService(uow);
        var act = () => service.ApproveBatchAsync(batchId, approver.Id, approverHasOverride: false, note: null, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ApproveBatchAsync_ThrowsUnauthorizedAccessException_WhenApproverIsIneligible()
    {
        using var db = new SqliteHrmsContext();
        var batchId = Guid.NewGuid();
        var batchCode = "DTR-ineligible";
        Guid payrollGroupId;
        Employee generator;
        Employee ineligibleApprover;
        var designatedApproverId = Guid.NewGuid();
        await using (var seed = db.NewContext())
        {
            (payrollGroupId, generator, ineligibleApprover) = SeedEmployees(seed);
            seed.Employees.Add(new Employee { Id = designatedApproverId, PayrollGroupId = payrollGroupId });
            seed.DTRBatches.Add(new DTRBatch
            {
                Id = batchId,
                BatchCode = batchCode,
                PayPeriodStart = new DateOnly(2026, 1, 1),
                PayPeriodEnd = new DateOnly(2026, 1, 15),
                GeneratedByEmployeeId = generator.Id,
                ApprovalStatus = ApprovalStatus.ForApproval,
            });
            seed.DailyTimeRecords.Add(new DailyRecord
            {
                Id = Guid.NewGuid(),
                EmployeeId = generator.Id,
                WorkDate = new DateOnly(2026, 1, 1),
                BatchCode = batchCode,
                Posted = false,
            });
            // A configured workflow for Dtr (tenant-wide default) with a single Person step
            // naming ONE specific approver -- anyone else must be rejected, same eligibility
            // rule every other application type enforces.
            var workflowId = Guid.NewGuid();
            seed.ApprovalWorkflows.Add(new ApprovalWorkflow
            {
                Id = workflowId,
                ApplicationType = ApprovalApplicationType.Dtr,
                Name = "DTR Posting Approval",
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
        var act = () => service.ApproveBatchAsync(batchId, ineligibleApprover.Id, approverHasOverride: false, note: null, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();

        await using var verify = db.NewContext();
        var batch = await verify.DTRBatches.FindAsync(batchId);
        batch!.ApprovalStatus.Should().Be(ApprovalStatus.ForApproval, "a rejected approval attempt must not advance the batch's status");
        batch.IsPosted.Should().BeFalse();
    }

    [Fact]
    public async Task GetBatches_ReportsApprovedForALegacyBatchWithNoDTRBatchRow()
    {
        using var db = new SqliteHrmsContext();
        var batchCode = "DTR-legacy-no-header";
        await using (var seed = db.NewContext())
        {
            var (_, generator, _) = SeedEmployees(seed);
            seed.DailyTimeRecords.Add(new DailyRecord
            {
                Id = Guid.NewGuid(),
                EmployeeId = generator.Id,
                WorkDate = new DateOnly(2026, 1, 1),
                BatchCode = batchCode,
                Posted = true,
            });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var service = BuildService(uow);
        var batches = await service.GetBatches(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), CancellationToken.None);

        var legacy = batches.Should().ContainSingle(x => x.Code == batchCode).Subject;
        legacy.Id.Should().BeNull();
        legacy.ApprovalStatus.Should().Be(ApprovalStatus.Approved,
            "a legacy batch that predates DTRBatch must not retroactively show as pending");
    }

    // The Calculate DTR batch list shows Generated By / Generated At straight from these fields.
    // Names are resolved server-side, and a batch saved before BuzlinkRepository 1.3.7 stamped
    // CreatedAt on [DisableSoftDelete] entities (DTRBatch is one) holds DateTime.MinValue --
    // that must come back as null ("unknown"), not as year 1.
    [Fact]
    public async Task GetBatches_ResolvesGeneratorNames_AndReportsUnsetCreatedAtAsNull()
    {
        using var db = new SqliteHrmsContext();
        var stampedAt = new DateTime(2026, 9, 24, 3, 48, 0, DateTimeKind.Utc);
        Employee generator;
        Employee requester;
        await using (var seed = db.NewContext())
        {
            (_, generator, requester) = SeedEmployees(seed);
            generator.LastName = "Abella";
            generator.FirstName = "Jerome";
            requester.LastName = "Cruz";
            requester.FirstName = "Ana";
            foreach (var (code, day) in new[] { ("DTR-unstamped", 1), ("DTR-stamped", 16) })
            {
                seed.DailyTimeRecords.Add(new DailyRecord
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = generator.Id,
                    WorkDate = new DateOnly(2026, 1, day),
                    BatchCode = code,
                });
            }
            seed.DTRBatches.Add(new DTRBatch
            {
                Id = Guid.NewGuid(),
                BatchCode = "DTR-unstamped",
                GeneratedByEmployeeId = generator.Id,
                CreatedAt = DateTime.MinValue,
            });
            seed.DTRBatches.Add(new DTRBatch
            {
                Id = Guid.NewGuid(),
                BatchCode = "DTR-stamped",
                GeneratedByEmployeeId = generator.Id,
                CreatedAt = stampedAt,
                PendingDeletion = true,
                RequestedDeletionByEmployeeId = requester.Id,
            });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var service = BuildService(new UnitOfWorkService(context));
        var batches = await service.GetBatches(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), CancellationToken.None);

        var unstamped = batches.Should().ContainSingle(x => x.Code == "DTR-unstamped").Subject;
        unstamped.GeneratedByName.Should().StartWith("Abella, Jerome");
        unstamped.GeneratedAt.Should().BeNull("DateTime.MinValue is an unset timestamp, not a real time");

        var stamped = batches.Should().ContainSingle(x => x.Code == "DTR-stamped").Subject;
        stamped.GeneratedByName.Should().StartWith("Abella, Jerome");
        stamped.GeneratedAt.Should().Be(stampedAt);
        stamped.RequestedDeletionByName.Should().StartWith("Cruz, Ana");
    }

    // Deleting an already-posted batch: RequestDeletionAsync/ApproveDeletionAsync/
    // DeclineDeletionAsync -- a separate ApprovalApplicationType.DtrDeletion approval cycle from
    // the original Dtr posting instance (which is already Approved/resolved and can't be
    // reopened). The batch stays fully visible/usable (ApprovalStatus stays Approved) while a
    // deletion request is pending -- see DTRBatch.PendingDeletion.

    private async Task<(Guid BatchId, string BatchCode, Employee Generator, Employee Approver)> SeedPostedBatchAsync(
        SqliteHrmsContext db, string batchCode)
    {
        var batchId = Guid.NewGuid();
        Employee generator;
        Employee approver;
        await using (var seed = db.NewContext())
        {
            (_, generator, approver) = SeedEmployees(seed);
            seed.DTRBatches.Add(new DTRBatch
            {
                Id = batchId,
                BatchCode = batchCode,
                PayPeriodStart = new DateOnly(2026, 1, 1),
                PayPeriodEnd = new DateOnly(2026, 1, 15),
                GeneratedByEmployeeId = generator.Id,
                ApprovalStatus = ApprovalStatus.Approved,
                IsPosted = true,
                PostedAt = DateTime.UtcNow,
                PostedBy = generator.Id,
            });
            seed.DailyTimeRecords.Add(new DailyRecord
            {
                Id = Guid.NewGuid(),
                EmployeeId = generator.Id,
                WorkDate = new DateOnly(2026, 1, 1),
                BatchCode = batchCode,
                Posted = true,
            });
            await seed.SaveChangesAsync();
        }
        return (batchId, batchCode, generator, approver);
    }

    [Fact]
    public async Task RequestDeletionAsync_SetsPendingDeletionAndStartsADtrDeletionInstance()
    {
        using var db = new SqliteHrmsContext();
        var (batchId, batchCode, generator, _) = await SeedPostedBatchAsync(db, "DTR-request-deletion");

        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var service = BuildService(uow);
        await service.RequestDeletionAsync(batchId, generator.Id, CancellationToken.None);

        await using var verify = db.NewContext();
        var batch = await verify.DTRBatches.FindAsync(batchId);
        batch!.PendingDeletion.Should().BeTrue();
        batch.RequestedDeletionByEmployeeId.Should().Be(generator.Id);
        batch.ApprovalStatus.Should().Be(ApprovalStatus.Approved, "the batch stays visible/usable while the deletion request is pending");
        var record = await verify.DailyTimeRecords.FirstAsync(x => x.BatchCode == batchCode);
        record.Posted.Should().BeTrue("nothing about the batch itself changes until the deletion is actually approved");
        (await verify.ApprovalInstances.AnyAsync(x => x.ApplicationId == batchId && x.ApplicationType == ApprovalApplicationType.DtrDeletion))
            .Should().BeTrue();
    }

    [Fact]
    public async Task RequestDeletionAsync_ThrowsInvalidOperationException_WhenBatchIsNotPosted()
    {
        using var db = new SqliteHrmsContext();
        var batchId = Guid.NewGuid();
        Employee generator;
        await using (var seed = db.NewContext())
        {
            (_, generator, _) = SeedEmployees(seed);
            seed.DTRBatches.Add(new DTRBatch
            {
                Id = batchId,
                BatchCode = "DTR-not-posted-yet",
                PayPeriodStart = new DateOnly(2026, 1, 1),
                PayPeriodEnd = new DateOnly(2026, 1, 15),
                GeneratedByEmployeeId = generator.Id,
                ApprovalStatus = ApprovalStatus.ForApproval,
            });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var service = BuildService(uow);
        var act = () => service.RequestDeletionAsync(batchId, generator.Id, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task RequestDeletionAsync_ThrowsValidationException_WhenAPayrollRunHasAlreadyBeenSavedFromTheBatch()
    {
        using var db = new SqliteHrmsContext();
        var batchId = Guid.NewGuid();
        var batchCode = "DTR-payroll-linked";
        Employee generator;
        await using (var seed = db.NewContext())
        {
            (_, generator, _) = SeedEmployees(seed);
            seed.DTRBatches.Add(new DTRBatch
            {
                Id = batchId,
                BatchCode = batchCode,
                PayPeriodStart = new DateOnly(2026, 1, 1),
                PayPeriodEnd = new DateOnly(2026, 1, 15),
                GeneratedByEmployeeId = generator.Id,
                ApprovalStatus = ApprovalStatus.Approved,
                IsPosted = true,
            });
            seed.PayrollBatches.Add(new PayrollBatch
            {
                Id = Guid.NewGuid(),
                PayPeriodStart = new DateOnly(2026, 1, 1),
                PayPeriodEnd = new DateOnly(2026, 1, 15),
                DtrBatchCodes = batchCode,
            });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var service = BuildService(uow);
        var act = () => service.RequestDeletionAsync(batchId, generator.Id, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();

        await using var verify = db.NewContext();
        (await verify.DTRBatches.FindAsync(batchId))!.PendingDeletion.Should().BeFalse(
            "a payroll-linked batch is hard-blocked even from requesting deletion");
    }

    [Fact]
    public async Task ApproveDeletionAsync_DeletesTheBatch_WhenEverythingSucceeds()
    {
        using var db = new SqliteHrmsContext();
        var (batchId, batchCode, generator, approver) = await SeedPostedBatchAsync(db, "DTR-approve-deletion");

        await using (var context1 = db.NewContext())
        {
            var uow1 = new UnitOfWorkService(context1);
            await BuildService(uow1).RequestDeletionAsync(batchId, generator.Id, CancellationToken.None);
        }

        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var service = BuildService(uow);
        // No ApprovalWorkflow configured for DtrDeletion -- implicit single-approval fallback,
        // same as every other application type.
        await service.ApproveDeletionAsync(batchId, approver.Id, approverHasOverride: false, note: null, CancellationToken.None);

        await using var verify = db.NewContext();
        (await verify.DTRBatches.FindAsync(batchId)).Should().BeNull("a fully-approved deletion request actually deletes the DTRBatch header row");
        (await verify.DailyTimeRecords.AnyAsync(x => x.BatchCode == batchCode)).Should().BeFalse(
            "a fully-approved deletion request actually deletes the DailyRecord rows too");
    }

    [Fact]
    public async Task DeclineDeletionAsync_RevertsPendingDeletion_WithoutDeletingTheBatch()
    {
        using var db = new SqliteHrmsContext();
        var (batchId, batchCode, generator, approver) = await SeedPostedBatchAsync(db, "DTR-decline-deletion");

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
        var batch = await verify.DTRBatches.FindAsync(batchId);
        batch!.PendingDeletion.Should().BeFalse("declining the deletion request reverts the batch to normal");
        batch.ApprovalStatus.Should().Be(ApprovalStatus.Approved, "the batch is still posted -- nothing about its own approval was touched");
        batch.IsPosted.Should().BeTrue();
        (await verify.DailyTimeRecords.AnyAsync(x => x.BatchCode == batchCode)).Should().BeTrue("declining must not delete anything");
    }

    [Fact]
    public async Task ApproveDeletionAsync_ThrowsInvalidOperationException_WhenNoDeletionIsPending()
    {
        using var db = new SqliteHrmsContext();
        var (batchId, _, _, approver) = await SeedPostedBatchAsync(db, "DTR-no-pending-deletion");

        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var service = BuildService(uow);
        var act = () => service.ApproveDeletionAsync(batchId, approver.Id, approverHasOverride: false, note: null, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
