using Hrms.Domain.Entities; 

namespace Hrms.Core.Services;

public record struct ChangeHolidayQueryPayload(Guid? PayrollGroupId, Guid? EmployeeId, Guid? ClientId, DateOnly fromDate, DateOnly toDate);
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
            .Select(x => x.BatchEntryId)
            .ToList();

        var existing = GetQueryable()
           .Where(x => batches.Contains(x.BatchEntryId))
           .ToList();

        await RemoveRangeAsync(existing, token);
        await _uow.SaveChangesAsync(token);
        await AddChangeHolidayAsync(holidayModel, token);
        await CommitChangesAsync(token);
    }

    public async Task<List<ChangeHolidayModel>> LoadAllAsync(ChangeHolidayQueryPayload payload,
        CancellationToken token)
    {
        //flatten
        return (await GetQueryable()
        .Include(x => x.Employee).ThenInclude(e => e.Client)
        .Include(x => x.Holiday)
        .Where(x =>
            (payload.PayrollGroupId == null || x.Employee.PayrollGroupId == payload.PayrollGroupId) &&
            (payload.EmployeeId == null || x.EmployeeId == payload.EmployeeId) &&
            (payload.ClientId == null || x.Employee.ClientId == payload.ClientId) &&
            x.PayrollDate >= payload.fromDate && x.PayrollDate <= payload.toDate)
        .ToListAsync(token))
        .GroupBy(x => new { x.BatchEntryId, x.EmployeeId })
        .Select(g =>
        {
            var ordered = g.OrderBy(x => x.PayrollDate).ToList();
            return new ChangeHolidayModel
            {
                BatchId = g.Key.BatchEntryId,
                FullName = ordered.FirstOrDefault()?.Employee?.FullName(),
                HolidayName = ordered.FirstOrDefault()?.Holiday?.Description,
                ClientName = ordered.FirstOrDefault()?.Employee?.Client?.Name,
                FromDate = ordered.FirstOrDefault()?.PayrollDate ?? default,
                ToDate = ordered.LastOrDefault()?.PayrollDate ?? default
            };
        })
        .OrderBy(x => x.FromDate)
        .ToList();
    }


    private async Task AddChangeHolidayAsync(CreateChangeHoliday holidayModel, CancellationToken token)
    {
        var batchId = Guid.NewGuid();
        foreach (var emp in holidayModel.EmployeeIds)
        {
            var entity1 = new ChangeHoliday()
            {

                State = ChangeSchedState.OVERRIDEN,
                PayrollDate = holidayModel.PayrollDateFrom,
                EmployeeId = emp,
                HolidayId = holidayModel.HolidayId,
                BatchEntryId = batchId,
            };
            var entity2 = new ChangeHoliday()
            {
                State = ChangeSchedState.REPLACEMENT,
                PayrollDate = holidayModel.PayrollDateTo,
                EmployeeId = emp,
                HolidayId = holidayModel.HolidayId,
                BatchEntryId = batchId,
            };
            await _uow.Repository.AddAsync(entity1, token);
            await _uow.Repository.AddAsync(entity2, token);
        }
    }


    public async Task DeleteAsync(Guid batchId, CancellationToken token)
    {
        await RemoveAsync(x => x.BatchEntryId == batchId, token);
        await CommitChangesAsync(token);
    }


    public async  Task<Dictionary<Holidaykey, List<ChangeHoliday>>>
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

