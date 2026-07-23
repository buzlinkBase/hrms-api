using Hrms.Domain.Entities;
using MassTransit.Util;

namespace Hrms.Core.Services;

public class ChangeHolidayQueryPayload
{
    public Guid? EmployeeId { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
}
public class ChangeHolidayService : BaseService<ChangeHoliday>
{
    public ChangeHolidayService(IUnitOfWorkService uow) : base(uow)
    {
    }
    public async Task AddAsync(CreateChangeHoliday holidayModel, CancellationToken token)
    {
        var Ids = holidayModel.EmployeeIds;
        if (!Ids.Any()) return;

        var batches = GetQueryable()
            .Where(x => Ids.Contains(x.EmployeeId)
                 && x.HolidayId == holidayModel.HolidayId)
            .Select(x => x.BatchCode)
            .Distinct()
            .ToList();

        var existing = GetQueryable()
           .Where(x => batches.Contains(x.BatchCode))
           .ToList();

        await RemoveRangeAsync(existing, token);
        await AddChangeHolidayAsync(holidayModel, token);
        await SaveChangesAsync(token);
        await CommitChangesAsync(token);
    }

    public async Task<List<ChangeHolidayModel>> LoadAllAsync(ChangeHolidayQueryPayload payload, CancellationToken token)
    {
        var records = await GetQueryable()
            .Include(x => x.Employee).ThenInclude(e => e.Client)
            .Include(x => x.Holiday)
            .Where(x =>
                (!payload.EmployeeId.HasValue || x.EmployeeId == payload.EmployeeId) &&
                x.PayrollDate >= payload.FromDate && x.PayrollDate <= payload.ToDate)
            .ToListAsync(token);

        return records
            .GroupBy(x => new { x.BatchCode, x.EmployeeId })
            .Select(g =>
            {
                var firstItem = g.First();
                return new ChangeHolidayModel
                {
                    EmployeeId = g.Key.EmployeeId,
                    BatchCode = g.Key.BatchCode,
                    FullName = firstItem.Employee?.FullName(),
                    HolidayName = firstItem.Holiday?.Description,
                    ClientName = firstItem.Employee?.Client?.Name,
                    FromDate = g.Min(x => x.PayrollDate),
                    ToDate = g.Max(x => x.PayrollDate)
                };
            })
            .OrderBy(x => x.FromDate)
            .ToList();
    }


    private async Task AddChangeHolidayAsync(CreateChangeHoliday holidayModel, CancellationToken token)
    {
        var yr = DateTime.UtcNow.Year;
        var count = Context.ChangeRestDays
            .Where(x => x.PayrollDate.Year == yr)
            .GroupBy(x => x.BatchCode).Count() + 1;
        var batchCode = $"CHOL{holidayModel.PayrollDateFrom.ToString("MMMddyyyy")}{holidayModel.PayrollDateTo.ToString("MMMddyyyy")}{count.ToString().PadLeft(5, '0')}";
        var models = new List<ChangeRestDay>();

        foreach (var emp in holidayModel.EmployeeIds)
        {
            var entity1 = new ChangeHoliday()
            {

                State = ChangeSchedState.OVERRIDEN,
                PayrollDate = holidayModel.PayrollDateFrom,
                EmployeeId = emp,
                HolidayId = holidayModel.HolidayId,
                BatchCode = batchCode,
            };
            var entity2 = new ChangeHoliday()
            {
                State = ChangeSchedState.REPLACEMENT,
                PayrollDate = holidayModel.PayrollDateTo,
                EmployeeId = emp,
                HolidayId = holidayModel.HolidayId,
                BatchCode = batchCode,
            };
            await _uow.Repository.AddAsync(entity1, token);
            await _uow.Repository.AddAsync(entity2, token);
        }
    }

    public async Task DeleteEmployee(Guid employeeId, string batchCode, CancellationToken token)
    {
        await Context.ChangeHolidays.Where(x => x.BatchCode == batchCode && x.EmployeeId == employeeId)
             .ExecuteDeleteAsync(token);
        await SaveChangesAsync(token);
        await CommitChangesAsync(token);
    }


    public async Task DeleteAsync(string BatchCode, CancellationToken token)
    {
        await RemoveAsync(x => x.BatchCode == BatchCode, token);
        await CommitChangesAsync(token);
    }


    public async Task<Dictionary<Holidaykey, List<ChangeHoliday>>>
        GetAllChangedHolidayAsync(DateOnly fromDate, DateOnly toDate,
        CancellationToken token)
    {
        return await GetQueryable()
          .Where(x => x.PayrollDate >= fromDate && x.PayrollDate <= toDate)
          .AsNoTracking()
          .GroupBy(x => new Holidaykey(x.EmployeeId, x.PayrollDate))
          .ToDictionaryAsync(x => x.Key, x => x.Distinct().ToList(), token)
          ;
    }
}

