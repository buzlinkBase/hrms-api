using Hrms.Core.Services.Approvals;
using Hrms.Domain.Entities;
using Mapster;

namespace Hrms.Core.Services;

public class OvertimeApplicationService : BaseService<OverTimeApplication>
{
    private readonly ApprovalEngineService _approvalEngine;

    public OvertimeApplicationService(IUnitOfWorkService uow, ApprovalEngineService approvalEngine) : base(uow)
    {
        _approvalEngine = approvalEngine;
    }

    public async Task AddAsync(OverTimeApplication model, CancellationToken token)
    {
        await CreateAsync(model, token);
        if (model.ApprovalStatus == ApprovalStatus.ForApproval)
            await _approvalEngine.StartAsync(ApprovalApplicationType.Overtime, model.Id, model.EmployeeId, token);
        await CommitChangesAsync(token);
    }

    // Plain field edit -- never changes ApprovalStatus, whatever the payload says. Status only
    // moves through ApproveAsync/DeclineAsync (the approval engine), so an edit can't bypass the
    // configured approvers (e.g. flip Declined -> Approved).
    public async Task UpdateAsync(UpdateOvertimeApplication payload, CancellationToken token)
    {
        var existing = await Context.OTApplications.FindAsync(new object[] { payload.Id }, token)
            ?? throw new NotFoundException("Record not found");

        var previousStatus = existing.ApprovalStatus;
        payload.Adapt(existing);
        existing.ApprovalStatus = previousStatus;

        // Changed content goes back to the first approver if anyone already signed off.
        if (existing.ApprovalStatus == ApprovalStatus.ForApproval)
            await _approvalEngine.RestartIfProgressedAsync(ApprovalApplicationType.Overtime, existing.Id, token);

        await ModifyAsync(existing, token);
        await CommitChangesAsync(token);
    }

    // Self-service edit: only the caller's own, still-pending application. See
    // MeController.UpdateMyOvertimeApplication.
    public async Task UpdateOwnAsync(UpdateOvertimeApplication payload, Guid employeeId, CancellationToken token)
    {
        var existing = await Context.OTApplications.AsNoTracking().FirstOrDefaultAsync(x => x.Id == payload.Id, token);
        if (existing == null || existing.EmployeeId != employeeId)
            throw new NotFoundException("Overtime application not found.");
        if (existing.ApprovalStatus != ApprovalStatus.ForApproval)
            throw new InvalidOperationException("Only applications still awaiting approval can be edited.");

        await UpdateAsync(payload, token);
    }

    public Task ApproveAsync(Guid id, Guid? approverEmployeeId, bool approverHasOverride, string? note, CancellationToken token) =>
        RecordDecisionAsync(id, ApprovalActionType.Approved, approverEmployeeId, approverHasOverride, note, token);

    public Task DeclineAsync(Guid id, Guid? approverEmployeeId, bool approverHasOverride, string? note, CancellationToken token) =>
        RecordDecisionAsync(id, ApprovalActionType.Declined, approverEmployeeId, approverHasOverride, note, token);

    private async Task RecordDecisionAsync(
        Guid id, ApprovalActionType action, Guid? approverEmployeeId, bool approverHasOverride, string? note, CancellationToken token)
    {
        var existing = await Context.OTApplications.FindAsync(new object[] { id }, token)
            ?? throw new NotFoundException("Record not found");
        if (existing.ApprovalStatus != ApprovalStatus.ForApproval)
            throw new InvalidOperationException("This overtime application is not awaiting approval.");

        var approverId = approverEmployeeId ?? throw new AccountNotLinkedException("this approval action can't be recorded");

        var result = await _approvalEngine.RecordActionAsync(
            ApprovalApplicationType.Overtime, existing.Id, existing.EmployeeId,
            approverId, approverHasOverride, action, note, token);

        existing.ApprovalStatus = ApprovalEngineService.MapInstanceStatus(result.InstanceStatus);
        await ModifyAsync(existing, token);
        await CommitChangesAsync(token);
    }
    public async Task<List<OverTimeApplication>> FindAllAsync(CancellationToken token, DateOnly? from = null, DateOnly? to = null)
    {
        var query = GetQueryable();
        if (from.HasValue) query = query.Where(x => DateOnly.FromDateTime(x.CreatedAt) >= from.Value);
        if (to.HasValue) query = query.Where(x => DateOnly.FromDateTime(x.CreatedAt) <= to.Value);
        return await query.ToListAsync(token);
    }
    // Self-service "My Overtime Applications" — every status, newest first, scoped to one
    // employee. See MeController.GetMyOvertimeApplications.
    public Task<List<OverTimeApplication>> FindAllForEmployeeAsync(Guid employeeId, CancellationToken token)
    {
        return GetQueryable(x => x.EmployeeId == employeeId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(token);
    }

    public async Task<OverTimeApplication?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneReadOnlyAsync(Id, token);
    }

    // Self-service cancel of the employee's own still-pending application. See
    // MeController's overtime-applications/{id}/withdraw endpoint.
    public async Task WithdrawAsync(Guid id, Guid employeeId, CancellationToken token)
    {
        var existing = await GetOneAsync(id, token);
        if (existing == null || existing.EmployeeId != employeeId)
            throw new NotFoundException("Application not found");
        if (existing.ApprovalStatus != ApprovalStatus.ForApproval)
            throw new InvalidOperationException("Only pending applications can be withdrawn.");

        existing.ApprovalStatus = ApprovalStatus.Withdrawn;
        await ModifyAsync(existing, token);
        await CommitChangesAsync(token);
    }
    public async Task Delete(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);
    }

    public async Task<Dictionary<OTKey, OverTimeApplication?>> FindByDateRangeAsync(
      DateOnly from,
      DateOnly to,
      HashSet<Guid> employeeIds,
      CancellationToken token)
    {
        var results = await GetQueryable(x =>
                x.ApprovalStatus == ApprovalStatus.Approved &&
                x.OTDate >= from && x.OTDate <= to &&
                employeeIds.Contains(x.EmployeeId))
            .GroupBy(a => new { a.EmployeeId, a.OTDate })
            .Select(g => new
            {
                Key = g.Key,
                Value = g.OrderBy(x => x.OTDate).FirstOrDefault()
            })
            .ToListAsync(token);

        return results.ToDictionary(
            x => new OTKey(x.Key.EmployeeId, x.Key.OTDate),
            x => x.Value
        );
    }
}
public readonly record struct OTKey(Guid EmpId, DateOnly OTDate);
