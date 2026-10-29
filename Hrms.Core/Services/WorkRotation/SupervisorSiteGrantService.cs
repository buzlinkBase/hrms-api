using Hrms.Domain.Entities;

namespace Hrms.Core.Services.WorkRotation;

public class SupervisorSiteGrantService : BaseService<SupervisorSiteGrant>
{
    public SupervisorSiteGrantService(IUnitOfWorkService uow) : base(uow)
    {
    }

    public Task<List<Guid>> GetAreaIdsAsync(Guid employeeId, CancellationToken token)
    {
        return GetQueryable()
            .Where(x => x.EmployeeId == employeeId)
            .Select(x => x.AreaId)
            .ToListAsync(token);
    }

    // Replaces the supervisor's whole list, so removing a site is just leaving it out.
    public async Task SetAsync(Guid employeeId, IEnumerable<Guid> areaIds, CancellationToken token)
    {
        var wanted = areaIds.Distinct().ToList();

        await Context.SupervisorSiteGrants
            .Where(x => x.EmployeeId == employeeId)
            .ExecuteDeleteAsync(token);

        if (wanted.Count > 0)
        {
            await CreateRangeAsync(
                wanted.Select(areaId => new SupervisorSiteGrant { EmployeeId = employeeId, AreaId = areaId }).ToList(),
                token);
        }
        await CommitChangesAsync(token);
    }
}
