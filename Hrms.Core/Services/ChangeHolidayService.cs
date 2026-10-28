using Hrms.Domain.Entities;
using MassTransit.Util;
using Hrms.Domain.Entities.EmployeeEntities;
using Onepunch.Common.Lib.Exceptions;

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
        await EnsureEmployeesAreCoveredAsync(holidayModel, token);

        var batches = GetQueryable()
            .Where(x => Ids.Contains(x.EmployeeId)
                 && x.PayrollDate == holidayModel.PayrollDateFrom
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

    // A localized special holiday can only be moved for employees assigned to one of its
    // project sites -- for anyone else it isn't their holiday to move (the DTR would also drop the
    // replacement, since it checks the day's site against the holiday's sites).
    private async Task EnsureEmployeesAreCoveredAsync(CreateChangeHoliday holidayModel, CancellationToken token)
    {
        var holiday = await _uow.Repository.FindAll<Holiday>()
            .AsNoTracking()
            .Include(x => x.Areas)
            .FirstOrDefaultAsync(x => x.Id == holidayModel.HolidayId, token)
            ?? throw new GuardException("The selected holiday no longer exists.");

        var siteIds = holiday.Areas.Select(a => a.AreaId).ToHashSet();
        if (holiday.HolType != HolidayType.SPECIAL || siteIds.Count == 0)
        {
            return;
        }

        var employeeIds = holidayModel.EmployeeIds.Distinct().ToList();
        var uncovered = (await _uow.Repository.FindAll<Employee>()
                .AsNoTracking()
                .Where(x => employeeIds.Contains(x.Id))
                .Select(x => new { x.AreaId, x.LastName, x.FirstName })
                .ToListAsync(token))
            .Where(x => !x.AreaId.HasValue || !siteIds.Contains(x.AreaId.Value))
            .Select(x => $"{x.LastName}, {x.FirstName}")
            .OrderBy(x => x)
            .ToList();
        if (uncovered.Count == 0)
        {
            return;
        }

        var names = string.Join("; ", uncovered.Take(5));
        var more = uncovered.Count > 5 ? $" and {uncovered.Count - 5} more" : string.Empty;
        throw new GuardException(
            $"{holiday.Description} only applies to its project sites. Not assigned to one of them: {names}{more}.");
    }

    public async Task<List<ChangeHolidayModel>> LoadAllAsync(ChangeHolidayQueryPayload payload, CancellationToken token)
    {
        DateTime fromDateTime = payload.FromDate.ToDateTime(TimeOnly.MinValue);
        DateTime toDateTime = payload.ToDate.ToDateTime(TimeOnly.MaxValue);

        var records = await GetQueryable()
            .AsNoTracking()
            .Include(x => x.Employee).ThenInclude(e => e.Client)
            .Include(x => x.Employee).ThenInclude(e => e.Area)
            .Include(x => x.Holiday)
            .Where(x =>
                (!payload.EmployeeId.HasValue || x.EmployeeId == payload.EmployeeId) &&
                x.CreatedAt >= fromDateTime && x.CreatedAt <= toDateTime)
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
                    FullName = firstItem.Employee.FullName(),
                    HolidayName = firstItem.Holiday.Description,
                    ClientName = firstItem.Employee?.Client?.Name,
                    Area = firstItem.Employee?.Area?.Name,
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

