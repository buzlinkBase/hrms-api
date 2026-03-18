using Hrms.Domain.Entities;
using Mapster;

namespace Hrms.Core.Services;

public class TaxContributionService : BaseService<WTaxContribution>
{
    private readonly TypeAdapterConfig _config;
    private readonly IMapper _mapper;

    public TaxContributionService(IUnitOfWorkService uow,
        TypeAdapterConfig config,
        IMapper mapper) : base(uow)
    {
        _config = config;
        _mapper = mapper;
    }
    public async Task AddAsync(WTaxContribution model,
        CancellationToken token)
    {
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }
    public async Task UpdateAsync(WTaxContribution model,
        CancellationToken token)
    {
        await ModifyAsync(model, token);
        await CommitChangesAsync(token);

    }
    //public async Task AddOrUpdateAsync(WTaxContribution model, CancellationToken token)
    //{
    //    await CreateOrUpdateAsync(model, token);
    //}

    public async Task<Dictionary<EmployeeKey, List<WTaxContributionModel>>>
        LoadContributionsAsync(DateOnly fromDate, DateOnly toDate,
        CancellationToken token)
    {

        return await GetQueryable()
            .Where(x => (x.Date.Month == fromDate.Month && x.Date.Year == fromDate.Year) ||
                (x.Date.Month == toDate.Month && x.Date.Year == toDate.Year))
            .ProjectToType<WTaxContributionModel>(_config)
            .GroupBy(x => new EmployeeKey(x.EmployeeId))
            .ToDictionaryAsync(x => x.Key, x => x.ToList(), token)
            ;
    }

    //public async Task<TaxContribution?> FineOneAsync(Guid Id)
    //{
    //    return await GetOneAsync(Id);
    //}
    //public async Task Delete(Guid Id)
    //{
    //    await RemoveAsync(Id);
    //}
}

