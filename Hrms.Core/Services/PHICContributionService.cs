using System.Text;
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

    // Cascade cleanup for PayrollProcessorService.DeleteBatchAsync — see
    // SSSContributionService.DeleteByBatchIdAsync for why this must mirror it.
    public async Task DeleteByBatchIdAsync(Guid payrollBatchId, CancellationToken token) =>
        await ExecuteDeleteAsync(x => x.PayrollBatchId == payrollBatchId, token);

    // PhilHealth EPRS (Electronic Premium Remittance System) upload file. Column layout
    // implemented from general knowledge of the EPRS CSV template, NOT against a live
    // reference spec — diff against the current portal template before first real submission.
    public async Task<byte[]> GenerateEprsFileAsync(DateOnly from, DateOnly to, CancellationToken token)
    {
        var rows = await GetQueryable(x => x.PayrollDate >= from && x.PayrollDate <= to).ToListAsync(token);
        var employees = await _employeeService.FindByIds(rows.Select(x => x.EmployeeId).Distinct().ToList(), token);
        var employeeMap = employees.ToDictionary(x => x.Id);

        var sb = new StringBuilder();
        sb.AppendLine("PhilHealth No.,Last Name,First Name,Middle Name,EE Share,ER Share,Total Premium,Applicable Period");
        foreach (var r in rows.OrderBy(x => employeeMap.TryGetValue(x.EmployeeId, out var e) ? e.LastName : ""))
        {
            employeeMap.TryGetValue(r.EmployeeId, out var emp);
            sb.AppendLine(string.Join(",",
                StatutoryFileFormat.CsvField(emp?.PHICNo ?? ""),
                StatutoryFileFormat.CsvField(emp?.LastName ?? ""),
                StatutoryFileFormat.CsvField(emp?.FirstName ?? ""),
                StatutoryFileFormat.CsvField(emp?.MiddleName ?? ""),
                r.EmployeeShare.ToString("F2"),
                r.EmployerShare.ToString("F2"),
                r.TotalContribution.ToString("F2"),
                from.ToString("MM/yyyy")));
        }
        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}

