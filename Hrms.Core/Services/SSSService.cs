
using Hrms.Domain.Entities;
using Mapster;

namespace Hrms.Core.Services;

public class SSSService : BaseService<SSSTable>
{
    private readonly TypeAdapterConfig _config;
    private readonly IMapper _mapper;

    public SSSService(IUnitOfWorkService uow,
        TypeAdapterConfig config,
        IMapper mapper) : base(uow)
    {
        _config = config;
        _mapper = mapper;
    }
    public async Task AddAsync(SSSTable model, CancellationToken token)
    {
        // Effectivity dating/versioning is not in use — every new bracket defaults to the
        // minimum date so it always applies, regardless of what the client sends.
        model.EffectiveDate = DateOnly.MinValue;
        model.TotalContibution = model.EE + model.ER + model.EC;
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }
    public async Task UpdateAsync(UpdateSSS payload, CancellationToken token)
    {
        var existing = await Context.GovSSSes.FindAsync(new object[] { payload.Id }, token);
        if (existing == null)
        {
            throw new NotFoundException("Record not found");
        }
        var effectiveDate = existing.EffectiveDate;
        payload.Adapt(existing);
        existing.EffectiveDate = effectiveDate; // not editable via the UI anymore — preserve it
        existing.TotalContibution = existing.EE + existing.ER + existing.EC;
        await ModifyAsync(existing, token);
        await CommitChangesAsync(token);
    }

    public async Task<List<SSSTable>> FindAllAsync(CancellationToken token)
    {
        return await GetQueryable()
            .OrderBy(x=>x.RangeFrom)
            .ToListAsync(token);
    }

    public async Task<List<SSSModel>> LoadForPayrollrunAsync(CancellationToken token)
    {
        return await GetQueryable()
            .OrderBy(x => x.RangeFrom)
            .ProjectToType<SSSModel>(_config)
            .ToListAsync(token);
    }


    public async Task<SSSTable?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }
    public async Task Delete(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);
    }
}

