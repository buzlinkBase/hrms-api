using Hrms.Core.Extensions;
using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;
using Mapster;

namespace Hrms.Core.Services;

public class PHICContributionService : BaseService<PHICContribution>
{
    private readonly TypeAdapterConfig _config;
    private readonly IMapper _mapper;
    private readonly EmployeeService _employeeService;

    public PHICContributionService(IUnitOfWorkService uow,
        TypeAdapterConfig config,
        IMapper mapper,
        EmployeeService employeeService) : base(uow)
    {
        _config = config;
        _mapper = mapper;
        _employeeService = employeeService;
    }

    public async Task<List<ContributionRemittanceModel>> GetRemittanceReportAsync(DateOnly from, DateOnly to, CancellationToken token)
    {
        var rows = await GetQueryable(x => x.PayrollDate >= from && x.PayrollDate <= to).ToListAsync(token);
        var employees = await _employeeService.FindByIds(rows.Select(x => x.EmployeeId).Distinct().ToList(), token);
        var employeeMap = employees.ToDictionary(x => x.Id);
        return rows.Select(r => new ContributionRemittanceModel
        {
            EmployeeId = r.EmployeeId,
            EmployeeNo = employeeMap.TryGetValue(r.EmployeeId, out var e) ? e.EmployeeNo : "",
            FullName = employeeMap.TryGetValue(r.EmployeeId, out var e2) ? e2.FullName() : "",
            GovIdNumber = employeeMap.TryGetValue(r.EmployeeId, out var e3) ? e3.PHICNo : "",
            PayrollFrom = r.PayrollFrom,
            PayrollTo = r.PayrollTo,
            PayrollDate = r.PayrollDate,
            EmployeeShare = r.EmployeeShare,
            EmployerShare = r.EmployerShare,
            TotalContribution = r.TotalContribution,
        }).OrderBy(x => x.FullName).ToList();
    }
    public async Task AddAsync(PHICContribution model, CancellationToken token)
    {
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }
    public async Task UpdateAsync(PHICContribution model, CancellationToken token)
    {
        await ModifyAsync(model, token);
        await CommitChangesAsync(token);
    }

    public async Task AddRangeAsync(List<PHICContribution> models, CancellationToken token)
    {
        await Uow.Repository.AddRangeAsync(models, token);
        await Uow.SaveChangesAsync(token);
        await CommitChangesAsync(token);
    }


    public async Task<Dictionary<EmployeeKey, List<PHICContributionModel>>> LoadContributionsAsync(DateOnly fromDate, DateOnly toDate,
        CancellationToken token)
    {
        return await GetQueryable()
           .Where(x =>
                    (x.PayrollDate.Month == fromDate.Month && x.PayrollDate.Year == fromDate.Year)
                    //(x.PayrollDate.Month == toDate.Month && x.PayrollDate.Year == toDate.Year)
                    )
            //.Where(x => !(x.PayrollDate >= fromDate && x.PayrollDate <= toDate))
            .ProjectToType<PHICContributionModel>(_config)
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

