using AutoMapper;
using AutoMapper.QueryableExtensions;
using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

public class PHICContributionService : BaseService<PHICContribution>
{
    private readonly IMapper _mapper;

    public PHICContributionService(IUnitOfWorkService uow, IMapper mapper) : base(uow)
    {
        _mapper = mapper;
    }
    public async Task AddAsync(PHICContribution model,CancellationToken token)
    {
        await CreateAsync(model,token);
    }
    public async Task UpdateAsync(PHICContribution model, CancellationToken token)
    {
        await ModifyAsync(model, token);
    }
    public async Task AddOrUpdateAsync(PHICContribution model, CancellationToken token)
    {
        await CreateOrUpdateAsync(model, token);
    }

    public async Task<Dictionary<EmployeeKey, List<PHICContributionModel>>> LoadContributionsAsync(DateOnly fromDate, DateOnly toDate,
        CancellationToken token)
    {
        return await GetQueryable()
            .Where(x => (x.PayrollDate.Month == fromDate.Month && x.PayrollDate.Year == fromDate.Year) ||
                (x.PayrollDate.Month == toDate.Month && x.PayrollDate.Year == toDate.Year))
            .ProjectTo<PHICContributionModel>(_mapper.ConfigurationProvider)
            .GroupBy(x => new EmployeeKey(x.EmployeeId))
            .ToDictionaryAsync(x => x.Key, x => x.ToList(), token)
            ;
    }

    //public async Task<PHICContribution?> FineOneAsync(Guid Id)
    //{
    //    return await GetOneAsync(Id);
    //}
    //public async Task Delete(Guid Id)
    //{
    //    await RemoveAsync(Id);
    //}
}

