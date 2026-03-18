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
        var spec = new IsPostedSpec<Payroll>(true)
            .And(new IsDateByMonthYearSpec<Payroll>(fromDate))
            .AndNot(new IsDateWithinRangeSpec<Payroll>(fromDate, toDate))
            ;
        return await GetQueryable(spec)
            .GroupBy(x => new EmployeeKey(x.EmployeeId))
            .ToDictionaryAsync(x => x.Key, x => x.ToList(),token);
        ;
    }

    public async Task<Payroll?> FineOneAsync(Guid Id,CancellationToken token)
    {
        return await GetOneAsync(Id,token);
    }

    public async Task Delete(Guid Id,CancellationToken token)
    {
        await RemoveAsync(Id,token);
    }

}

