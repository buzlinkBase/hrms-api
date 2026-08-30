using Hrms.Core.Extensions;
using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;
using Mapster;

namespace Hrms.Core.Services;

public class TaxContributionService : BaseService<WTaxContribution>
{
    private readonly TypeAdapterConfig _config;
    private readonly IMapper _mapper;
    private readonly EmployeeService _employeeService;

    public TaxContributionService(IUnitOfWorkService uow,
        TypeAdapterConfig config,
        IMapper mapper,
        EmployeeService employeeService) : base(uow)
    {
        _config = config;
        _mapper = mapper;
        _employeeService = employeeService;
    }

    // WTaxContribution.PayrollDate is set from PostingPeriod (the BIR-specific cross-month
    // credit date), not StatutoryCreditDate — see PayrollProcessorService.SaveStatutoryContributionsAsync.
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
            GovIdNumber = employeeMap.TryGetValue(r.EmployeeId, out var e3) ? e3.TIN : "",
            PayrollFrom = r.PayrollFrom,
            PayrollTo = r.PayrollTo,
            PayrollDate = r.PayrollDate,
            EmployeeShare = r.Amount,
            EmployerShare = 0,
            TotalContribution = r.Amount,
        }).OrderBy(x => x.FullName).ToList();
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
    public async Task AddRangeAsync(List<WTaxContribution> models, CancellationToken token)
    {
        await Uow.Repository.AddRangeAsync(models, token);
        await Uow.SaveChangesAsync(token);
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
            .Where(x =>
                    (x.PayrollDate.Month == fromDate.Month && x.PayrollDate.Year == fromDate.Year)
                    //(x.PayrollDate.Month == toDate.Month && x.PayrollDate.Year == toDate.Year)
                    )
            //.Where(x => !(x.PayrollDate >= fromDate && x.PayrollDate <= toDate))
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

    // Cascade cleanup for PayrollProcessorService.DeleteBatchAsync — see
    // SSSContributionService.DeleteByBatchIdAsync for why this must mirror it.
    public async Task DeleteByBatchIdAsync(Guid payrollBatchId, CancellationToken token) =>
        await ExecuteDeleteAsync(x => x.PayrollBatchId == payrollBatchId, token);
}

