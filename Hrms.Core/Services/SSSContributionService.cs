using Hrms.Domain.Entities;
using Mapster;

namespace Hrms.Core.Services;

public class SSSContributionService : BaseService<SSSContribution>
{
    private readonly TypeAdapterConfig _config;
    private readonly IMapper _mapper;

    public SSSContributionService(IUnitOfWorkService uow,
        TypeAdapterConfig config,
        IMapper mapper) : base(uow)
    {
        _config = config;
        _mapper = mapper;
    }
    public async Task AddAsync(SSSContribution model, CancellationToken token)
    {
        await CreateAsync(model, token);
    }
    public async Task UpdateAsync(SSSContribution model, CancellationToken token)
    {
        await ModifyAsync(model, token);
    }
    public async Task AddOrUpdateAsync(SSSContribution model, CancellationToken token)
    {
        await CreateOrUpdateAsync(model, token);
    }

    public async Task<Dictionary<EmployeeKey, List<SSSContributionModel>>> LoadContributionsAsync(DateOnly fromDate, DateOnly toDate,
        CancellationToken token)
    {
        var data = await GetQueryable()
                .Where(x =>
                    (x.PayrollDate.Month == fromDate.Month && x.PayrollDate.Year == fromDate.Year) ||
                    (x.PayrollDate.Month == toDate.Month && x.PayrollDate.Year == toDate.Year))
                .Where(x => !(x.PayrollDate >= fromDate && x.PayrollDate <= toDate))
            .ProjectToType<SSSContributionModel>(_config)
            .GroupBy(x => new EmployeeKey(x.EmployeeId))
            .ToDictionaryAsync(x => x.Key, x => x.ToList(), token)
            ;
        return data;
    }

    //public async Task<SSSContribution?> FineOneAsync(Guid Id)
    //{
    //    return await GetOneAsync(Id);
    //}
    //public async Task Delete(Guid Id)
    //{
    //    await RemoveAsync(Id);
    //}

}

