
using Hrms.Domain.Entities;
using Mapster;

namespace Hrms.Core.Services;

public class PHICService : BaseService<PHICTable>
{
    private readonly TypeAdapterConfig _config;
    private readonly IMapper _mapper;

    public PHICService(IUnitOfWorkService uow,
        TypeAdapterConfig config,
        IMapper mapper) : base(uow)
    {
        _config = config;
        _mapper = mapper;
    }
    public async Task AddAsync(PHICTable model, CancellationToken token)
    {
        // Effectivity dating/versioning is not in use — every new bracket defaults to the
        // minimum date so it always applies, regardless of what the client sends.
        model.EffectiveDate = DateOnly.MinValue;
        model.TotalContribution = model.EmployeeShare + model.EmployerShare;
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }
    public async Task UpdateAsync(UpdatePHIC payload, CancellationToken token)
    {
        var existing = await Context.GovPHICs.FindAsync(new object[] { payload.Id }, token);
        if (existing == null)
        {
            throw new NotFoundException("Record not found");
        }
        var effectiveDate = existing.EffectiveDate;
        payload.Adapt(existing);
        existing.EffectiveDate = effectiveDate; // not editable via the UI anymore — preserve it
        existing.TotalContribution = existing.EmployeeShare + existing.EmployerShare;
        await ModifyAsync(existing, token);
        await CommitChangesAsync(token);
    }

    public async Task<List<PHICTable>> FindAllAsync(CancellationToken token)
    {
        return await GetQueryable()
            .OrderBy(x => x.MinSalaryBase)
            .ToListAsync(token);
    }

    public async Task<List<PHICModel>> LoadForPayrollrunAsync(CancellationToken token)
    {
        return await GetQueryable()
            .OrderBy(x => x.MinSalaryBase)
            .ProjectToType<PHICModel>(_config)
            .ToListAsync(token);
    }


    public async Task<PHICTable?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }
    public async Task Delete(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);
    }
    public async Task DeleteAllAsync(CancellationToken token)
    {
        await RemoveAllAsync();
        await CommitChangesAsync(token);
    }
}

