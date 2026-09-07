using Hrms.Domain.Entities;
using Mapster;

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
        await CreateAsync(holiday, token);
        await CommitChangesAsync(token);
        return _mapper.Map<HolidayModel>(holiday);
    }
    public async Task<HolidayModel> UpdateAsync(UpdateHoliday model, CancellationToken token)
    {
        var existing = await Context.Holidays.FindAsync(new object[] { model.Id }, token);
        if (existing == null)
        {
            throw new NotFoundException("Record not found");
        }
        model.Adapt(existing);
        existing.HolYear = model.HolDate.Year;
        existing.IsPaid = HolidayPaidPolicy.Resolve(existing.HolType);
        await ModifyAsync(existing, token);
        await CommitChangesAsync(token);
        return _mapper.Map<HolidayModel>(existing);
    }

    public async Task<List<HolidayResult>> FindAllAsync(int year, CancellationToken token)
    {
        var holidays = await _uow.Repository.Find<Holiday>(x =>
        x.IsRecuring || x.HolYear == year)
            .Include(x => x.Area)
            .ToListAsync(token);

        return holidays.Select(x =>
        {
            var displayDate = x.IsRecuring
                ? ResolveRecurringDate(x, year)
                : x.HolDate;
            return new HolidayResult(x.Id,
                x.Description,
                x.AreaId,
                x.Area?.Name,
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
            .Find<Holiday>(x => x.IsRecuring || (x.HolDate >= from && x.HolDate <= to) && x.Status=="Active")
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
                    AreaId = x.AreaId,
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
        var result = await GetOneAsync(Id, token);
        return _mapper.Map<HolidayModel?>(result);
    }

    public async Task<HolidayModel?> FindLatest(Guid Id, CancellationToken token)
    {
        var result = await GetOneAsync(Id, token);
        if (result == null) return _mapper.Map<HolidayModel?>(result);
        var curDate = DateTime.UtcNow;
        if (result.IsRecuring)
        {
            result.HolYear = curDate.Year;
            result.HolDate = ResolveRecurringDate(result, result.HolYear);
        }
        return _mapper.Map<HolidayModel?>(result);
    }

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
    Guid? AreaId,
    string? AreaName,
    string HolDate,
    string HolidayType,
    bool IsRecuring,
    bool IsPaid,
    HolidayWorkType WorkType,
    string status);