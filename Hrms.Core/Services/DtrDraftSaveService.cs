using Hrms.Core.Messaging;
using Hrms.Core.Services.Approvals;
using Hrms.Domain.Entities;
using MassTransit;

namespace Hrms.Core.Services;

/// <summary>What a background DTR save did, for the notice the requester gets.</summary>
public sealed record DtrDraftSaveResult(int EmployeeCount, int RowCount, string? BatchCode)
{
    public bool NothingToSave => RowCount == 0;
}

/// <summary>
/// Save Draft for the Calculate DTR screen, by reference. The screen used to upload every
/// calculated row (tens of thousands for a big company -- past the proxy and Kestrel body limits);
/// now it sends the filter and the ticked employees, this service queues the work, and the worker
/// recomputes the rows with the same engine and saves them through
/// <see cref="DailyRecordService.SaveDraftAsync"/>.
/// </summary>
public class DtrDraftSaveService
{
    public const string DraftSavedStatusLabel = "Draft Saved";
    public const string SaveFailedStatusLabel = "Save Failed";
    public const string NothingToSaveStatusLabel = "Nothing To Save";

    private readonly DailyRecordService _records;
    private readonly IDtrRowSource _rows;
    private readonly ApprovalEngineService _approvals;
    private readonly IPublishEndpoint _publisher;
    private readonly IMapper _mapper;

    public DtrDraftSaveService(
        DailyRecordService records,
        IDtrRowSource rows,
        ApprovalEngineService approvals,
        IPublishEndpoint publisher,
        IMapper mapper)
    {
        _records = records;
        _rows = rows;
        _approvals = approvals;
        _publisher = publisher;
        _mapper = mapper;
    }

    /// <summary>
    /// Queues the save. Published through the outbox, so it leaves with the commit here -- a request
    /// that fails after this never leaves a save behind.
    /// </summary>
    public async Task QueueAsync(SaveDtrDraftRequest request, Guid userId, Guid generatedByEmployeeId, CancellationToken token)
    {
        await _publisher.Publish(new SaveDtrDraftRequested
        {
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            PayrollGroupId = request.PayrollGroupId,
            BranchId = request.BranchId,
            DepartmentId = request.DepartmentId,
            ClientId = request.ClientId,
            OperationAreaId = request.OperationAreaId,
            EmployeeIds = request.EmployeeIds.Distinct().ToList(),
            PostingDescription = request.PostingDescription,
            UserId = userId,
            GeneratedByEmployeeId = generatedByEmployeeId,
        }, token);
        await _records.CommitChangesAsync(token);
    }

    /// <summary>
    /// Recomputes the ticked employees' rows, drops the days that already have a saved DTR (the same
    /// "fills the missing days" rule the screen has always applied), and saves the rest as one draft
    /// batch with its approval started. The requester is told the outcome in the same commit.
    /// </summary>
    public async Task<DtrDraftSaveResult> SaveAsync(SaveDtrDraftRequested request, CancellationToken token)
    {
        var wanted = request.EmployeeIds.ToHashSet();
        var computed = (await _rows.ComputeAsync(new DTRRequestPayload
        {
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            PayrollGroupId = request.PayrollGroupId,
            BranchId = request.BranchId,
            DepartmentId = request.DepartmentId,
            ClientId = request.ClientId,
            OperationAreaId = request.OperationAreaId,
        }, token)).Where(r => wanted.Contains(r.EmployeeId)).ToList();

        var saved = (await _records.GetPayrollReadinessAsync(request.FromDate, request.ToDate, request.PayrollGroupId, token))
            .ToDictionary(x => x.EmployeeId, x => x.SavedDates.ToHashSet());
        var fresh = computed
            .Where(r => !(saved.TryGetValue(r.EmployeeId, out var days) && days.Contains(r.WorkDate)))
            .ToList();

        if (fresh.Count == 0)
        {
            await _approvals.NotifyApplicantAsync(ApprovalApplicationType.Dtr, Guid.Empty, request.GeneratedByEmployeeId,
                NothingToSaveStatusLabel, "Every selected day already has a saved DTR.", token, allowEmail: false);
            await _records.CommitChangesAsync(token);
            return new DtrDraftSaveResult(0, 0, null);
        }

        var rangeFrom = fresh.Min(x => x.WorkDate);
        var rangeTo = fresh.Max(x => x.WorkDate);
        var batchCode = await _records.BuildBatchCodeAsync(rangeFrom, rangeTo, request.PayrollGroupId, token);

        foreach (var row in fresh) row.PostingDescription = request.PostingDescription;
        var models = _mapper.Map<List<DailyRecord>>(fresh);
        foreach (var model in models)
        {
            model.UserId = request.UserId;
            model.BatchCode = batchCode;
        }

        var employees = fresh.Select(x => x.EmployeeId).Distinct().Count();
        await _records.SaveDraftAsync(models, batchCode, rangeFrom, rangeTo, request.PayrollGroupId,
            request.GeneratedByEmployeeId, token,
            beforeCommit: (batch, ct) => _approvals.NotifyApplicantAsync(ApprovalApplicationType.Dtr, batch.Id,
                request.GeneratedByEmployeeId, DraftSavedStatusLabel,
                $"{employees} employee(s), {models.Count} day record(s) saved as a draft awaiting approval.", ct, allowEmail: false,
                // The poster started the save, so the success notice isn't news to them; failures still reach them.
                actorEmployeeId: request.GeneratedByEmployeeId));

        return new DtrDraftSaveResult(employees, models.Count, batchCode);
    }

    /// <summary>
    /// Tells the requester a background save failed. Meant for a fresh scope: the save's own unit of
    /// work is abandoned (rolled back) by then, and this notice has to commit on its own.
    /// </summary>
    public async Task NotifyFailureAsync(Guid generatedByEmployeeId, string reason, CancellationToken token)
    {
        await _approvals.NotifyApplicantAsync(ApprovalApplicationType.Dtr, Guid.Empty, generatedByEmployeeId,
            SaveFailedStatusLabel, reason, token, allowEmail: false);
        await _records.CommitChangesAsync(token);
    }
}
