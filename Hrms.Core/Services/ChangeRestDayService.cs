using Hrms.Domain.Entities;
namespace Hrms.Core.Services;

public class ChangeRestDayService : BaseService<ChangeRestDay>
{
    public ChangeRestDayService(IUnitOfWorkService uow) : base(uow) { }

    public async Task AddChangeOff(ChangeOffModel changeOffs, CancellationToken token)
    {
        var Ids = changeOffs.EmployeeIds;
        if (!Ids.Any()) return;

        var batches = await GetQueryable()
            .Where(x => Ids.Any(xx => xx == x.EmployeeId)
                && x.PayrollDate == changeOffs.PayrolLDateFrom)
            .Select(x => x.BatchCode)
            .Distinct()
            .ToListAsync(token);

        var existing = await GetQueryable()
           .Where(x => batches.Any(xx => xx == x.BatchCode)
            && Ids.Contains(x.EmployeeId))
           .ToListAsync(token);

        await RemoveRangeAsync(existing, token);
        await AddNewDayOffAsync(changeOffs, token);
        await SaveChangesAsync(token);
        await CommitChangesAsync(token);

    }

    private async Task AddNewDayOffAsync(ChangeOffModel changeOff, CancellationToken token)
    {
        var yr = DateTime.UtcNow.Year;
        var count = Context.ChangeRestDays
            .Where(x => x.PayrollDate.Year == yr)
            .GroupBy(x => x.BatchCode).Count() + 1;
        var batchCode = $"COFF{changeOff.PayrolLDateFrom.ToString("MMMddyyyy")}{changeOff.PayrolLDateTo.ToString("MMMddyyyy")}{count.ToString().PadLeft(5, '0')}";
        var models = new List<ChangeRestDay>();
        foreach (var emp in changeOff.EmployeeIds)
        {
            var entity1 = new ChangeRestDay()
            {
                DayName = changeOff.FromDay,
                State = ChangeSchedState.OVERRIDEN,
                PayrollDate = changeOff.PayrolLDateFrom,
                EmployeeId = emp,
                BatchCode = batchCode,
            };
            var entity2 = new ChangeRestDay()
            {
                DayName = changeOff.ToDay,
                State = ChangeSchedState.REPLACEMENT,
                PayrollDate = changeOff.PayrolLDateTo,
                EmployeeId = emp,
                BatchCode = batchCode,
            };
            models.Add(entity1);
            models.Add(entity2);
        }
        await _uow.Repository.AddRangeAsync(models);
    }
    public async Task<Dictionary<ResDaykey, List<ChangeRestDay>>> GetChangeRestDays(DateOnly fromDate,
        DateOnly toDate,
        HashSet<Guid> empIds,
        CancellationToken token)
    {
        return await _uow.Repository
         .Find<ChangeRestDay>(x => empIds.Any(xx => xx == x.EmployeeId) &&
                x.PayrollDate >= fromDate
                && x.PayrollDate <= toDate)
         .GroupBy(x => new ResDaykey(x.EmployeeId, x.PayrollDate))
         .ToDictionaryAsync(x => x.Key, x => x.ToList(), token)
         ;
    }
    public async Task<List<RestDayRecordResponse>> FindList(RestDayListFilter query, CancellationToken token)
    {
        DateTime? fromDateTime = query.FromDate?.ToDateTime(TimeOnly.MinValue);
        DateTime? toDateTime = query.ToDate?.ToDateTime(TimeOnly.MaxValue);

        return await GetQueryable()
            .AsNoTracking()
            .Where(x =>
                (!query.EmployeeId.HasValue || x.EmployeeId == query.EmployeeId) &&
                (!fromDateTime.HasValue || x.CreatedAt >= fromDateTime.Value) &&
                (!toDateTime.HasValue || x.CreatedAt <= toDateTime.Value))
            .GroupBy(x => new
            {
                x.BatchCode,
                x.EmployeeId,
                x.Employee.FirstName,
                x.Employee.LastName
            })
            .Select(g => new RestDayRecordResponse
            {
                EmployeeId = g.Key.EmployeeId,
                BatchCode = g.Key.BatchCode,
                // Construct string directly inside SQL projection
                FullName = $"{g.Key.FirstName} {g.Key.LastName}".Trim(),
                FromDate = g.Min(x => x.PayrollDate),
                ToDate = g.Max(x => x.PayrollDate)
            })
            .OrderBy(x => x.FromDate)
            .ToListAsync(token);
    }

    public async Task DeleteEmployee(Guid employeeId, string batchCode, CancellationToken token)
    {
        await Context.ChangeRestDays.Where(x => x.BatchCode == batchCode && x.EmployeeId == employeeId)
             .ExecuteDeleteAsync(token);
        await SaveChangesAsync(token);
        await CommitChangesAsync(token);
    }
    public async Task DeleteBatch(string batchCode, CancellationToken token)
    {
        await Context.ChangeRestDays.Where(x => x.BatchCode == batchCode)
             .ExecuteDeleteAsync(token);
        await SaveChangesAsync(token);
        await CommitChangesAsync(token);
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
    public Guid EmployeeId { get; set; }
    public string BatchCode { get; set; }
    public string FullName { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
}

public class RestDayListFilter
{
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public Guid? EmployeeId { get; set; }
}
