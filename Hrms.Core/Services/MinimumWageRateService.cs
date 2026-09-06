using Hrms.Domain.Entities;
using Mapster;

namespace Hrms.Core.Services;

public class MinimumWageRateService : BaseService<MinimumWageRate>
{
    private readonly IMapper _mapper;

    public MinimumWageRateService(IUnitOfWorkService uow, IMapper mapper) : base(uow)
    {
        _mapper = mapper;
    }

    public async Task<MinimumWageRateModel> AddAsync(CreateMinimumWageRate model, CancellationToken token)
    {
        var rate = _mapper.Map<MinimumWageRate>(model);
        await CreateAsync(rate, token);
        await CommitChangesAsync(token);
        return _mapper.Map<MinimumWageRateModel>(rate);
    }

    public async Task<MinimumWageRateModel> UpdateAsync(UpdateMinimumWageRate model, CancellationToken token)
    {
        var existing = await Context.MinimumWageRates.FindAsync(new object[] { model.Id }, token)
            ?? throw new NotFoundException("Record not found");
        model.Adapt(existing);
        await ModifyAsync(existing, token);
        await CommitChangesAsync(token);
        return _mapper.Map<MinimumWageRateModel>(existing);
    }

    public async Task<List<MinimumWageRateModel>> FindAllAsync(CancellationToken token)
    {
        var rates = await GetQueryable()
            .OrderBy(x => x.RegionCode).ThenByDescending(x => x.EffectiveDate)
            .ToListAsync(token);
        return rates.Select(x => _mapper.Map<MinimumWageRateModel>(x)).ToList();
    }

    public async Task<MinimumWageRateModel?> FineOneAsync(Guid id, CancellationToken token)
    {
        var result = await GetOneAsync(id, token);
        return _mapper.Map<MinimumWageRateModel?>(result);
    }

    public async Task Delete(Guid id, CancellationToken token)
    {
        await RemoveAsync(id, token);
        await CommitChangesAsync(token);
    }
}
