using System.Linq.Expressions;
using DocumentFormat.OpenXml.Office2010.ExcelAc;
using Hrms.Core.Services.Approvals;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Mapster;
using Microsoft.Extensions.Logging;

namespace Hrms.Core.Services;

public class DailyRecordService : BaseService<DailyRecord>
{
    private readonly TypeAdapterConfig _config;
    private readonly IMapper _mapper;
    private readonly ILogger<DailyRecordService> _logger;
    private readonly LeaveDtrReconciliationService _reconciliation;
    private readonly PayrollBatchService _payrollBatchService;
    private readonly DTRBatchService _dtrBatchService;
    private readonly ApprovalEngineService _approvalEngine;

    public DailyRecordService(IUnitOfWorkService uow,
        TypeAdapterConfig config,
        IMapper mapper,
        ILogger<DailyRecordService> logger,
        LeaveDtrReconciliationService reconciliation,
        PayrollBatchService payrollBatchService,
        DTRBatchService dtrBatchService,
        ApprovalEngineService approvalEngine) : base(uow)
    {
        _config = config;
        _mapper = mapper;
        _logger = logger;
        _reconciliation = reconciliation;
        _payrollBatchService = payrollBatchService;
        _dtrBatchService = dtrBatchService;
        _approvalEngine = approvalEngine;
    }
    // Posted attendance/DTR days for this employee in (fromDate, toDate] — used by
    // LastPayrollService's safety check: days worked after their last regular payroll's
    // period end that no regular run has ever paid out. fromDate is exclusive since it's
    // meant to be passed the last-paid period's own end date.
    public async Task<int> CountPostedDaysAsync(Guid employeeId, DateOnly fromDate, DateOnly toDate, CancellationToken token)
    {
        return await GetQueryable(x =>
                x.EmployeeId == employeeId &&
                x.Posted &&
                x.WorkDate > fromDate &&
                x.WorkDate <= toDate)
            .CountAsync(token);
    }

    // Leave.EligibilityBasis.PresentDays — counts posted days up to (and including) toDate
    // that aren't Absent/Incomplete/Skipped (see LeaveEligibilityCalculator.NonPresentWorkTypes).
    // No lower bound needed: DTR rows don't exist before an employee's hire date.
    public async Task<int> CountPresentDaysAsync(Guid employeeId, DateOnly toDate, CancellationToken token)
    {
        return await GetQueryable(x =>
                x.EmployeeId == employeeId &&
                x.Posted &&
                x.WorkDate <= toDate &&
                !LeaveEligibilityCalculator.NonPresentWorkTypes.Contains(x.WorkTypeEnum))
            .CountAsync(token);
    }

    // Batch form of CountPresentDaysAsync for LeavePeriodGrantWorker, which checks every
    // active employee against every lump-sum leave type in one pass — one grouped query
    // instead of one round-trip per employee.
    public async Task<Dictionary<Guid, int>> CountPresentDaysBatchAsync(IEnumerable<Guid> employeeIds, DateOnly toDate, CancellationToken token)
    {
        var ids = employeeIds.ToList();
        if (ids.Count == 0) return new Dictionary<Guid, int>();

        return await GetQueryable(x =>
                ids.Contains(x.EmployeeId) &&
                x.Posted &&
                x.WorkDate <= toDate &&
                !LeaveEligibilityCalculator.NonPresentWorkTypes.Contains(x.WorkTypeEnum))
            .GroupBy(x => x.EmployeeId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, token);
    }

    // Client.UniformAllowanceBasis.PresentDays -- present days WITHIN [fromDate, toDate] (one
    // calendar month), unlike CountPresentDaysAsync/CountPresentDaysBatchAsync above, which are
    // unbounded lifetime-to-date counts for Leave's tenure eligibility. See
    // UniformAllowanceAccrualWorker.
    public async Task<Dictionary<Guid, int>> CountPresentDaysInRangeBatchAsync(
        IEnumerable<Guid> employeeIds, DateOnly fromDate, DateOnly toDate, CancellationToken token)
    {
        var ids = employeeIds.ToList();
        if (ids.Count == 0) return new Dictionary<Guid, int>();

        return await GetQueryable(x =>
                ids.Contains(x.EmployeeId) &&
                x.Posted &&
                x.WorkDate >= fromDate &&
                x.WorkDate <= toDate &&
                !LeaveEligibilityCalculator.NonPresentWorkTypes.Contains(x.WorkTypeEnum))
            .GroupBy(x => x.EmployeeId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, token);
    }

    // commit=false lets SaveDraftAsync compose this with the DTRBatch header write and
    // ApprovalEngineService.StartAsync as ONE atomic unit -- same reasoning as
    // PayrollBatchService.AddAsync's commit parameter.
    public async Task AddRangeAsync(List<DailyRecord> records, CancellationToken token, bool commit = true)
    {
        var employeeIds = records.Select(x => x.EmployeeId).Distinct().ToList();
        var minDate = records.Min(x => x.WorkDate);
        var maxDate = records.Max(x => x.WorkDate);

        var existing = await _uow.Repository
            .Find<DailyRecord>(x => employeeIds.Contains(x.EmployeeId)
                && x.WorkDate >= minDate && x.WorkDate <= maxDate)
            .Select(x => new { x.EmployeeId, x.WorkDate, x.FullName })
            .ToListAsync(token);

        var existingKeys = existing.Select(x => (x.EmployeeId, x.WorkDate)).ToHashSet();
        var duplicates = records
            .Where(x => existingKeys.Contains((x.EmployeeId, x.WorkDate)))
            .ToList();

        if (duplicates.Count > 0)
        {
            var names = string.Join(", ", duplicates
                .Select(d => d.FullName ?? d.EmployeeId.ToString())
                .Distinct());
            throw new ValidationException(
                $"DTR already exists for {duplicates.Count} employee-date(s) in this range ({names}). " +
                "Delete the existing batch first if you need to repost.");
        }

        await Uow.Repository.AddRangeAsync(records, token);
        if (commit) await CommitChangesAsync(token);
        else await SaveChangesAsync(token);
    }

    // Replaces the controller's direct AddRangeAsync call -- persists the calculated DTR rows as
    // an editable/deletable draft (DailyRecord.Posted stays false) AND creates the DTRBatch
    // header, starting its Dtr-type approval instance automatically at Save Draft time, same as
    // every other application type (see ApprovalEngineService.StartAsync). Only ApproveBatchAsync,
    // once the instance resolves to Approved, actually flips Posted=true via PostAsync.
    public async Task<DTRBatch> SaveDraftAsync(
        List<DailyRecord> records, string batchCode, DateOnly rangeFrom, DateOnly rangeTo,
        Guid? payrollGroupId, Guid generatedByEmployeeId, CancellationToken token)
    {
        await AddRangeAsync(records, token, commit: false);

        var batch = new DTRBatch
        {
            BatchCode = batchCode,
            PayPeriodStart = rangeFrom,
            PayPeriodEnd = rangeTo,
            PayrollGroupId = payrollGroupId,
            GeneratedByEmployeeId = generatedByEmployeeId,
        };
        await _dtrBatchService.AddAsync(batch, token, commit: false);
        await _approvalEngine.StartAsync(ApprovalApplicationType.Dtr, batch.Id, generatedByEmployeeId, token);

        await CommitChangesAsync(token);
        return batch;
    }

    // One shared ApprovalApplicationType.Dtr approval type. Runs RecordActionAsync plus (on the
    // final step) the existing PostAsync body as ONE atomic unit -- same transaction-composition
    // reasoning as PayrollBatchLifecycleService.ApproveBatchAsync: every step flushes via
    // commit:false/SaveChangesAsync and joins the same still-open ambient transaction; the one
    // real CommitChangesAsync call at the end finalizes all of it together, and any exception
    // before that point rolls the whole thing back via the still-open ambient transaction.
    public async Task ApproveBatchAsync(Guid dtrBatchId, Guid? approverEmployeeId, bool approverHasOverride, string? note, CancellationToken token)
    {
        var batch = await _dtrBatchService.FineOneAsync(dtrBatchId, token)
            ?? throw new NotFoundException("DTR batch not found.");
        if (batch.ApprovalStatus != ApprovalStatus.ForApproval)
            throw new InvalidOperationException("This DTR batch is not awaiting approval.");
        var approverId = approverEmployeeId ?? throw new AccountNotLinkedException("this approval action can't be recorded");

        try
        {
            var result = await _approvalEngine.RecordActionAsync(
                ApprovalApplicationType.Dtr, batch.Id, batch.GeneratedByEmployeeId,
                approverId, approverHasOverride, ApprovalActionType.Approved, note, token);

            batch.ApprovalStatus = ApprovalEngineService.MapInstanceStatus(result.InstanceStatus);
            if (batch.ApprovalStatus == ApprovalStatus.Approved)
            {
                batch.IsPosted = true;
                batch.PostedAt = DateTime.UtcNow;
                batch.PostedBy = approverId;
                await _dtrBatchService.UpdateAsync(batch, token, commit: false);
                await PostAsync(batch.BatchCode, token, commit: false);
            }
            else
            {
                // More steps remain -- stays ForApproval, not posted yet.
                await _dtrBatchService.UpdateAsync(batch, token, commit: false);
            }

            await CommitChangesAsync(token);
        }
        catch
        {
            if (_uow.CurrentTransaction != null)
            {
                await _uow.CurrentTransaction.RollbackAsync(token);
            }
            throw;
        }
    }

    public async Task DeclineBatchAsync(Guid dtrBatchId, Guid? approverEmployeeId, bool approverHasOverride, string? note, CancellationToken token)
    {
        var batch = await _dtrBatchService.FineOneAsync(dtrBatchId, token)
            ?? throw new NotFoundException("DTR batch not found.");
        if (batch.ApprovalStatus != ApprovalStatus.ForApproval)
            throw new InvalidOperationException("This DTR batch is not awaiting approval.");
        var approverId = approverEmployeeId ?? throw new AccountNotLinkedException("this approval action can't be recorded");

        try
        {
            var result = await _approvalEngine.RecordActionAsync(
                ApprovalApplicationType.Dtr, batch.Id, batch.GeneratedByEmployeeId,
                approverId, approverHasOverride, ApprovalActionType.Declined, note, token);

            batch.ApprovalStatus = ApprovalEngineService.MapInstanceStatus(result.InstanceStatus);
            await _dtrBatchService.UpdateAsync(batch, token, commit: false);
            await CommitChangesAsync(token);
        }
        catch
        {
            if (_uow.CurrentTransaction != null)
            {
                await _uow.CurrentTransaction.RollbackAsync(token);
            }
            throw;
        }
    }
    public async Task<string> BuildBatchCodeAsync(DateOnly rangeFrom, DateOnly rangeTo, Guid? payrollGroupId, CancellationToken token)
    {
        TimeZoneInfo manilaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
        DateTime localTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, manilaTimeZone);
        var ts = localTime.ToString("MM.dd.yyyy.HH:mm");

        var payrollGroupCode = payrollGroupId.HasValue
            ? await Context.PayrollGroups
                .Where(x => x.Id == payrollGroupId.Value)
                .Select(x => x.Code)
                .FirstOrDefaultAsync(token)
            : null;

        var pgSegment = string.IsNullOrWhiteSpace(payrollGroupCode) ? "" : $" PG:{payrollGroupCode}";
        var baseCode = $"DTR{rangeFrom.ToString("MMMddyyyy")}-{rangeTo.ToString("MMMddyyyy")}{pgSegment} TS:{ts}";
        return await MakeUniqueBatchCodeAsync(baseCode, token);
    }

    // The timestamp is only to the minute, so two drafts for the same period and payroll group
    // saved in the same minute (e.g. one per client) used to share a code -- and everything keyed
    // by it (approve, post, delete, the Saved DTR list) treated them as one batch. A taken code
    // gets "-2", "-3", ... Checked against both the batch headers and the DTR rows (legacy batches
    // have rows only), including rows of confidential employees the caller can't see.
    internal async Task<string> MakeUniqueBatchCodeAsync(string baseCode, CancellationToken token)
    {
        var taken = (await Context.DTRBatches
                .Where(x => x.BatchCode.StartsWith(baseCode))
                .Select(x => x.BatchCode)
                .ToListAsync(token))
            .Concat(await Context.IgnoringConfidentiality<DailyRecord>()
                .Where(x => x.BatchCode != null && x.BatchCode.StartsWith(baseCode))
                .Select(x => x.BatchCode!)
                .Distinct()
                .ToListAsync(token))
            .ToHashSet();

        if (!taken.Contains(baseCode)) return baseCode;
        for (var n = 2; ; n++)
        {
            var candidate = $"{baseCode}-{n}";
            if (!taken.Contains(candidate)) return candidate;
        }
    }
    // Direct delete -- only for a batch that isn't posted yet (ForApproval/Declined), where
    // nothing is final and no approval is needed. An already-posted batch must go through
    // RequestDeletionAsync/ApproveDeletionAsync instead -- see below.
    public async Task DeleteAsync(string batchCode, CancellationToken token)
    {
        // Across the whole batch -- posted days of an employee the caller can't see count too.
        var hasPosted = await BatchRecords(batchCode).AnyAsync(x => x.Posted, token);
        if (hasPosted)
            throw new ValidationException("This DTR batch has already been posted — request its deletion for approval instead.");

        // The real "payroll already saved from this batch" check -- Posted alone no longer
        // implies this since DTR posting/approval is decoupled from Payroll generation, and
        // DailyRecordsController's still-live Unpost endpoint can flip Posted back to false
        // without knowing (or caring) whether a Payroll run still references this batch code.
        // Same source of truth GetBatches' IsPayrollGenerated flag uses -- see
        // Payroll.DtrBatchCodes / PayrollBatchService.GetUsedDtrBatchCodesAsync. This is a hard
        // block even through the deletion-approval flow below -- an approver taking on
        // responsibility for a batch still can't un-break a live Payroll run's DTR references.
        // Day-level (a cutoff-mode run may have paid just some of this batch's days) plus the
        // run header's batch-code list (the legacy batch-mode link).
        var usedBatchCodes = await _payrollBatchService.GetUsedDtrBatchCodesAsync(token);
        if (usedBatchCodes.Contains(batchCode) || await BatchRecords(batchCode).AnyAsync(x => x.PayrollBatchId != null, token))
            throw new ValidationException("Cannot delete this DTR batch — payroll has already been generated and saved from it. Delete the payroll run first if you need to regenerate it.");

        await ExecuteBatchDeletionAsync(batchCode, token);
    }

    // The actual cascade, shared by the direct-delete path above (an unposted draft) and
    // ApproveDeletionAsync below (an already-posted batch, once its deletion request itself
    // clears approval).
    private async Task ExecuteBatchDeletionAsync(string batchCode, CancellationToken token)
    {
        // Give back every leave day this batch consumed BEFORE its rows go -- otherwise the
        // Deduction entries outlive the DTR and those days stay charged forever. Same
        // transaction as the delete; a no-op for an unposted draft (nothing was consumed).
        await _reconciliation.ReverseConsumptionAsync(batchCode, token);
        await SaveChangesAsync(token);
        // Every row of the batch, including confidential employees the caller can't see -- the
        // batch header goes too, so none may be left behind (see BatchRecords).
        await BatchRecords(batchCode).ExecuteDeleteAsync(token);
        // Cleans up the now-orphaned DTRBatch header row alongside its DailyRecord rows -- a
        // no-op for a legacy batch that predates this entity.
        await _dtrBatchService.DeleteByCodeAsync(batchCode, token, commit: false);
        await CommitChangesAsync(token);
    }

    // Requesting deletion of an already-posted batch starts a separate DtrDeletion approval
    // instance instead of deleting outright (a resolved Dtr posting instance can't be reopened
    // for a second approval cycle -- see ApprovalApplicationType.DtrDeletion). The batch stays
    // fully visible/usable (ApprovalStatus stays Approved, DailyRecord rows untouched) while the
    // request is pending -- PendingDeletion is purely informational until it clears.
    public async Task RequestDeletionAsync(Guid dtrBatchId, Guid requestedByEmployeeId, CancellationToken token)
    {
        var batch = await _dtrBatchService.FineOneAsync(dtrBatchId, token)
            ?? throw new NotFoundException("DTR batch not found.");
        if (batch.ApprovalStatus != ApprovalStatus.Approved || !batch.IsPosted)
            throw new InvalidOperationException("Only an already-posted batch needs its deletion approved — delete an unposted draft directly instead.");
        if (batch.PendingDeletion)
            throw new InvalidOperationException("A deletion request is already pending for this batch.");

        var usedBatchCodes = await _payrollBatchService.GetUsedDtrBatchCodesAsync(token);
        if (usedBatchCodes.Contains(batch.BatchCode) || await BatchRecords(batch.BatchCode).AnyAsync(x => x.PayrollBatchId != null, token))
            throw new ValidationException("Cannot delete this DTR batch — payroll has already been generated and saved from it. Delete the payroll run first if you need to regenerate it.");

        batch.PendingDeletion = true;
        batch.RequestedDeletionByEmployeeId = requestedByEmployeeId;
        await _dtrBatchService.UpdateAsync(batch, token, commit: false);
        await _approvalEngine.StartAsync(ApprovalApplicationType.DtrDeletion, batch.Id, requestedByEmployeeId, token);
        await CommitChangesAsync(token);
    }

    public async Task ApproveDeletionAsync(Guid dtrBatchId, Guid? approverEmployeeId, bool approverHasOverride, string? note, CancellationToken token)
    {
        var batch = await _dtrBatchService.FineOneAsync(dtrBatchId, token)
            ?? throw new NotFoundException("DTR batch not found.");
        if (!batch.PendingDeletion)
            throw new InvalidOperationException("This batch has no deletion request awaiting approval.");
        var approverId = approverEmployeeId ?? throw new AccountNotLinkedException("this approval action can't be recorded");

        try
        {
            var result = await _approvalEngine.RecordActionAsync(
                ApprovalApplicationType.DtrDeletion, batch.Id, batch.RequestedDeletionByEmployeeId ?? batch.GeneratedByEmployeeId,
                approverId, approverHasOverride, ApprovalActionType.Approved, note, token);

            if (result.InstanceStatus == ApprovalInstanceStatus.Approved)
            {
                // Fully approved -- the batch (and its DTRBatch header) is actually deleted now.
                await ExecuteBatchDeletionAsync(batch.BatchCode, token);
            }
            else
            {
                // More steps remain in a multi-step workflow -- stays PendingDeletion, nothing
                // else to persist here (RecordActionAsync already tracked its own writes).
                await CommitChangesAsync(token);
            }
        }
        catch
        {
            if (_uow.CurrentTransaction != null)
            {
                await _uow.CurrentTransaction.RollbackAsync(token);
            }
            throw;
        }
    }

    public async Task DeclineDeletionAsync(Guid dtrBatchId, Guid? approverEmployeeId, bool approverHasOverride, string? note, CancellationToken token)
    {
        var batch = await _dtrBatchService.FineOneAsync(dtrBatchId, token)
            ?? throw new NotFoundException("DTR batch not found.");
        if (!batch.PendingDeletion)
            throw new InvalidOperationException("This batch has no deletion request awaiting approval.");
        var approverId = approverEmployeeId ?? throw new AccountNotLinkedException("this approval action can't be recorded");

        try
        {
            await _approvalEngine.RecordActionAsync(
                ApprovalApplicationType.DtrDeletion, batch.Id, batch.RequestedDeletionByEmployeeId ?? batch.GeneratedByEmployeeId,
                approverId, approverHasOverride, ApprovalActionType.Declined, note, token);

            // Declining a deletion request just reverts the batch to normal — ApprovalStatus was
            // never touched, so it's already back to Approved/posted as if nothing happened.
            batch.PendingDeletion = false;
            await _dtrBatchService.UpdateAsync(batch, token, commit: false);
            await CommitChangesAsync(token);
        }
        catch
        {
            if (_uow.CurrentTransaction != null)
            {
                await _uow.CurrentTransaction.RollbackAsync(token);
            }
            throw;
        }
    }
    // Cutoff-mode filter shared by the day loader, the leave loader and the warning queries:
    // inside the range, not yet paid by any payroll run, and Posted (approved) or not as asked.
    // Group, employee, department and client are all optional and come from the Employee record (its current
    // tags), never from the DTR row, so the run follows how the employee is set up today.
    private static Expression<Func<DailyRecord, bool>> CutoffFilter(CutoffScope c, bool posted) =>
        x => x.Posted == posted
            && x.PayrollBatchId == null
            && x.WorkDate >= c.From && x.WorkDate <= c.To
            && (c.PayrollGroupId == null || x.Employee!.PayrollGroupId == c.PayrollGroupId)
            && (c.EmployeeIds == null || c.EmployeeIds.Count == 0 || c.EmployeeIds.Contains(x.EmployeeId))
            && (c.DepartmentIds == null || c.DepartmentIds.Count == 0 || (x.Employee!.DepartmentId != null && c.DepartmentIds.Contains(x.Employee!.DepartmentId.Value)))
            && (c.ClientIds == null || c.ClientIds.Count == 0 || (x.Employee!.ClientId != null && c.ClientIds.Contains(x.Employee!.ClientId.Value)));

    // Per-employee payroll run: every payable DTR day of the group's employees in the range,
    // regardless of which DTR batch or client it was reported under. The requested range is
    // returned as the pay period (not Min/Max of the days found) so cutoff/statutory handling
    // stays stable when a later run pays only the remainder of a cutoff.
    public async Task<(Dictionary<EmployeeKey, List<DailyRecordRunModel>> Records, DateOnly FromDate, DateOnly ToDate)>
    LoadForPayrollRunByCutoffAsync(CutoffScope scope, CancellationToken token)
    {
        var recordsList = await GetQueryable(CutoffFilter(scope, posted: true))
             .AsNoTracking()
             .ProjectToType<DailyRecordRunModel>(_config)
             .AsSplitQuery()
             .ToListAsync(token);

        var records = recordsList
            .GroupBy(x => new EmployeeKey(x.EmployeeId!))
            .ToDictionary(g => g.Key, g => g.ToList());
        return (records, scope.From, scope.To);
    }

    public Task<Dictionary<EmployeeKey, List<LeaveMetaDataModel>>>
        LoadLeaveInfoForPayrollRunByCutoffAsync(CutoffScope scope, CancellationToken token) =>
        LoadLeaveInfoAsync(CutoffFilter(scope, posted: true), token);

    // Employees of the group with DTR days in the range that aren't approved yet and unpaid --
    // shown as a warning on the cutoff-mode payroll preview ("these people have days still waiting
    // on DTR approval") so nobody is silently short-paid.
    public async Task<List<PayrollNotReadyEmployee>> GetNotReadyEmployeesAsync(CutoffScope scope, CancellationToken token)
    {
        var rows = await GetQueryable(CutoffFilter(scope, posted: false))
            .Select(x => new { x.EmployeeId, x.FullName, x.BatchCode })
            .ToListAsync(token);

        return rows
            .GroupBy(x => x.EmployeeId)
            .Select(g => new PayrollNotReadyEmployee
            {
                EmployeeId = g.Key,
                FullName = g.Select(x => x.FullName).FirstOrDefault(n => !string.IsNullOrEmpty(n)),
                UnapprovedDays = g.Count(),
                BatchCodes = g.Select(x => x.BatchCode).Where(c => !string.IsNullOrEmpty(c)).Select(c => c!).Distinct().ToList(),
            })
            .OrderBy(x => x.FullName)
            .ToList();
    }

    // Per-employee day counts behind the Payroll Run screen's readiness list (and the Calculate
    // DTR screen's coverage view): every DTR day in the
    // range, split into approved-and-unpaid, pending (not approved, unpaid) and already paid.
    // Group is optional and, like CutoffFilter, read from the Employee record. Status is left for
    // the caller (PayrollProcessorService.GetReadinessAsync), which also knows about FIXED pay.
    public async Task<List<PayrollReadinessEmployee>> GetPayrollReadinessAsync(DateOnly from, DateOnly to, Guid? payrollGroupId, CancellationToken token)
    {
        var rows = await GetQueryable(x => x.WorkDate >= from && x.WorkDate <= to
                && (payrollGroupId == null || x.Employee!.PayrollGroupId == payrollGroupId))
            .Select(x => new
            {
                x.EmployeeId,
                x.FullName,
                x.BatchCode,
                x.WorkDate,
                x.Posted,
                Paid = x.PayrollBatchId != null,
                x.Employee!.EmployeeNo,
                x.Employee!.PayrollGroupId,
                x.Employee!.ClientId,
                x.Employee!.DepartmentId,
                x.Employee!.SalaryType,
            })
            .ToListAsync(token);

        return rows
            .GroupBy(x => x.EmployeeId)
            .Select(g =>
            {
                var first = g.First();
                var pending = g.Where(x => !x.Paid && !x.Posted).ToList();
                return new PayrollReadinessEmployee
                {
                    EmployeeId = g.Key,
                    EmployeeNo = first.EmployeeNo,
                    FullName = g.Select(x => x.FullName).FirstOrDefault(n => !string.IsNullOrEmpty(n)),
                    PayrollGroupId = first.PayrollGroupId,
                    ClientId = first.ClientId,
                    DepartmentId = first.DepartmentId,
                    SalaryType = first.SalaryType,
                    ApprovedDays = g.Count(x => !x.Paid && x.Posted),
                    PendingDays = pending.Count,
                    PaidDays = g.Count(x => x.Paid),
                    PendingBatchCodes = pending.Select(x => x.BatchCode).Where(c => !string.IsNullOrEmpty(c)).Select(c => c!).Distinct().ToList(),
                    SavedDates = g.Select(x => x.WorkDate).Distinct().Order().ToList(),
                };
            })
            .OrderBy(x => x.FullName)
            .ToList();
    }

    // FIXED-salary employees of the group who have approved, unpaid days in the range -- the
    // candidates for the "fixed salary already paid" skip in a cutoff-mode run.
    public async Task<List<PayrollNotReadyEmployee>> GetPayableFixedSalaryEmployeesAsync(CutoffScope scope, CancellationToken token)
    {
        var rows = await GetQueryable(CutoffFilter(scope, posted: true))
            .Where(x => x.Employee!.SalaryType == SalaryType.FIXED)
            .Select(x => new { x.EmployeeId, x.FullName })
            .ToListAsync(token);
        return rows
            .GroupBy(x => x.EmployeeId)
            .Select(g => new PayrollNotReadyEmployee
            {
                EmployeeId = g.Key,
                FullName = g.Select(x => x.FullName).FirstOrDefault(n => !string.IsNullOrEmpty(n)),
                UnapprovedDays = g.Count(),
            })
            .ToList();
    }

    // Stamps the payroll run on every day it paid, in the same unit of work as the run's header and
    // Payroll rows (nothing is flushed here -- the caller's CommitChangesAsync does it). Fails if any
    // day was paid by another run since this one loaded it, which rolls the whole run back.
    public async Task MarkPaidAsync(IReadOnlyCollection<Guid> dailyRecordIds, Guid payrollBatchId, CancellationToken token)
    {
        if (dailyRecordIds.Count == 0) return;
        var stamped = 0;
        foreach (var chunk in dailyRecordIds.Chunk(1000))
        {
            var ids = chunk.ToList();
            var records = await Context.IgnoringConfidentiality<DailyRecord>()
                .Where(x => ids.Contains(x.Id))
                .ToListAsync(token);
            foreach (var r in records)
            {
                if (r.PayrollBatchId != null && r.PayrollBatchId != payrollBatchId)
                    throw new ValidationException("Some of these DTR days were just paid by another payroll run. Preview again and retry.");
                r.PayrollBatchId = payrollBatchId;
            }
            stamped += records.Count;
        }
        if (stamped != dailyRecordIds.Count)
            throw new ValidationException("Some of these DTR days no longer exist. Preview again and retry.");
        await SaveChangesAsync(token);
    }

    // Releases every day a payroll run had paid so it can be paid again -- the inverse of
    // MarkPaidAsync. The days stay approved/posted; nothing needs to be re-approved.
    public async Task ReleasePaidAsync(Guid payrollBatchId, CancellationToken token)
    {
        var records = await Context.IgnoringConfidentiality<DailyRecord>()
            .Where(x => x.PayrollBatchId == payrollBatchId)
            .ToListAsync(token);
        foreach (var r in records) r.PayrollBatchId = null;
        await SaveChangesAsync(token);
    }

    public async Task<(Dictionary<EmployeeKey, List<DailyRecordRunModel>> Records, DateOnly FromDate, DateOnly ToDate)>
    LoadForPayrollRunAsync(List<string> batchCodes, CancellationToken token)
    {
        // Guard 1: Argument validation
        ArgumentNullException.ThrowIfNull(batchCodes);

        // Guard 2: Short-circuit empty inputs to avoid unnecessary DB calls
        if (batchCodes.Count == 0)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            return ([], today, today);
        }

        // The projection pulls two collections per DTR row (LeavesInfo and the employee's payroll
        // group CutoffDays); as one statement they multiply rows, so it runs as split queries.
        var recordsList = await GetQueryable(x => batchCodes.Contains(x.BatchCode!))
             .AsNoTracking()
             .ProjectToType<DailyRecordRunModel>(_config)
             .AsSplitQuery()
             .ToListAsync(token);

        // Guard 3: Return early if database returns no matching records
        if (recordsList.Count == 0)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            return ([], today, today);
        }

        var records = recordsList
            .GroupBy(x => new EmployeeKey(x.EmployeeId!))
            .ToDictionary(g => g.Key, g => g.ToList());

        // Single-pass optimization for date range calculation
        var fromDate = recordsList.Min(x => x.WorkDate);
        var toDate = recordsList.Max(x => x.WorkDate);

        return (records, fromDate, toDate);
    }

    // Per-employee leave-type breakdown (which leave, how many hours, paid/unpaid) for the
    // days covered by this Generate run — a dedicated hand-rolled projection rather than
    // trusting LoadForPayrollRunAsync's ProjectToType<DailyRecordRunModel> to also pull the
    // LeavesInfo child collection along for free, matching the proven-working pattern
    // DTRDetailQuery already uses for the same navigation. Used by
    // PayrollProcessorService to build PayrollSummaryLine.PaidLeaveBreakdown — richer,
    // per-leave-type detail behind the existing lump PaidLeaves/UnpaidLeaves totals.
    public async Task<Dictionary<EmployeeKey, List<LeaveMetaDataModel>>>
        LoadLeaveInfoForPayrollRunAsync(List<string> batchCodes, CancellationToken token)
    {
        if (batchCodes.Count == 0) return [];
        return await LoadLeaveInfoAsync(x => batchCodes.Contains(x.BatchCode!), token);
    }

    private async Task<Dictionary<EmployeeKey, List<LeaveMetaDataModel>>>
        LoadLeaveInfoAsync(Expression<Func<DailyRecord, bool>> dayFilter, CancellationToken token)
    {
        var rows = await GetQueryable(dayFilter)
            .Where(x => x.LeavesInfo != null && x.LeavesInfo.Any())
            .AsNoTracking()
            .SelectMany(x => x.LeavesInfo!.Select(li => new
            {
                x.EmployeeId,
                li.LeaveId,
                li.Name,
                li.Hours,
                li.StartDateTime,
                li.EndDateTime,
                li.PayType,
            }))
            .ToListAsync(token);

        return rows
            .GroupBy(x => new EmployeeKey(x.EmployeeId))
            .ToDictionary(g => g.Key, g => g.Select(x => new LeaveMetaDataModel
            {
                LeaveId = x.LeaveId,
                Name = x.Name,
                Hours = x.Hours,
                StartDateTime = x.StartDateTime,
                EndDateTime = x.EndDateTime,
                PayType = x.PayType,
            }).ToList());
    }

    public async Task<Dictionary<EmployeeKey, List<DailyRecordRunModel>>>
        LoadForPayrollAsync(PayrollCalcPayload payload,
        CancellationToken token)
    {
        IQueryable<DailyRecord> query;

        if (payload.BatchCodes != null && payload.BatchCodes.Count > 0)
        {
            query = GetQueryable(x => x.Posted && payload.BatchCodes.Contains(x.BatchCode!))
                .AsNoTracking();
        }
        else
        {
            query = GetQueryable(GetQueryExpression(payload))
                .AsNoTracking();
        }

        // Same two collections as LoadForPayrollRunAsync -- split. (No Include needed: EF ignores
        // Include once ProjectToType shapes the result.)
        var records = await query
            .ProjectToType<DailyRecordRunModel>(_config)
            .AsSplitQuery()
            .Where(x => x.EmployeeId != null)
            .ToListAsync(token);
        return records
            .GroupBy(x => new EmployeeKey(x.EmployeeId))
            .ToDictionary(x => x.Key, x => x.ToList());
    }

    private Expression<Func<DailyRecord, bool>> GetQueryExpression(DTRQueryPayload payload)
    {
        Expression<Func<DailyRecord, bool>> expression = x =>
            x.Posted &&
            x.WorkDate >= payload.FromDate && x.WorkDate <= payload.ToDate &&
            (payload.EmployeeId == null || x.EmployeeId == payload.EmployeeId) &&
            (payload.DepartmentId == null || x.DepartmentId == payload.DepartmentId) &&
            (payload.PayrollGroupId == null || x.PayrollGroupId == payload.PayrollGroupId) &&
            (payload.ClientId == null || x.ClientId == payload.ClientId);
        return expression;
    }

    // commit=false lets ApproveBatchAsync compose this with the DTRBatch status/posted-flag
    // write as ONE atomic unit -- same reasoning as UnpostAsync's commit parameter below.
    // Posting, unposting and deleting act on a whole batch, whoever runs them: a confidential
    // employee the approver can't see is still in the batch, so these skip the confidential-
    // employee filter (company scoping and soft-delete still apply). Same for the leave
    // reconciliation they drive -- see LeaveDtrReconciliationService.Everyone.
    private IQueryable<DailyRecord> BatchRecords(string batchCode) =>
        Context.IgnoringConfidentiality<DailyRecord>().Where(x => x.BatchCode == batchCode);

    public async Task PostAsync(string batchCode, CancellationToken token, bool commit = true)
    {
        var records = await BatchRecords(batchCode)
            .Where(x => !x.Posted)
            .Include(x => x.LeavesInfo)
            .ToListAsync(token);

        foreach (var r in records)
            r.Posted = true;

        // Phase 2: convert approval-time reservations into authoritative deductions
        // using PaidLeaveHours/LeavesInfo already set by the DTR computation engine.
        await _reconciliation.ConsumeReservationsAsync(batchCode, records, token);

        if (commit) await CommitChangesAsync(token);
        else await SaveChangesAsync(token);
        _logger.LogInformation("DTR posted: batch {BatchCode} ({Count} records)", batchCode, records.Count);
    }

    // commit=false lets PayrollBatchLifecycleService.DeleteBatchAsync compose this with the
    // rest of that method's cleanup as ONE atomic unit -- same reasoning as
    // PayrollBatchService.PostAsync's commit parameter.
    public async Task UnpostAsync(string batchCode, CancellationToken token, bool commit = true)
    {
        var records = await BatchRecords(batchCode)
            .Where(x => x.Posted)
            .ToListAsync(token);

        foreach (var r in records)
        {
            r.Posted = false;
            r.PaidLeaveHours = 0;
        }

        // Reverse Phase 2 credit deductions; restores reservations for still-approved leaves
        await _reconciliation.ReverseConsumptionAsync(batchCode, token);

        if (commit) await CommitChangesAsync(token);
        else await SaveChangesAsync(token);
        _logger.LogInformation("DTR unposted: batch {BatchCode} ({Count} records)", batchCode, records.Count);
    }

    // Unpost individual records by employee + date range (used when a leave status changes
    // after the DTR for that period was already posted).
    public async Task<int> UnpostByDateRangeAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken token)
    {
        var records = await _uow.Repository
            .Find<DailyRecord>(x =>
                x.EmployeeId == employeeId &&
                x.WorkDate >= from &&
                x.WorkDate <= to &&
                x.Posted)
            .ToListAsync(token);

        if (records.Count == 0) return 0;

        foreach (var r in records)
        {
            r.Posted = false;
            r.PaidLeaveHours = 0;
        }

        // Caller is responsible for CommitChangesAsync so this can be batched
        // with other changes in the same unit of work.
        return records.Count;
    }

    public async Task<List<BatchesModel>> GetBatches(DateOnly fromDate, DateOnly toDate, CancellationToken token)
    {
        // Batches with any day in the range, then aggregated over ALL of each batch's rows -- the
        // old query kept only x.First() per batch, so every batch showed one day as its period
        // ("Sep 01 – Sep 01"), 1 employee, and the posted flag of a single row.
        var codesInRange = Context.DailyTimeRecords
            .Where(x => x.WorkDate >= fromDate && x.WorkDate <= toDate && x.BatchCode != null)
            .Select(x => x.BatchCode)
            .Distinct();

        var records = await Context.DailyTimeRecords
            .Where(x => codesInRange.Contains(x.BatchCode))
            .GroupBy(x => x.BatchCode)
            .Select(g => new
            {
                g.Key,
                FromDate = g.Min(x => x.WorkDate),
                ToDate = g.Max(x => x.WorkDate),
                EmployeeCount = g.Select(x => x.EmployeeId).Distinct().Count(),
                UnpostedCount = g.Count(x => !x.Posted),
                TotalCount = g.Count(),
                PaidCount = g.Count(x => x.PayrollBatchId != null),
                PostingDescription = g.Max(x => x.PostingDescription),
            })
            .ToListAsync(token);

        // Left-join by BatchCode (a plain string, not a real FK) -- a legacy batch that predates
        // DTRBatch has no matching row here and is reported as already-Approved, see BatchesModel.
        var batchCodes = records.Select(x => x.Key).Where(x => x != null).Select(x => x!).Distinct().ToList();
        var dtrBatches = await _dtrBatchService.FindByCodesAsync(batchCodes, token);
        // Batches saved before codes were made unique can share one (same period and payroll
        // group, same minute) -- list the newest header instead of failing the whole list.
        var dtrBatchesByCode = dtrBatches
            .GroupBy(x => x.BatchCode)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.CreatedAt).First());

        // One lookup for every generator/deletion-requester across all batches, so the UI can
        // show names without downloading the whole employee list to resolve a few ids.
        var employeeIds = dtrBatches
            .Select(x => x.GeneratedByEmployeeId)
            .Concat(dtrBatches.Where(x => x.RequestedDeletionByEmployeeId.HasValue)
                .Select(x => x.RequestedDeletionByEmployeeId!.Value))
            .Distinct()
            .ToList();
        var employeeNames = employeeIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _uow.Repository.Find<Employee>(x => employeeIds.Contains(x.Id))
                .AsNoTracking()
                // Name columns only, not full employee rows.
                .Select(x => new Employee { Id = x.Id, FirstName = x.FirstName, LastName = x.LastName, MiddleName = x.MiddleName, Suffix = x.Suffix })
                .ToListAsync(token))
                .ToDictionary(x => x.Id, x => x.FullName());
        string? NameOf(Guid? id) =>
            id.HasValue && employeeNames.TryGetValue(id.Value, out var name) ? name : null;

        return records
         .Select(g =>
         {
             dtrBatchesByCode.TryGetValue(g.Key ?? "", out var dtrBatch);
             return new BatchesModel
             {
                 Id = dtrBatch?.Id,
                 Code = g.Key,
                 // The batch header's pay period is authoritative; legacy batches without a
                 // DTRBatch row fall back to the span of their DTR days.
                 FromDate = dtrBatch is { PayPeriodStart: var start } && start != default ? start : g.FromDate,
                 ToDate = dtrBatch is { PayPeriodEnd: var end } && end != default ? end : g.ToDate,
                 EmployeeCount = g.EmployeeCount,
                 IsPosted = g.UnpostedCount == 0,
                 PostingDescription = g.PostingDescription ?? "",
                 // Fully paid only -- a cutoff-mode run can pay some of a batch's days and leave the rest
                 // for a later run, so a partly-paid batch still has work left.
                 IsPayrollGenerated = g.PaidCount > 0 && g.PaidCount == g.TotalCount,
                 ApprovalStatus = dtrBatch?.ApprovalStatus ?? ApprovalStatus.Approved,
                 GeneratedByEmployeeId = dtrBatch?.GeneratedByEmployeeId,
                 GeneratedByName = NameOf(dtrBatch?.GeneratedByEmployeeId),
                 PayrollGroupId = dtrBatch?.PayrollGroupId,
                 // Rows saved before CreatedAt was stamped for [DisableSoftDelete] entities hold
                 // DateTime.MinValue -- report those as unknown rather than as year 1.
                 GeneratedAt = dtrBatch != null && dtrBatch.CreatedAt > DateTime.MinValue
                     ? dtrBatch.CreatedAt
                     : null,
                 PendingDeletion = dtrBatch?.PendingDeletion ?? false,
                 RequestedDeletionByEmployeeId = dtrBatch?.RequestedDeletionByEmployeeId,
                 RequestedDeletionByName = NameOf(dtrBatch?.RequestedDeletionByEmployeeId),
             };
         })
         .OrderByDescending(x=>x.Code)
         .ToList();
    }

    public async Task<List<DTRSummaryModel>> DTRSummaryQuery(string BatchCode, CancellationToken token)
    {

        var dtr = _uow.Repository
            .FindAll<DailyRecord>()
            .AsNoTracking()
            .Where(x => x.BatchCode == BatchCode)
             ;

        return await dtr
            //.Where(x => x.Posted)
            .GroupBy(x => new { x.EmployeeId, x.BatchCode })
            .Select(x => new DTRSummaryModel()
            {
                FullName = x.First().FullName,
                EmployeeId = x.Key.EmployeeId,
                BatchCode = x.Key.BatchCode,
                UTHours = x.Sum(x => x.UTMinutes / 60),
                OverHours = x.Sum(x => x.OverMinutes / 60),
                LateHours = x.Sum(x => x.LateForOTMinutes / 60),

                RegularNetHours = x.Sum(x => x.RegularNetHours),
                RegularOTHours = x.Sum(x => x.RegularOTHours),
                RegularNDHours = x.Sum(x => x.RegularNDHours),
                RegularNDOTHours = x.Sum(x => x.RegularNDOTHours),

                RestDayHours = x.Sum(x => x.RestDayHours),
                RestDayOTHours = x.Sum(x => x.RestDayOTHours),
                RestDayNDHours = x.Sum(x => x.RestDayNDHours),
                RestDayNDOTHours = x.Sum(x => x.RestDayNDOTHours),

                LegalHolHours = x.Sum(x => x.LegalHolHours),
                LegalHolOTHours = x.Sum(x => x.LegalHolOTHours),
                LegalHolNightDiffHours = x.Sum(x => x.LegalHolNightDiffHours),
                LegalHolNightDiffOTHours = x.Sum(x => x.LegalHolNightDiffOTHours),

                SpecialHolHours = x.Sum(x => x.SpecialHolHours),
                SpecialHolOTHours = x.Sum(x => x.SpecialHolOTHours),
                SpecialHolNightDiffHours = x.Sum(x => x.SpecialHolNightDiffHours),
                SpecialHolNightDiffOTHours = x.Sum(x => x.SpecialHolNightDiffOTHours),

                RestLegalDayHours = x.Sum(xx => xx.RestLegalDayHours),
                RestLegalDayNDHours = x.Sum(xx => xx.RestLegalDayNDHours),
                RestLegalDayNDOTHours = x.Sum(xx => xx.RestLegalDayNDOTHours),
                RestLegalDayOTHours = x.Sum(xx => xx.RestLegalDayOTHours),

                RestSpecialDayHours = x.Sum(xx => xx.RestSpecialDayHours),
                RestSpecialDayNDHours = x.Sum(xx => xx.RestSpecialDayNDHours),
                RestSpecialDayNDOTHours = x.Sum(xx => xx.RestSpecialDayNDOTHours),
                RestSpecialDayOTHours = x.Sum(xx => xx.RestSpecialDayOTHours),

                DoubleLegalHours = x.Sum(xx => xx.DoubleLegalHours),
                DoubleLegalOTHours = x.Sum(xx => xx.DoubleLegalOTHours),
                DoubleLegalNDHours = x.Sum(xx => xx.DoubleLegalNDHours),
                DoubleLegalNDOTHours = x.Sum(xx => xx.DoubleLegalNDOTHours),

                RestDoubleLegalHours = x.Sum(xx => xx.RestDoubleLegalHours),
                RestDoubleLegalOTHours = x.Sum(xx => xx.RestDoubleLegalOTHours),
                RestDoubleLegalNDHours = x.Sum(xx => xx.RestDoubleLegalNDHours),
                RestDoubleLegalNDOTHours = x.Sum(xx => xx.RestDoubleLegalNDOTHours),

                AbsentCount = x.Sum(x => x.AbsentCount),
                LeaveHours = x.Sum(x => x.PaidLeaveHours),
                UnpaidLeaveHours = x.Sum(x => x.UnpaidLeaveHours),
            })
            .OrderBy(x => x.FullName)
            .ToListAsync(token);
    }
    public async Task<List<TardinessReportModel>> TardinessReportQuery(DTRRequestPayload payload, CancellationToken token)
    {

        var fromDate = payload.FromDate;
        var toDate = payload.ToDate;

        var query =
            from record in _uow.Repository.FindAll<DailyRecord>().AsNoTracking()
            where record.WorkDate >= fromDate && record.WorkDate <= toDate
                && (record.LateMinutes > 0 || record.UTMinutes > 0)
                && (payload.DepartmentId == null || record.DepartmentId == payload.DepartmentId.Value)
                && (payload.BranchId == null || record.BranchId == payload.BranchId.Value)
                && (payload.OperationAreaId == null || record.AreaId == payload.OperationAreaId.Value)
                && (payload.ClientId == null || record.ClientId == payload.ClientId.Value)
                && (payload.EmployeeId == null || record.EmployeeId == payload.EmployeeId.Value)
            join shift in Context.TimeShifts.AsNoTracking()
                on record.ShiftId equals shift.Id into shiftJoin
            from shift in shiftJoin.DefaultIfEmpty()
            select new TardinessReportModel
            {
                WorkDate = record.WorkDate,
                EmployeeNo = record.Employee != null ? record.Employee.EmployeeNo : string.Empty,
                FullName = record.FullName,
                Department = record.Employee != null && record.Employee.Department != null
                    ? record.Employee.Department.Name
                    : null,
                ScheduledIn = record.ShiftStartTime,
                ActualIn = record.StartTime,
                GracePeriodMinutes = shift != null ? shift.GracePeriodMinutes : 0,
                TardinessMinutes = record.LateMinutes + record.UTMinutes,
                DeductibleMinutes = record.LateMinutes > (shift != null ? shift.GracePeriodMinutes : 0)
                    ? record.LateMinutes + record.UTMinutes
                    : 0
            };

        return await query
            .OrderBy(x => x.WorkDate)
            .ThenBy(x => x.FullName)
            .ToListAsync(token);
    }

    public async Task<List<DTRDetailModel>> DTRDetailQuery(string batchCode, CancellationToken token)
    {
        return await _uow.Repository
             .FindAll<DailyRecord>()
             .AsNoTracking()
             .Where(x => x.BatchCode == batchCode)
             .Select(x => new DTRDetailModel
             {
                 Id = x.Id,
                 BatchCode = x.BatchCode,
                 ShiftId = x.ShiftId,
                 ShiftWorkingHour = x.ShiftWorkingHour,
                 HolCount = x.HolCount,
                 SPCount = x.SPCount,
                 FullName = x.FullName,
                 EmployeeId = x.EmployeeId,
                 WorkDate = x.WorkDate,
                 ShiftName = x.ShiftName,
                 ShiftStartTime = x.ShiftStartTime,
                 ShiftEndTime = x.ShiftEndTime,
                 StartTime = x.StartTime,
                 EndTime = x.EndTime,
                 WorkType = x.WorkType,
                 WorkTypeEnum = x.WorkTypeEnum,
                 LateMinutes = x.LateMinutes,
                 UTMinutes = x.UTMinutes,
                 OverMinutes = x.OverMinutes,
                 LateForOTMinutes = 0,
                 OBHours = x.OBHours,
                 PaidLeaveHours = x.PaidLeaveHours,
                 UnpaidLeaveHours = x.UnpaidLeaveHours,
                 LeavesInfo = x.LeavesInfo == null ? null : x.LeavesInfo.Select(li => new LeaveMetaDataModel
                 {
                     LeaveId = li.LeaveId,
                     Name = li.Name,
                     Hours = li.Hours,
                     StartDateTime = li.StartDateTime,
                     EndDateTime = li.EndDateTime,
                     PayType = li.PayType,
                 }).ToList(),
                 AbsentCount = x.AbsentCount,

                 RegularNetHours = x.RegularNetHours,
                 RegularOTHours = x.RegularOTHours,
                 RegularNDHours = x.RegularNDHours,
                 RegularNDOTHours = x.RegularNDOTHours,

                 RestDayHours = x.RestDayHours,
                 RestDayOTHours = x.RestDayOTHours,
                 RestDayNDHours = x.RestDayNDHours,
                 RestDayNDOTHours = x.RestDayNDOTHours,

                 LegalHolHours = x.LegalHolHours,
                 LegalHolOTHours = x.LegalHolOTHours,
                 LegalHolNightDiffHours = x.LegalHolNightDiffHours,
                 LegalHolNightDiffOTHours = x.LegalHolNightDiffOTHours,

                 SpecialHolHours = x.SpecialHolHours,
                 SpecialHolOTHours = x.SpecialHolOTHours,
                 SpecialHolNightDiffHours = x.SpecialHolNightDiffHours,
                 SpecialHolNightDiffOTHours = x.SpecialHolNightDiffOTHours,

                 RestLegalDayHours = x.RestLegalDayHours,
                 RestLegalDayOTHours = x.RestLegalDayOTHours,
                 RestLegalDayNDHours = x.RestLegalDayNDHours,
                 RestLegalDayNDOTHours = x.RestLegalDayNDOTHours,

                 RestSpecialDayHours = x.RestSpecialDayHours,
                 RestSpecialDayOTHours = x.RestSpecialDayOTHours,
                 RestSpecialDayNDHours = x.RestSpecialDayNDHours,
                 RestSpecialDayNDOTHours = x.RestSpecialDayNDOTHours,

                 DoubleLegalHours = x.DoubleLegalHours,
                 DoubleLegalOTHours = x.DoubleLegalOTHours,
                 DoubleLegalNDHours = x.DoubleLegalNDHours,
                 DoubleLegalNDOTHours = x.DoubleLegalNDOTHours,

                 RestDoubleLegalHours = x.RestDoubleLegalHours,
                 RestDoubleLegalOTHours = x.RestDoubleLegalOTHours,
                 RestDoubleLegalNDHours = x.RestDoubleLegalNDHours,
                 RestDoubleLegalNDOTHours = x.RestDoubleLegalNDOTHours,

                 Note = x.Note,
                 PostingDescription = x.PostingDescription,
                 UserId = x.UserId,
                 BranchId = x.BranchId,
                 DepartmentId = x.DepartmentId,
                 PayrollGroupId = x.PayrollGroupId,
                 ClientId = x.ClientId,
                 AreaId = x.AreaId,
                 Posted = x.Posted,
             })
             .OrderBy(x => x.FullName)
             .ThenBy(x => x.WorkDate)
             .ToListAsync(token);
    }

}
