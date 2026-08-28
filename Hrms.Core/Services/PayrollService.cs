using DocumentFormat.OpenXml.VariantTypes;
using Hrms.Domain.Entities;
namespace Hrms.Core.Services;

public class PayrollService : BaseService<Payroll>
{
    private readonly IMapper _mapper;
    public PayrollService(IUnitOfWorkService uow, IMapper mapper) : base(uow)
    {
        _mapper = mapper;
    }

    public async Task<Dictionary<EmployeeKey, List<Payroll>>> LoadPostedPayrollAsync(DateOnly fromDate, DateOnly toDate,
        CancellationToken token)
    {
        //var spec = new IsPostedSpec<Payroll>(true)
        //    .And(new IsDateByMonthYearSpec<Payroll>(fromDate))
        //    .AndNot(new IsDateWithinRangeSpec<Payroll>(fromDate, toDate))
        //    ;
        return await GetQueryable(x =>
                 //x.PayrollDate >= fromDate && x.PayrollDate <= toDate &&
                 x.PayrollDate.Month == fromDate.Month && x.PayrollDate.Year == fromDate.Year  &&
                 x.IsPosted)
            .GroupBy(x => new EmployeeKey(x.EmployeeId))
            .ToDictionaryAsync(x => x.Key, x => x.ToList(), token);
        ;
    }

    public async Task<Payroll?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }

    public async Task Delete(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
    }

    public async Task SavePayrollsAsync(IEnumerable<Payroll> payrolls, CancellationToken token)
    {
        await Uow.Repository.AddRangeAsync(payrolls, token);
        await Uow.SaveChangesAsync(token);
        await CommitChangesAsync(token);
    }

    public async Task<List<Payroll>> GetAsync(
        DateOnly from, DateOnly to,
        Guid? employeeId, Guid? clientId, Guid? payrollGroupId,
        CancellationToken token)
    {
        return await GetQueryable(x =>
                x.PayPeriodStart >= from && x.PayPeriodEnd <= to &&
                (employeeId == null || x.EmployeeId == employeeId) &&
                (clientId == null || x.ClientId == clientId) &&
                (payrollGroupId == null || x.PayrollGroupId == payrollGroupId))
            .OrderByDescending(x => x.PayPeriodStart)
            .ThenBy(x => x.FullName)
            .ToListAsync(token);
    }
}

