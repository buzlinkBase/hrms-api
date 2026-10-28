using Hrms.Domain.Entities;
using Mapster;
using Onepunch.Common.Lib.Exceptions;

namespace Hrms.Core.Services;

public class HolidayService : BaseService<Holiday>
{
    private readonly IMapper _mapper;

    public HolidayService(IUnitOfWorkService uow, IMapper mapper) : base(uow)
    {
        _mapper = mapper;
    }
    public async Task<HolidayModel> AddAsync(CreateHoliday model, CancellationToken token)
    {
        var holiday = _mapper.Map<Holiday>(model);
        holiday.HolYear = model.HolDate.Year;
        holiday.IsPaid = HolidayPaidPolicy.Resolve(holiday.HolType);
        foreach (var areaId in await ResolveAreaIdsAsync(model, token))
        {
            holiday.Areas.Add(new HolidayArea { AreaId = areaId });
        }
        await CreateAsync(holiday, token);
        await CommitChangesAsync(token);
        return _mapper.Map<HolidayModel>(holiday);
    }
    public async Task<HolidayModel> UpdateAsync(UpdateHoliday model, CancellationToken token)
    {
        var existing = await Context.Holidays
            .Include(x => x.Areas)
            .FirstOrDefaultAsync(x => x.Id == model.Id, token);
        if (existing == null)
        {
            throw new NotFoundException("Record not found");
        }
        var areaIds = await ResolveAreaIdsAsync(model, token);
        model.Adapt(existing);
        existing.HolYear = model.HolDate.Year;
        existing.IsPaid = HolidayPaidPolicy.Resolve(existing.HolType);

        // Replace the site set: drop the ones no longer picked, add the new ones.
        var removed = existing.Areas.Where(a => !areaIds.Contains(a.AreaId)).ToList();
        Context.HolidayAreas.RemoveRange(removed);
        foreach (var area in removed)
        {
            existing.Areas.Remove(area);
        }
        // Added through the set (not just the collection) so they're tracked as new before
        // ModifyAsync's Update walks the graph.
        foreach (var areaId in areaIds.Where(id => existing.Areas.All(a => a.AreaId != id)).ToList())
        {
            Context.HolidayAreas.Add(new HolidayArea { HolidayId = existing.Id, AreaId = areaId });
        }
        await ModifyAsync(existing, token);
        await CommitChangesAsync(token);
        return _mapper.Map<HolidayModel>(existing);
    }

    // Sites only apply to special holidays (legal ones are nationwide by law). Every id must be
    // an existing project site of this company.
    private async Task<List<Guid>> ResolveAreaIdsAsync(CreateHoliday model, CancellationToken token)
    {
        if (model.HolType != HolidayType.SPECIAL)
        {
            return [];
        }
        var areaIds = model.AreaIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (areaIds.Count == 0)
        {
            return areaIds;
        }
        var found = await _uow.Repository.FindAll<CostCenters>()
            .AsNoTracking()
            .CountAsync(x => areaIds.Contains(x.Id), token);
        if (found != areaIds.Count)
        {
            throw new GuardException("One or more of the selected project sites no longer exist.");
        }
        return areaIds;
    }

    public async Task<List<HolidayResult>> FindAllAsync(int year, CancellationToken token)
    {
        var holidays = await _uow.Repository.Find<Holiday>(x =>
        x.IsRecuring || x.HolYear == year)
            .AsNoTracking()
            .Include(x => x.Areas).ThenInclude(a => a.Area)
            .ToListAsync(token);

        return holidays.Select(x =>
        {
            var displayDate = x.IsRecuring
                ? ResolveRecurringDate(x, year)
                : x.HolDate;
            return new HolidayResult(x.Id,
                x.Description,
                x.Areas.Select(a => a.AreaId).ToList(),
                x.Areas.Select(a => a.Area?.Name ?? string.Empty).Where(n => n.Length > 0).OrderBy(n => n).ToList(),
                x.IsRecuring ? $"{displayDate.ToString("MMM dd,")} {year}" : x.HolDate.ToString("MMM dd, yyyy"),
                x.HolType == HolidayType.LEGAL ? "Legal" : "Special",
                x.IsRecuring,
                x.IsPaid,
                x.WorkType,
                x.Status);
        }).ToList();
    }

    // Recomputes a recurring holiday's date for the given year — nth-weekday (e.g. "last
    // Monday of August") when WeekOfMonth/DayOfWeek are set, otherwise the existing fixed
    // month/day substitution.
    private static DateOnly ResolveRecurringDate(Holiday holiday, int year) =>
        holiday.WeekOfMonth.HasValue && holiday.DayOfWeek.HasValue
            ? HolidayRecurrenceCalculator.ResolveNthWeekday(year, holiday.HolDate.Month, holiday.DayOfWeek.Value, holiday.WeekOfMonth.Value)
            : new DateOnly(year, holiday.HolDate.Month, holiday.HolDate.Day);
    public async Task<List<HolidayModel>> GetAllHolidays(DateOnly from, DateOnly to,
        CancellationToken token)
    {
        from = from.AddDays(-4);
        var holidays = await _uow.Repository
            // Active only -- recurring holidays included. Operator precedence used to make this
            // "IsRecuring || (inRange && Active)", so inactive recurring holidays still applied.
            .Find<Holiday>(x => (x.IsRecuring || (x.HolDate >= from && x.HolDate <= to)) && x.Status == "Active")
            .AsNoTracking()
            .Include(x => x.Areas)
            .ToListAsync(token)
            ;

        return ProjectHolidaysForRange(holidays, from, to);
    }

    // The actual per-row date-resolution + range-filter logic behind GetAllHolidays — this is
    // what the DTR pipeline (dtr-api's HolidayQueryService.GetHolidayInfoAsync) consumes for
    // every payroll/DTR run. Pulled out as internal static, over an already-fetched row list,
    // so it's directly unit-testable without a database — same convention as
    // PayrollReportService.ResolveRegionRate.
    internal static List<HolidayModel> ProjectHolidaysForRange(List<Holiday> holidays, DateOnly from, DateOnly to)
    {
        return holidays
            .Select(x =>
            {
                DateOnly holDate;
                if (x.IsRecuring)
                {
                    var year = from.Year;
                    if (from.Year != to.Year)
                    {
                        year = x.HolDate.Month >= from.Month ? from.Year : to.Year;
                    }
                    holDate = ResolveRecurringDate(x, year);
                }
                else
                {
                    holDate = x.HolDate;
                }

                return new HolidayModel
                {
                    Id = x.Id,
                    AreaIds = x.Areas.Select(a => a.AreaId).ToList(),
                    Description = x.Description,
                    HolDate = holDate,
                    HolType = x.HolType,
                    HolYear = holDate.Year,
                    IsRecuring = x.IsRecuring,
                    WeekOfMonth = x.WeekOfMonth,
                    DayOfWeek = x.DayOfWeek,
                    Status = x.Status,
                    IsPaid = x.IsPaid,
                    WorkType = x.WorkType,
                };
            })
            .Where(h => h.HolDate >= from && h.HolDate <= to)
            .ToList();
    }
    public async Task<HolidayModel?> FineOneAsync(Guid Id, CancellationToken token)
    {
        var result = await GetOneWithAreasReadOnlyAsync(Id, token);
        return _mapper.Map<HolidayModel?>(result);
    }

    public async Task<HolidayModel?> FindLatest(Guid Id, CancellationToken token)
    {
        // Untracked: the date is re-resolved on this copy for display only, never saved.
        var result = await GetOneWithAreasReadOnlyAsync(Id, token);
        if (result == null) return _mapper.Map<HolidayModel?>(result);
        var curDate = DateTime.UtcNow;
        if (result.IsRecuring)
        {
            result.HolYear = curDate.Year;
            result.HolDate = ResolveRecurringDate(result, result.HolYear);
        }
        return _mapper.Map<HolidayModel?>(result);
    }

    private Task<Holiday?> GetOneWithAreasReadOnlyAsync(Guid id, CancellationToken token) =>
        _uow.Repository.FindAll<Holiday>()
            .AsNoTracking()
            .Include(x => x.Areas)
            .FirstOrDefaultAsync(x => x.Id == id, token);

    public async Task Delete(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);
    }
    public async Task DeleteAllAsync(int year, CancellationToken token)
    {
        var holidays = await _uow.Repository
            .Find<Holiday>(x => x.HolYear == year)
            .ToListAsync();
        ;
        await RemoveRangeAsync(holidays, token);
        await CommitChangesAsync(token);
    }
}

public readonly record struct Holidaykey(Guid EmpId, DateOnly PayrollDate);
public record HolidayResult(Guid Id,
    string Description,
    List<Guid> AreaIds,
    List<string> AreaNames,
    string HolDate,
    string HolidayType,
    bool IsRecuring,
    bool IsPaid,
    HolidayWorkType WorkType,
    string status);