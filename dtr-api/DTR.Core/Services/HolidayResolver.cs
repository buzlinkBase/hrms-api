
namespace DTR.Core.Services;

public class HolidayResolver
{
    private readonly HolidayQueryService _service;
    public HolidayResolver(HolidayQueryService service)
    {
        _service = service;
    }

    public async Task<Dictionary<Holidaykey, List<HolidayInfo>>> 
        ResolveAsync(DateOnly from,
        DateOnly dateTo,
     List<EmployeeDTRRun> employees,
     CancellationToken token)
    {
        return await _service.GetHolidayInfoAsync(from, dateTo, employees, token);
    }
}
