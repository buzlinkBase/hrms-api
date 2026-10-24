
using Hrms.Core.Services.ClientPortal;
using Hrms.Core.Validations;
using Hrms.Domain.Entities;
using Mapster;

namespace Hrms.Core.Services;

public class ClientService : BaseService<Client>
{
    private readonly ClientPortalClientChangeService? _portal;

    // The portal hears about every client change so its copy (same id) follows HRIS. Optional so a ClientService built without it still works.
    public ClientService(IUnitOfWorkService uow, ClientPortalClientChangeService? portal = null) : base(uow)
    {
        _portal = portal;
    }

    // Staged before the caller's commit, so the message rides that one commit through the outbox (never after it).
    private async Task StagePortalAsync(Client client, string changeType, CancellationToken token)
    {
        if (_portal is not null)
            await _portal.StageAsync(client, changeType, token);
    }
    protected override async Task<EvaluationResult> CreateValidatorAsync(Client model, CancellationToken token)
    {
        var validator = await new ClientValidator().ValidateAsync(model, token);
        if (!validator.IsValid)
        {
            return EvaluationResult.Fail(validator.Errors);
        }
        if (await NameIsTakenAsync(x => x.Name, model.Name, model.Id, token))
        {
            return EvaluationResult.Fail($"A client named '{model.Name.Trim()}' already exists.");
        }
        return await base.CreateValidatorAsync(model, token);
    }
    public async Task AddAsync(Client model, CancellationToken token)
    {
        await CreateAsync(model, token);
        await StagePortalAsync(model, ClientPortalClientChangeService.Created, token);
        await CommitChangesAsync(token);
    }
    // Creates the client and tags its areas in the same commit.
    public async Task AddAsync(Client model, List<Guid>? areaIds, bool reassignAreas, CancellationToken token)
    {
        await CreateAsync(model, token);
        await SetAreasAsync(model, areaIds, reassignAreas, token);
        await StagePortalAsync(model, ClientPortalClientChangeService.Created, token);
        await CommitChangesAsync(token);
    }

    // Tags exactly areaIds to the client (null = leave as is). Staged only -- the caller commits.
    // A site already tagged to a different client is rejected unless reassign is set, so one
    // client can't silently take another's billing site.
    public async Task SetAreasAsync(Client client, List<Guid>? areaIds, bool reassign, CancellationToken token)
    {
        if (areaIds is null) return;
        var wanted = areaIds.ToHashSet();
        var areas = await Context.Areas.AsTracking()
            .Where(a => wanted.Contains(a.Id) || a.ClientId == client.Id)
            .ToListAsync(token);

        if (wanted.Except(areas.Select(a => a.Id)).Any())
        {
            throw new NotFoundException("One or more areas were not found");
        }
        var taken = areas.Where(a => wanted.Contains(a.Id) && a.ClientId != null && a.ClientId != client.Id).ToList();
        if (taken.Count > 0 && !reassign)
        {
            var names = string.Join(", ", taken.Select(a => a.Name));
            throw new ValidationException($"These areas are already tagged to another client: {names}.");
        }

        foreach (var area in areas)
        {
            if (wanted.Contains(area.Id))
            {
                area.Client = client;
            }
            else
            {
                area.Client = null;
                area.ClientId = null;
            }
        }
    }

    // The client list/detail: each client with the areas tagged to it.
    public async Task<List<Client>> FindAllWithAreasAsync(CancellationToken token)
    {
        return await GetQueryable().Include(x => x.Areas).ToListAsync(token);
    }
    public async Task<Client?> FindOneWithAreasAsync(Guid id, CancellationToken token)
    {
        return await GetQueryable(x => x.Id == id).Include(x => x.Areas).FirstOrDefaultAsync(token);
    }
    public async Task AddRangeAsync(List<Client> models, CancellationToken token)
    {
        await CreateRangeAsync(models, token);
        // The caller commits; the messages ride that commit.
        foreach (var model in models)
            await StagePortalAsync(model, ClientPortalClientChangeService.Created, token);
    }
    public async Task UpdateAsync(UpdateClient payload, CancellationToken token)
    {
        var existing = await Context.Clients.FindAsync(new object[] { payload.Id }, token);
        if (existing == null)
        {
            throw new NotFoundException("Record not found");
        }
        payload.Adapt(existing);
        await ModifyAsync(existing, token);
        await SetAreasAsync(existing, payload.AreaIds, payload.ReassignAreas, token);
        await StagePortalAsync(existing, ClientPortalClientChangeService.Updated, token);
        await CommitChangesAsync(token);

    }
    public async Task AddOrUpdateAsync(Client model, CancellationToken token)
    {
        await CreateOrUpdateAsync(model, token);
        // Created or updated, the portal upserts by id, so one change type covers both.
        await StagePortalAsync(model, ClientPortalClientChangeService.Updated, token);
        await CommitChangesAsync(token);
    }
    public async Task<List<Client>> FindAllAsync(CancellationToken token)
    {
        return await GetQueryable().ToListAsync(token);
    }
    // Client.RetirementDaysPerYear per client, for PayrollRangeContextComposerService's
    // per-run CalculatorPayload.ClientRetirementDaysPerYear lookup -- see
    // EmployeePayrollLineService.ComputeRetirementAccrual.
    public async Task<Dictionary<Guid, decimal?>> FindRetirementDaysPerYearAsync(HashSet<Guid> clientIds, CancellationToken token)
    {
        if (clientIds.Count == 0) return new();
        return await GetQueryable(x => clientIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.RetirementDaysPerYear, token);
    }
    // Bulk client-name lookup for reports that list many clients' employees at once (e.g.
    // Payroll Summary print) — one query instead of N+1 per-row lookups.
    public async Task<Dictionary<Guid, string>> FindNamesByIdsAsync(HashSet<Guid> clientIds, CancellationToken token)
    {
        if (clientIds.Count == 0) return new();
        return await GetQueryable(x => clientIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, token);
    }

    public async Task<Client?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneReadOnlyAsync(Id, token);
    }
    public async Task DeleteAsync(Guid Id, CancellationToken token)
    {
        var existing = _portal is null ? null : await GetOneReadOnlyAsync(Id, token);
        await RemoveAsync(Id, token);
        if (existing is not null)
            await StagePortalAsync(existing, ClientPortalClientChangeService.Deleted, token);
        await CommitChangesAsync(token);
    }
}

