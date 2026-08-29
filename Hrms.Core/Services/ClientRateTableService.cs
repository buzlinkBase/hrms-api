using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

public class ClientRateTableService : BaseService<ClientRateTable>
{
    public ClientRateTableService(IUnitOfWorkService uow) : base(uow)
    {
    }

    public Task<List<ClientRateTable>> FindByClientAsync(Guid clientId, CancellationToken token) =>
        GetQueryable(x => x.ClientId == clientId).ToListAsync(token);

    public async Task<Dictionary<Guid, List<ClientRateTable>>> FindByClientsAsync(HashSet<Guid> clientIds, CancellationToken token)
    {
        if (clientIds.Count == 0) return new();
        var rows = await GetQueryable(x => clientIds.Contains(x.ClientId)).ToListAsync(token);
        return rows.GroupBy(x => x.ClientId).ToDictionary(g => g.Key, g => g.ToList());
    }

    public async Task BulkReplaceForClientAsync(Guid clientId, List<ClientRateTable> incoming, CancellationToken token)
    {
        var existing = await GetQueryable(x => x.ClientId == clientId).ToListAsync(token);
        Context.ClientRateTables.RemoveRange(existing);
        await Context.ClientRateTables.AddRangeAsync(incoming, token);
        await CommitChangesAsync(token);
    }
}
