using Hrms.Core.Services.Approvals;
using Hrms.Domain.Entities;
using Mapster;

namespace Hrms.Core.Services;

public class TravelOrderApplicationService : BaseService<TravelOrderApplication>
{
    private readonly ApprovalEngineService _approvalEngine;

    public TravelOrderApplicationService(IUnitOfWorkService uow, ApprovalEngineService approvalEngine) : base(uow)
    {
        _approvalEngine = approvalEngine;
    }

    public async Task AddAsync(TravelOrderApplication model, CancellationToken token)
    {
        model.ApplicationDate = DateTime.UtcNow;
        await CreateAsync(model, token);
        if (model.ApprovalStatus == ApprovalStatus.ForApproval)
            await _approvalEngine.StartAsync(ApprovalApplicationType.OfficialBusiness, model.Id, model.EmployeeId, token);
        await CommitChangesAsync(token);
    }

    // Plain field edit -- never changes ApprovalStatus (see OvertimeApplicationService.UpdateAsync).
    public async Task UpdateAsync(UpdateTravelOrderApplication payload, CancellationToken token)
    {
        var existing = await Context.TravelOrderApplications.FindAsync(new object[] { payload.Id }, token)
            ?? throw new NotFoundException("Record not found");

        var previousStatus = existing.ApprovalStatus;
        payload.Adapt(existing);
        existing.ApprovalStatus = previousStatus;

        // Changed content goes back to the first approver if anyone already signed off.
        if (existing.ApprovalStatus == ApprovalStatus.ForApproval)
            await _approvalEngine.RestartIfProgressedAsync(ApprovalApplicationType.OfficialBusiness, existing.Id, token);

        await ModifyAsync(existing, token);
        await CommitChangesAsync(token);
    }

    // Self-service edit: only the caller's own, still-pending application. See
    // MeController.UpdateMyTravelOrderApplication.
    public async Task UpdateOwnAsync(UpdateTravelOrderApplication payload, Guid employeeId, CancellationToken token)
    {
        var existing = await Context.TravelOrderApplications.AsNoTracking().FirstOrDefaultAsync(x => x.Id == payload.Id, token);
        if (existing == null || existing.EmployeeId != employeeId)
            throw new NotFoundException("Official business application not found.");
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
        var existing = await Context.TravelOrderApplications.FindAsync(new object[] { id }, token)
            ?? throw new NotFoundException("Record not found");
        if (existing.ApprovalStatus != ApprovalStatus.ForApproval)
            throw new InvalidOperationException("This official business application is not awaiting approval.");

        var approverId = approverEmployeeId ?? throw new AccountNotLinkedException("this approval action can't be recorded");

        var result = await _approvalEngine.RecordActionAsync(
            ApprovalApplicationType.OfficialBusiness, existing.Id, existing.EmployeeId,
            approverId, approverHasOverride, action, note, token);

        existing.ApprovalStatus = ApprovalEngineService.MapInstanceStatus(result.InstanceStatus);
        await ModifyAsync(existing, token);
        await CommitChangesAsync(token);
    }

    public async Task<List<TravelOrderApplication>> FindAllAsync(CancellationToken token, DateOnly? from = null, DateOnly? to = null)
    {
        var query = GetQueryable();
        if (from.HasValue) query = query.Where(x => DateOnly.FromDateTime(x.CreatedAt) >= from.Value);
        if (to.HasValue) query = query.Where(x => DateOnly.FromDateTime(x.CreatedAt) <= to.Value);
        return await query.ToListAsync(token);
    }

    public async Task<Dictionary<TravelKey, List<TravelOrderApplication>>> FindByDateRangeAsync(DateOnly fromDate, DateOnly toDate, HashSet<Guid> employeeIds, CancellationToken token)
    {
        var data = await _uow.Repository
                .Find<TravelOrderApplication>(x => x.StartDate >= fromDate
                    && x.EndDate <= toDate
                    && employeeIds.Contains(x.EmployeeId)
                    && x.ApprovalStatus == ApprovalStatus.Approved
                    )
                 .AsNoTracking()
                 .GroupBy(a => new TravelKey(a.EmployeeId))
                 .ToDictionaryAsync(g => g.Key, g => g.OrderBy(x => x.StartDate).ToList(), token);
        ;
        return data;
    }

    // Self-service "My Official Business Applications" — every status, newest first, scoped to
    // one employee. See MeController.GetMyTravelOrderApplications.
    public Task<List<TravelOrderApplication>> FindAllForEmployeeAsync(Guid employeeId, CancellationToken token)
    {
        return GetQueryable(x => x.EmployeeId == employeeId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(token);
    }

    public async Task<TravelOrderApplication?> FineOneAsync(Guid id, CancellationToken token)
    {
        return await GetOneReadOnlyAsync(id, token);
    }

    // Self-service cancel of the employee's own still-pending application. See
    // MeController's travel-order-applications/{id}/withdraw endpoint.
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

    public async Task Delete(Guid id, CancellationToken token)
    {
        await RemoveAsync(id, token);
        await CommitChangesAsync(token);
    }
}
public readonly record struct TravelKey(Guid EmpId);
