using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;
using Mapster;

namespace Hrms.Core.Services;

public class PayrollInclusionDefaultsService : BaseService<PayrollInclusionDefaults>
{
    public PayrollInclusionDefaultsService(IUnitOfWorkService uow) : base(uow)
    {
    }

    public async Task<PayrollInclusionDefaults?> FineOneAsync(CancellationToken token)
    {
        return await GetQueryable().FirstOrDefaultAsync(token);
    }

    public async Task SaveAsync(UpdatePayrollInclusionDefaults payload, CancellationToken token)
    {
        var existing = await FineOneAsync(token);
        var entity = payload.Adapt<PayrollInclusionDefaults>();
        entity.Id = existing?.Id ?? Guid.Empty;
        await CreateOrUpdateAsync(entity, token);
        await CommitChangesAsync(token);
    }
}
