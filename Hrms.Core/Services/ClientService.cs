
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

