using System.Linq.Expressions;
using DocumentFormat.OpenXml.Office2010.ExcelAc;
using Hrms.Core.Services.Approvals;
using Hrms.Domain.Entities;
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
        var approverId = approverEmployeeId ?? throw new InvalidOperationException(
            "Your account isn't linked to an Employee record, so this approval action can't be recorded. Contact an admin to link your account.");

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
        var approverId = approverEmployeeId ?? throw new InvalidOperationException(
            "Your account isn't linked to an Employee record, so this approval action can't be recorded. Contact an admin to link your account.");

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
        return $"DTR{rangeFrom.ToString("MMMddyyyy")}-{rangeTo.ToString("MMMddyyyy")}{pgSegment} TS:{ts}";
    }
    // Direct delete -- only for a batch that isn't posted yet (ForApproval/Declined), where
    // nothing is final and no approval is needed. An already-posted batch must go through
    // RequestDeletionAsync/ApproveDeletionAsync instead -- see below.
    public async Task DeleteAsync(string batchCode, CancellationToken token)
    {
        var hasPosted = await _uow.Repository
            .Find<DailyRecord>(x => x.BatchCode == batchCode && x.Posted)
            .AnyAsync(token);
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
        var usedBatchCodes = await _payrollBatchService.GetUsedDtrBatchCodesAsync(token);
        if (usedBatchCodes.Contains(batchCode))
            throw new ValidationException("Cannot delete this DTR batch — payroll has already been generated and saved from it. Delete the payroll run first if you need to regenerate it.");

        await ExecuteBatchDeletionAsync(batchCode, token);
    }

    // The actual cascade, shared by the direct-delete path above (an unposted draft) and
    // ApproveDeletionAsync below (an already-posted batch, once its deletion request itself
    // clears approval).
    private async Task ExecuteBatchDeletionAsync(string batchCode, CancellationToken token)
    {
        await ExecuteDeleteAsync(x => x.BatchCode == batchCode, token);
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
        if (usedBatchCodes.Contains(batch.BatchCode))
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
        var approverId = approverEmployeeId ?? throw new InvalidOperationException(
            "Your account isn't linked to an Employee record, so this approval action can't be recorded. Contact an admin to link your account.");

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
        var approverId = approverEmployeeId ?? throw new InvalidOperationException(
            "Your account isn't linked to an Employee record, so this approval action can't be recorded. Contact an admin to link your account.");

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
    public async Task DeleteAsync(DateRangePayload payload, List<Guid> employeeIds,
        CancellationToken token)
    {
        await ExecuteDeleteAsync(x =>
        employeeIds.Contains(x.EmployeeId) &&
        x.WorkDate >= payload.FromDate && x.WorkDate <= payload.ToDate, token);
        await CommitChangesAsync(token);
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

        var recordsList = await GetQueryable(x => batchCodes.Contains(x.BatchCode!))
             .AsNoTracking()
             .ProjectToType<DailyRecordRunModel>(_config)
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

        var rows = await GetQueryable(x => batchCodes.Contains(x.BatchCode!) && x.LeavesInfo != null && x.LeavesInfo.Any())
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
                .AsNoTracking()
                .Include(x => x.Employee);
        }
        else
        {
            query = GetQueryable(GetQueryExpression(payload))
                .AsNoTracking()
                .Include(x => x.Employee);
        }

        return await query
            .ProjectToType<DailyRecordRunModel>(_config)
            .Where(x => x.EmployeeId != null)
            .GroupBy(x => new EmployeeKey(x.EmployeeId))
            .ToDictionaryAsync(x => x.Key, x => x.ToList(), token);
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
    public async Task PostAsync(string batchCode, CancellationToken token, bool commit = true)
    {
        var records = await _uow.Repository
            .Find<DailyRecord>(x => x.BatchCode == batchCode && !x.Posted)
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
        var records = await _uow.Repository
            .Find<DailyRecord>(x => x.BatchCode == batchCode && x.Posted)
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
        var records = await Context.DailyTimeRecords
            .Where(x => x.WorkDate >= fromDate && x.WorkDate <= toDate && x.BatchCode != null)
            .GroupBy(x => x.BatchCode)
            .Select(x => new
            {
                x.Key,
                x.First().WorkDate,
                x.First().EmployeeId,
                x.First().Posted,
                x.First().PostingDescription,
                x.First().CreatedAt
            })
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(token);

        var usedBatchCodes = await _payrollBatchService.GetUsedDtrBatchCodesAsync(token);

        // Left-join by BatchCode (a plain string, not a real FK) -- a legacy batch that predates
        // DTRBatch has no matching row here and is reported as already-Approved, see BatchesModel.
        var batchCodes = records.Select(x => x.Key).Where(x => x != null).Select(x => x!).Distinct().ToList();
        var dtrBatches = await _dtrBatchService.FindByCodesAsync(batchCodes, token);
        var dtrBatchesByCode = dtrBatches.ToDictionary(x => x.BatchCode);

        return records
         .GroupBy(x => x.Key)
         .Select(g =>
         {
             dtrBatchesByCode.TryGetValue(g.Key ?? "", out var dtrBatch);
             return new BatchesModel
             {
                 Id = dtrBatch?.Id,
                 Code = g.Key,
                 FromDate = g.Min(x => x.WorkDate),
                 ToDate = g.Max(x => x.WorkDate),
                 EmployeeCount = g.Select(x => x.EmployeeId).Distinct().Count(),
                 IsPosted = g.All(x => x.Posted),
                 PostingDescription = g.FirstOrDefault()?.PostingDescription ?? "",
                 IsPayrollGenerated = g.Key != null && usedBatchCodes.Contains(g.Key),
                 ApprovalStatus = dtrBatch?.ApprovalStatus ?? ApprovalStatus.Approved,
                 GeneratedByEmployeeId = dtrBatch?.GeneratedByEmployeeId,
                 PayrollGroupId = dtrBatch?.PayrollGroupId,
                 GeneratedAt = dtrBatch?.CreatedAt,
                 PendingDeletion = dtrBatch?.PendingDeletion ?? false,
                 RequestedDeletionByEmployeeId = dtrBatch?.RequestedDeletionByEmployeeId,
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
