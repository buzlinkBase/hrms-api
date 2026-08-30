using Hrms.Core.Extensions;
using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;
using Mapster;

namespace Hrms.Core.Services;

public class SSSContributionService : BaseService<SSSContribution>
{
    private readonly TypeAdapterConfig _config;
    private readonly IMapper _mapper;
    private readonly EmployeeService _employeeService;

    public SSSContributionService(IUnitOfWorkService uow,
        TypeAdapterConfig config,
        IMapper mapper,
        EmployeeService employeeService) : base(uow)
    {
        _config = config;
        _mapper = mapper;
        _employeeService = employeeService;
    }

    // Remittance report — proper from/to range (unlike LoadContributionsAsync below, whose
    // toDate filter is dead code), joined with Employee for name/SSS No.
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
            GovIdNumber = employeeMap.TryGetValue(r.EmployeeId, out var e3) ? e3.SSSNo : "",
            PayrollFrom = r.PayrollFrom,
            PayrollTo = r.PayrollTo,
            PayrollDate = r.PayrollDate,
            EmployeeShare = r.EE,
            EmployerShare = r.ER + r.EC,
            TotalContribution = r.TotalContibution,
        }).OrderBy(x => x.FullName).ToList();
    }
    public async Task AddAsync(SSSContribution model, CancellationToken token)
    {
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }
    public async Task UpdateAsync(SSSContribution model, CancellationToken token)
    {
        await ModifyAsync(model, token);
        await CommitChangesAsync(token);
    }
    public async Task AddOrUpdateAsync(SSSContribution model, CancellationToken token)
    {
        await CreateOrUpdateAsync(model, token);
        await CommitChangesAsync(token);
    }

    public async Task AddRangeAsync(List<SSSContribution> models, CancellationToken token)
    {
        await Uow.Repository.AddRangeAsync(models, token);
        await Uow.SaveChangesAsync(token);
        await CommitChangesAsync(token);
    }

    public async Task<Dictionary<EmployeeKey, List<SSSContributionModel>>> LoadContributionsAsync(DateOnly fromDate, DateOnly toDate, CancellationToken token)
    {
        var data = await GetQueryable()
                .Where(x =>
                    (x.PayrollDate.Month == fromDate.Month && x.PayrollDate.Year == fromDate.Year)
                    //(x.PayrollDate.Month == toDate.Month && x.PayrollDate.Year == toDate.Year)
                    )
            //.Where(x => !(x.PayrollDate >= fromDate && x.PayrollDate <= toDate))
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

    // Cascade cleanup for PayrollProcessorService.DeleteBatchAsync — removes every ledger row
    // this run wrote in one statement, so remittance reports (which read this table directly,
    // not Payroll.IsPosted) don't keep showing contributions for a payroll run that no longer
    // exists. Matches on PayrollBatchId rather than EmployeeId+period so it can't collide with
    // a different batch that happens to cover the same employee/period.
    public async Task DeleteByBatchIdAsync(Guid payrollBatchId, CancellationToken token) =>
        await ExecuteDeleteAsync(x => x.PayrollBatchId == payrollBatchId, token);
}

