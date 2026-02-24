namespace DTR.Core;
public class HolidayQueryService
{
    private readonly HolidayService _holidayService;
    private readonly ChangeHolidayService _changeHolidayService;
    public HolidayQueryService(HolidayService holidayService,
        ChangeHolidayService changeHolidayService)
    {
        
        _holidayService = holidayService;
        _changeHolidayService = changeHolidayService;
    }

    public async Task<Dictionary<Holidaykey, List<HolidayInfo>>> GetHolidayInfoAsync(DateOnly fromDate, DateOnly toDate,
        List<EmployeeDTRRun> employees,
        CancellationToken token)
    {
        var holidays = await _holidayService.GetAllHolidays(fromDate, toDate, token);
        var allChanged = await _changeHolidayService.GetAllChangedHolidayAsync(fromDate, toDate, token);
        //handlers
        var overrideOff = new OverrideHolidayHandler(holidays, allChanged);
        overrideOff.SetNextHandler(new FallBackHolidayHandler(holidays, allChanged));
        var holidayDic = new Dictionary<Holidaykey, List<HolidayInfo>>();
        foreach (var employee in employees)
        {
            for (DateOnly curDate = fromDate; curDate <= toDate; curDate = curDate.AddDays(1))
            {
                var result = overrideOff.Handle(employee, curDate);
                if (result.Any())
                {
                    var key = new Holidaykey(employee.Id, curDate);
                    holidayDic[key] = result;
                }
            }
        }
        return holidayDic;
    }
}