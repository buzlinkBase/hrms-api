using Hrms.Domain.Entities;

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
        await CreateAsync(holiday, token);
        await CommitChangesAsync(token);
        return _mapper.Map<HolidayModel>(holiday);
    }
    public async Task<HolidayModel> UpdateAsync(UpdateHoliday model, CancellationToken token)
    {
        var holiday = _mapper.Map<Holiday>(model);
        holiday.HolYear = model.HolDate.Year;
        holiday.Id = model.Id;
        await ModifyAsync(holiday, token);
        await CommitChangesAsync(token);
        return _mapper.Map<HolidayModel>(holiday);
    }

    public async Task<List<HolidayResult>> FindAllAsync(int year, CancellationToken token)
    {
        return _uow.Repository.Find<Holiday>(x =>
        x.IsRecuring || x.HolYear == year)
            .Select(x => new HolidayResult(x.Id,
             x.Description,
             x.AreaId,
             x.Area.Name,
             x.IsRecuring ? $"{x.HolDate.ToString("MMM dd,")} {year}" : x.HolDate.ToString("MMM dd, yyyy"),
             x.HolType == HolidayType.LEGAL ? "Legal" : "Special",
             x.IsRecuring,
             x.IsPaid,
             x.WorkType,
             x.Status))
            .ToList()
            ;
    }
    public async Task<List<HolidayModel>> GetAllHolidays(DateOnly from, DateOnly to,
        CancellationToken token)
    {
        from = from.AddDays(-4);
        var holidays = await _uow.Repository
            .Find<Holiday>(x => x.IsRecuring || (x.HolDate >= from && x.HolDate <= to))
            .ToListAsync(token)
            ;

        return holidays
            .Select(x =>
            {
                DateOnly holDate;
                if (x.IsRecuring)
                {
                    holDate = new DateOnly(from.Year, x.HolDate.Month, x.HolDate.Day);
                    if (from.Year != to.Year)
                    {
                        var year = x.HolDate.Month >= from.Month ? from.Year : to.Year;
                        holDate = new DateOnly(year, x.HolDate.Month, x.HolDate.Day);
                    }
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
            result.HolDate = DateOnly.FromDateTime(new DateTime(result.HolYear, result.HolDate.Month, result.HolDate.Day));
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