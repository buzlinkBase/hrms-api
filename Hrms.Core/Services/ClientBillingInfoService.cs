using Hrms.Domain.Entities;
using Mapster;

namespace Hrms.Core.Services;

public class ClientBillingInfoService : BaseService<ClientBillingInfo>
{
    public ClientBillingInfoService(IUnitOfWorkService uow) : base(uow)
    {
    }

    public async Task<ClientBillingInfo?> FindByClientIdAsync(Guid clientId, CancellationToken token) =>
        await GetQueryable(x => x.ClientId == clientId).FirstOrDefaultAsync(token);

    public async Task SaveAsync(Guid clientId, UpdateClientBillingInfo payload, CancellationToken token)
    {
        var existing = await FindByClientIdAsync(clientId, token);
        var entity = payload.Adapt<ClientBillingInfo>();
        entity.Id = existing?.Id ?? Guid.Empty;
        entity.ClientId = clientId;
        await CreateOrUpdateAsync(entity, token);
        await CommitChangesAsync(token);
    }
}
