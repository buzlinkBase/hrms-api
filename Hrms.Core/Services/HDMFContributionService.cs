using Hrms.Core.Extensions;
using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;
using Mapster;


namespace Hrms.Core.Services;

public class HDMFContributionService : BaseService<HDMFContribution>
{
    private readonly IMapper _mapper;
    private readonly TypeAdapterConfig _config;
    private readonly EmployeeService _employeeService;

    public HDMFContributionService(IUnitOfWorkService uow,
        IMapper mapper,
        TypeAdapterConfig config,
        EmployeeService employeeService) : base(uow)
    {
        _mapper = mapper;
        _config = config;
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
            GovIdNumber = employeeMap.TryGetValue(r.EmployeeId, out var e3) ? e3.HDMFNo : "",
            PayrollFrom = r.PayrollFrom,
            PayrollTo = r.PayrollTo,
            PayrollDate = r.PayrollDate,
            EmployeeShare = r.EmployeeShare,
            EmployerShare = r.EmployerShare,
            TotalContribution = r.TotalContribution,
        }).OrderBy(x => x.FullName).ToList();
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

    public async Task AddRangeAsync(List<HDMFContribution> models, CancellationToken token)
    {
        await Uow.Repository.AddRangeAsync(models, token);
        await Uow.SaveChangesAsync(token);
        await CommitChangesAsync(token);
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

    // Cascade cleanup for PayrollProcessorService.DeleteBatchAsync — see
    // SSSContributionService.DeleteByBatchIdAsync for why this must mirror it.
    public async Task DeleteByBatchIdAsync(Guid payrollBatchId, CancellationToken token) =>
        await ExecuteDeleteAsync(x => x.PayrollBatchId == payrollBatchId, token);
}

