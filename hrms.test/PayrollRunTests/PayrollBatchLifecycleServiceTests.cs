using Hrms.Core.Services;
using Hrms.Domain.Entities;
using hrms.test.TestSupport;
using Mapster;
using MapsterMapper;
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

        var dtrService = new DailyRecordService(
            uow, config, mapper,
            Substitute.For<ILogger<DailyRecordService>>(),
            new LeaveDtrReconciliationService(uow, Substitute.For<ILogger<LeaveDtrReconciliationService>>()),
            payrollBatchService);

        var consumptionService = new PayrollInputConsumptionService(uow);
        var yearLockService = new YearLockService(uow);

        return new PayrollBatchLifecycleService(
            uow, payrollBatchService, payrollService, sssService, phicService, hdmfService, taxService,
            dtrService, consumptionService, yearLockService);
    }

    [Fact]
    public async Task PostBatchAsync_PostsBatchAndPayrolls_WhenEverythingSucceeds()
    {
        using var db = new SqliteHrmsContext();
        var batchId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();

        // Seeded through a separate, already-committed context -- exactly like a real Generate
        // Payroll request that ran (and committed) before this separate Post request's own
        // IUnitOfWorkService/ambient transaction even started. See SqliteHrmsContext's doc
        // comment for why this can't go through the same IUnitOfWorkService the test exercises
        // below.
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
                Id = Guid.NewGuid(),
                PayrollBatchId = batchId,
                EmployeeId = employeeId,
            });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var service = BuildService(uow);
        await service.PostBatchAsync(batchId, CancellationToken.None);

        await using var verify = db.NewContext();
        var batch = await verify.PayrollBatches.FindAsync(batchId);
        batch!.IsPosted.Should().BeTrue("the batch header should be posted once the whole sequence succeeds");
        var payroll = await verify.Payrolls.FirstAsync(x => x.PayrollBatchId == batchId);
        payroll.IsPosted.Should().BeTrue("each child payroll row should also be flagged posted");
    }

    [Fact]
    public async Task PostBatchAsync_RollsBackTheBatchFlag_WhenTheSecondStageFails()
    {
        using var db = new SqliteHrmsContext();
        var batchId = Guid.NewGuid();

        // Deliberately no Payroll rows for this batch -- PayrollService.PostBatchAsync throws
        // ValidationException("Payroll batch not found.") for an empty batch, simulating any
        // failure in the second stage (a real DbUpdateException from a unique-index race would
        // hit the same catch/rollback path; this is just the simplest reliable way to trigger it
        // without contriving a genuine constraint violation). Seeded through a separate,
        // already-committed context -- see the comment in the test above.
        await using (var seed = db.NewContext())
        {
            seed.PayrollBatches.Add(new PayrollBatch
            {
                Id = batchId,
                PayPeriodStart = new DateOnly(2026, 1, 1),
                PayPeriodEnd = new DateOnly(2026, 1, 15),
            });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var service = BuildService(uow);
        var act = () => service.PostBatchAsync(batchId, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();

        // The critical assertion: PayrollBatchService.PostAsync's own commit (IsPosted = true)
        // must NOT have survived -- before the transaction fix, this would incorrectly read
        // true, leaving the batch durably "posted" despite the overall operation having failed.
        await using var verify = db.NewContext();
        var batch = await verify.PayrollBatches.FindAsync(batchId);
        batch!.IsPosted.Should().BeFalse(
            "the first stage's commit must roll back too when the second stage fails, " +
            "or a retry would silently skip re-posting the batch header (the 'must post twice' bug)");
    }

    // DeleteBatchAsync had the exact same "multiple independent CommitChangesAsync calls"
    // shape as PostBatchAsync above, just with three culprits instead of two (the mid-method
    // PayrollService.CommitChangesAsync, DailyRecordService.UnpostAsync's own internal commit,
    // and PayrollBatchService.DeleteAsync's own internal commit) -- the PayrollBatch header row
    // removal was the very LAST operation in the method, so it always ran against an
    // already-closed ambient transaction and its delete was silently dropped, even though every
    // earlier step (contribution ledgers, Payroll rows) really did commit. These tests exercise
    // the fix the same way as the PostBatchAsync tests above: a real SQLite transaction.
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
}
