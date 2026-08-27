using Hrms.Domain.Entities;
using Mapster;


namespace Hrms.Core.Services;

public class HDMFContributionService : BaseService<HDMFContribution>
{
    private readonly IMapper _mapper;
    private readonly TypeAdapterConfig _config;

    public HDMFContributionService(IUnitOfWorkService uow,
        IMapper mapper,
        TypeAdapterConfig config) : base(uow)
    {
        _mapper = mapper;
        _config = config;
    }
    public async Task AddAsync(HDMFContribution model, CancellationToken token)
    {
        await CreateAsync(model, token);
    }
    public async Task UpdateAsync(HDMFContribution model, CancellationToken token)
    {
        await ModifyAsync(model, token);
    }
    public async Task AddOrUpdateAsync(HDMFContribution model, CancellationToken token)
    {
        await CreateOrUpdateAsync(model, token);
    }

    public async Task<Dictionary<EmployeeKey, List<HDMFContributionModel>>> LoadContributionsAsync(DateOnly fromDate,
        DateOnly toDate,
        CancellationToken token)
    {
        return await GetQueryable()
            .Where(x =>
                    (x.PayrollDate.Month == fromDate.Month && x.PayrollDate.Year == fromDate.Year)
                    //(x.PayrollDate.Month == toDate.Month && x.PayrollDate.Year == toDate.Year)
                    )
            //.Where(x => !(x.PayrollDate >= fromDate && x.PayrollDate <= toDate))
            .ProjectToType<HDMFContributionModel>(_config)
            .GroupBy(x => new EmployeeKey(x.EmployeeId))
            .ToDictionaryAsync(x => x.Key, x => x.ToList(), token)
            ;
    }
}

