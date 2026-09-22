using Hrms.Core.Services;
using Hrms.Domain.Entities;
using hrms.test.TestSupport;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace hrms.test.PayrollRunTests;

/// <summary>
/// PayrollBatchService.AddAsync used to commit the PayrollBatch header immediately, independent
/// of PayrollService.SavePayrollsAsync's own separate commit for its Payroll children. Every
/// payroll-generating flow (Regular/13th Month/Last Pay/Year-End Adjustment) follows exactly
/// this create-batch-then-save-rows shape, so anything that threw between those two commits (a
/// mapping error, a cancelled request) left a PayrollBatch permanently orphaned with zero
/// children -- stuck forever, since PayrollService.PostBatchAsync throws "Payroll batch not
/// found" for an empty batch, and nothing cleaned it up.
///
/// Both methods now take commit=false so a caller can compose them into ONE atomic unit, mirroring
/// PayrollBatchLifecycleServiceTests' already-established fix for the same class of bug in the
/// Post flow. Needs a REAL relational provider (SQLite ":memory:") since only that supports
/// Database.BeginTransactionAsync/CommitAsync/RollbackAsync.
/// </summary>
public class PayrollBatchAtomicCreationTests
{
    [Fact]
    public async Task CommitFalse_BothStagesSucceed_BatchAndPayrollPersistTogether_WhenCommittedOnce()
    {
        using var db = new SqliteHrmsContext();
        var batchId = Guid.NewGuid();

        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var batchService = new PayrollBatchService(uow);
        var payrollService = new PayrollService(uow, Substitute.For<IMapper>());

        await batchService.AddAsync(new PayrollBatch
        {
            Id = batchId,
            PayPeriodStart = new DateOnly(2026, 1, 1),
            PayPeriodEnd = new DateOnly(2026, 1, 15),
        }, CancellationToken.None, commit: false);

        await payrollService.SavePayrollsAsync(new[]
        {
            new Payroll { Id = Guid.NewGuid(), PayrollBatchId = batchId, EmployeeId = Guid.NewGuid() },
        }, CancellationToken.None, commit: false);

        // The single atomic commit point every generator flow now performs.
        await payrollService.CommitChangesAsync(CancellationToken.None);

        await using var verify = db.NewContext();
        (await verify.PayrollBatches.FindAsync(batchId)).Should().NotBeNull(
            "the batch header should persist once the whole sequence commits");
        (await verify.Payrolls.CountAsync(x => x.PayrollBatchId == batchId)).Should().Be(1,
            "its Payroll child row should persist together with the batch header");
    }

    [Fact]
    public async Task CommitFalse_SecondStageNeverRuns_NeitherBatchNorAnythingPersists()
    {
        using var db = new SqliteHrmsContext();
        var batchId = Guid.NewGuid();

        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var batchService = new PayrollBatchService(uow);

        // Simulates the exact failure window this fix closes: batch creation succeeds (but only
        // flushes, doesn't commit, with commit: false), then something throws before
        // SavePayrollsAsync/the final CommitChangesAsync ever run -- e.g. a mapping exception
        // building the Payroll rows. The ambient transaction is never committed, so disposing the
        // context rolls it back instead of leaving an orphaned batch durably persisted.
        await batchService.AddAsync(new PayrollBatch
        {
            Id = batchId,
            PayPeriodStart = new DateOnly(2026, 1, 1),
            PayPeriodEnd = new DateOnly(2026, 1, 15),
        }, CancellationToken.None, commit: false);

        // No SavePayrollsAsync, no CommitChangesAsync -- context disposed with the transaction
        // still open, exactly what happens when an exception propagates out of a generator flow
        // before it reaches its own commit point.
        await context.DisposeAsync();

        await using var verify = db.NewContext();
        (await verify.PayrollBatches.FindAsync(batchId)).Should().BeNull(
            "an uncommitted batch must roll back on dispose, not survive as a permanently " +
            "orphaned header with zero Payroll children");
    }

    [Fact]
    public async Task DefaultCommitTrue_StillCommitsImmediately_ExistingCallersUnaffected()
    {
        // Every OTHER existing caller of AddAsync/SavePayrollsAsync relies on the default
        // commit: true behavior (each call commits on its own) -- confirms that default still
        // holds so this change is purely additive for the 4 generator flows that opted into
        // commit: false.
        using var db = new SqliteHrmsContext();
        var batchId = Guid.NewGuid();

        await using var context = db.NewContext();
        var uow = new UnitOfWorkService(context);
        var batchService = new PayrollBatchService(uow);

        await batchService.AddAsync(new PayrollBatch
        {
            Id = batchId,
            PayPeriodStart = new DateOnly(2026, 1, 1),
            PayPeriodEnd = new DateOnly(2026, 1, 15),
        }, CancellationToken.None);

        await using var verify = db.NewContext();
        (await verify.PayrollBatches.FindAsync(batchId)).Should().NotBeNull(
            "AddAsync's default commit: true must still persist immediately on its own");
    }
}
