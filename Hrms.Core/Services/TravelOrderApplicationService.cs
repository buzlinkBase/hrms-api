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

    public async Task UpdateAsync(
        UpdateTravelOrderApplication payload,
        CancellationToken token,
        Guid? approverEmployeeId = null,
        bool approverHasOverride = false)
    {
        var existing = await Context.TravelOrderApplications.FindAsync(new object[] { payload.Id }, token);
        if (existing == null)
        {
            throw new NotFoundException("Record not found");
        }

        var previousStatus = existing.ApprovalStatus;
        var isApprovalAction = previousStatus == ApprovalStatus.ForApproval
            && payload.ApprovalStatus is ApprovalStatus.Approved or ApprovalStatus.Declined;

        payload.Adapt(existing);

        if (isApprovalAction)
        {
            existing.ApprovalStatus = previousStatus;

            var approverId = approverEmployeeId ?? throw new InvalidOperationException(
                "Your account isn't linked to an Employee record, so this approval action can't be recorded. Contact an admin to link your account.");

            var action = payload.ApprovalStatus == ApprovalStatus.Approved
                ? ApprovalActionType.Approved
                : ApprovalActionType.Declined;

            var result = await _approvalEngine.RecordActionAsync(
                ApprovalApplicationType.OfficialBusiness, existing.Id, existing.EmployeeId,
                approverId, approverHasOverride, action, payload.Note, token);

            existing.ApprovalStatus = ApprovalEngineService.MapInstanceStatus(result.InstanceStatus);
        }

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
        return await GetOneAsync(id, token);
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
