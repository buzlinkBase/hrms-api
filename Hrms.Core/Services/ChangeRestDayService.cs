


using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

public class ChangeRestDayService : BaseService<ChangeRestDay>
{
    public ChangeRestDayService(IUnitOfWorkService uow) : base(uow) { }

    public async Task SaveChange(ChangeOffModel changeOffs, CancellationToken token)
    {
        var Ids = changeOffs.EmployeeIds;
        if (!Ids.Any()) return;

        var batches = GetQueryable()
            .Where(x => Ids.Any(xx => xx == x.EmployeeId)
                && x.PayrollDate == changeOffs.PayrolLDateFrom)
            .Select(x => x.BatchEntryId)
            .ToList();

        var existing = GetQueryable()
           .Where(x => batches.Any(xx => xx == x.BatchEntryId)
            && Ids.Contains(x.EmployeeId))
           .ToList();

        await RemoveRangeAsync(existing, token);
        await SaveChangesAsync(token);
        await AddNewDayOffAsync(changeOffs, token);
        await CommitChangesAsync(token);
    }
    private async Task AddNewDayOffAsync(ChangeOffModel changeOff, CancellationToken token)
    {
        var batchId = Guid.NewGuid();
        foreach (var emp in changeOff.EmployeeIds)
        {
            var entity1 = new ChangeRestDay()
            {
                DayName = changeOff.FromDay,
                State = ChangeSchedState.OVERRIDEN,
                PayrollDate = changeOff.PayrolLDateFrom,
                EmployeeId = emp,
                BatchEntryId = batchId,
            };
            var entity2 = new ChangeRestDay()
            {
                DayName = changeOff.ToDay,
                State = ChangeSchedState.REPLACEMENT,
                PayrollDate = changeOff.PayrolLDateTo,
                EmployeeId = emp,
                BatchEntryId = batchId,
            };
            await _uow.Repository.AddAsync(entity1, token);
            await _uow.Repository.AddAsync(entity2, token);
        }
    }
    public async  Task<Dictionary<ResDaykey, List<ChangeRestDay>>> GetChangeRestDays(DateOnly fromDate,
        DateOnly toDate,
        HashSet<Guid> empIds, 
        CancellationToken token)
    {
        return await _uow.Repository
         .Find<ChangeRestDay>(x =>  empIds.Any(xx=>xx == x.EmployeeId) &&
                x.PayrollDate >= fromDate 
                && x.PayrollDate <= toDate)
         .GroupBy(x => new ResDaykey(x.EmployeeId, x.PayrollDate))
         .ToDictionaryAsync(x => x.Key, x => x.ToList(), token)
         ;
    }
    public List<RestDayRecordResponse> FindList(Guid? payrollGroupId,
        Guid? employeeId ,
        Guid? clientId ,
        DateOnly fromDate, DateOnly toDate,
        CancellationToken token)
    {
        //flatten
        return GetQueryable()
        .Where(x =>
            (payrollGroupId == null || x.Employee.PayrollGroupId == payrollGroupId) &&
            (employeeId == null || x.EmployeeId == employeeId) &&
            (clientId    == null || x.Employee.ClientId == clientId) &&
            x.PayrollDate >= fromDate && x.PayrollDate <= toDate)
        .ToList()
        .GroupBy(x => new { x.BatchEntryId, x.EmployeeId })
        .Select(g =>
        {
            var ordered = g.OrderBy(x => x.PayrollDate).ToList();
            return new RestDayRecordResponse
            {
                BatchId = g.Key.BatchEntryId,
                FullName = ordered.First().Employee.FullName(),
                FromDate = ordered.First().PayrollDate,
                ToDate = ordered.Last().PayrollDate
            };
        })
        .OrderBy(x => x.FromDate)
        .ToList();
    }

}

public readonly record struct ResDaykey(Guid EmpId, DateOnly RestDay);
public class ChangeOffModel
{
    public DayName FromDay { get; set; }
    public DayName ToDay { get; set; }
    public DateOnly PayrolLDateFrom { get; set; }
    public DateOnly PayrolLDateTo { get; set; }
    public Guid[] EmployeeIds { get; set; }
}

public class RestDayRecordResponse
{
    public Guid BatchId { get; set; }
    public string FullName { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
}
