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

    // Every row from one Generate run — all rows written by a single PayrollProcessorService
    // .CalculateAsync call share the same PayrollBatchId (assigned once per run, the id of
    // the PayrollBatch header row, not per employee).
    public async Task<List<Payroll>> GetByBatchIdAsync(Guid payrollBatchId, CancellationToken token)
    {
        return await GetQueryable(x => x.PayrollBatchId == payrollBatchId).ToListAsync(token);
    }

    public async Task DeleteByBatchIdAsync(Guid payrollBatchId, CancellationToken token)
    {
        await ExecuteDeleteAsync(x => x.PayrollBatchId == payrollBatchId, token);
    }

    // Mirrors PayrollBatch.IsPosted onto every child row of the batch — PayrollBatch is the
    // canonical source for Post/Delete decisions, but the child rows keep their own copy so
    // hot-path report filters (PayrollReportService, LoadPostedPayrollAsync above) don't
    // need to join PayrollBatch. See PayrollProcessorService.PostBatchAsync, which calls
    // this alongside PayrollBatchService.PostAsync.
    public async Task PostBatchAsync(Guid payrollBatchId, CancellationToken token)
    {
        // GetQueryable defaults to AsNoTracking — must opt into tracking here (noTracking:
        // false), or mutating IsPosted below never gets picked up by SaveChanges and this
        // silently no-ops: no exception, no rows actually updated. ModifyRangeAsync/UpdateRange
        // is kept as a belt-and-suspenders explicit mark, but the real fix is tracking the
        // query in the first place — same reasoning as DailyRecordService.PostAsync/UnpostAsync,
        // which mutate tracked entities directly with no explicit Update() call at all.
        var payrolls = await GetQueryable(x => x.PayrollBatchId == payrollBatchId, noTracking: false).ToListAsync(token);
        if (payrolls.Count == 0) throw new ValidationException("Payroll batch not found.");
        foreach (var payroll in payrolls) payroll.IsPosted = true;
        await ModifyRangeAsync(payrolls, token);
        await CommitChangesAsync(token);
    }

    public async Task SavePayrollsAsync(IEnumerable<Payroll> payrolls, CancellationToken token)
    {
        await Uow.Repository.AddRangeAsync(payrolls, token);
        await Uow.SaveChangesAsync(token);
        await CommitChangesAsync(token);
    }

    // Employees who already have a 13th month payout Payroll row for the given calendar
    // year — used by PayrollProcessorService.GenerateThirteenthMonthAsync to block
    // regenerating for an employee already paid, the 13th-month analog of
    // PayrollBatchService.GetUsedDtrBatchCodesAsync.
    public async Task<HashSet<Guid>> GetThirteenthMonthPaidEmployeeIdsAsync(int year, CancellationToken token)
    {
        var ids = await GetQueryable(x => x.PayrollType == PayrollType.ThirteenthMonth && x.PayPeriodStart.Year == year)
            .Select(x => x.EmployeeId)
            .Distinct()
            .ToListAsync(token);
        return ids.ToHashSet();
    }

    // Last Pay is a one-time-ever payout per employee (not annual like 13th month), so no
    // year bound — an employee who already has a LastPay row must have it deleted first to
    // regenerate. See PayrollProcessorService.GenerateLastPayAsync.
    public async Task<HashSet<Guid>> GetLastPayPaidEmployeeIdsAsync(CancellationToken token)
    {
        var ids = await GetQueryable(x => x.PayrollType == PayrollType.LastPay)
            .Select(x => x.EmployeeId)
            .Distinct()
            .ToListAsync(token);
        return ids.ToHashSet();
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

