

using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;

namespace DTR.Core.Services;

public class RestDayResolver
{
    private readonly ChangeRestDayService _changeRestDayService;
    private readonly RestDayDateService _restDayDateService;
    public RestDayResolver(ChangeRestDayService changeRestDayService,
        RestDayDateService  restDayDateService)
    {
        _changeRestDayService = changeRestDayService;
        _restDayDateService = restDayDateService;
    }
    public async Task<Dictionary<ResDaykey, CurrentRestDay>> ResolveAsync(DateOnly fromDate, DateOnly toDate,
    List<EmployeeDTRRun> employees,
    CancellationToken token)
    {

        var employeeIds = new HashSet<Guid>(employees.Select(e => e.Id));
        var allOff = await _changeRestDayService.GetChangeRestDays(fromDate, toDate, employeeIds,token);
        var allSpecDates = await _restDayDateService.LoadRestDayDate(fromDate, toDate, employeeIds, token);
        var overrideOff = new OverrideDayOffHandler(allOff);
        var specDateOff = new SpecificDateOffHandler(allSpecDates);
        var fallback = new FallBackDayOffHandler(allOff);
        overrideOff.SetNextHandler(specDateOff);
        specDateOff.SetNextHandler(fallback);

        var restDayCollection = new Dictionary<ResDaykey, CurrentRestDay>();
        for (DateOnly curdate = fromDate; curdate <= toDate; curdate = curdate.AddDays(1))
        {
            foreach (var employee in employees)
            {
                var result = overrideOff.Handle(employee, curdate);
                if (result != null)
                {
                    var key = new ResDaykey(employee.Id, curdate);
                    restDayCollection[key] = result;
                }
            }
        }
        return restDayCollection;
    }
}

public abstract class RestDayHandler
{
    protected RestDayHandler NextHandler { get; set; }
    public void SetNextHandler(RestDayHandler handler)
    {
        NextHandler = handler;
    }
    protected abstract bool IsApplicable(EmployeeDTRRun employee, DateOnly payrollDate);
    public CurrentRestDay? Handle(EmployeeDTRRun employee, DateOnly payrollDate)
    {
        if (IsApplicable(employee, payrollDate))
        {
            return GetCurrentOff(employee, payrollDate);
        }
        else if (NextHandler != null)
        {
            return NextHandler.Handle(employee, payrollDate);
        }
        return null;
    }
    protected abstract CurrentRestDay? GetCurrentOff(EmployeeDTRRun employee, DateOnly payrollDate);
}
public class OverrideDayOffHandler : RestDayHandler
{
    private readonly Dictionary<ResDaykey, List<ChangeRestDay>> _changeOffList;
    private ChangeRestDay? _replacementOff;
    public OverrideDayOffHandler(Dictionary<ResDaykey, List<ChangeRestDay>> changeOffList)
    {
        _changeOffList = changeOffList
            .Where(kvp => kvp.Value.Any(x => x.State == ChangeSchedState.REPLACEMENT))
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Where(x => x.State == ChangeSchedState.REPLACEMENT).ToList()
                );
        ;
        _replacementOff = new ChangeRestDay();
    }
    protected override CurrentRestDay? GetCurrentOff(EmployeeDTRRun employee, DateOnly payrollDate)
    {
        if (_replacementOff == null) return default;
        return new CurrentRestDay
        {
            Employee = employee,
            DayName = _replacementOff.DayName,
            PayrollDate = _replacementOff.PayrollDate,
            State = ChangeSchedState.REPLACEMENT
        };
    }
    protected override bool IsApplicable(EmployeeDTRRun employee, DateOnly payrollDate)
    {
        if (_changeOffList == null || _changeOffList.Count == 0)
        {
            return false;
        }
        var key = new ResDaykey(employee.Id, payrollDate);
        _replacementOff = _changeOffList.TryGetValue(key, out var results) && results != null ? results.FirstOrDefault() : null;
        return _replacementOff != null;
    }
}
public class SpecificDateOffHandler : RestDayHandler
{
    private readonly Dictionary<ResDaykey, RestDayDate?> _restDates;

    public SpecificDateOffHandler(Dictionary<ResDaykey, RestDayDate?> restDates)
    {
        _restDates = restDates;
    }
    protected override CurrentRestDay? GetCurrentOff(EmployeeDTRRun employee, DateOnly payrollDate)
    {
        DayName dayEnum = (DayName)payrollDate.DayOfWeek;
        return new CurrentRestDay
        {
            Employee = employee,
            DayName = dayEnum,
            PayrollDate = payrollDate,
            State = ChangeSchedState.DEFAULT
        };
    }

    protected override bool IsApplicable(EmployeeDTRRun employee, DateOnly payrollDate)
    {
        var key = new ResDaykey(employee.Id, payrollDate);
        return _restDates.TryGetValue(key, out var restday) && restday != null;
    }
}
public class FallBackDayOffHandler : RestDayHandler
{
    private readonly List<ChangeRestDay> _cancelledOutDayOff;
    public FallBackDayOffHandler(Dictionary<ResDaykey, List<ChangeRestDay>> offs)
    {
        _cancelledOutDayOff = offs
            .Values.SelectMany(x => x
                .Where(x => x.State == ChangeSchedState.OVERRIDEN)
                .Select(x => x))
            .ToList();
        ;
    }

    protected override CurrentRestDay? GetCurrentOff(EmployeeDTRRun employee, DateOnly payrollDate)
    {
        DayName dayEnum = (DayName)payrollDate.DayOfWeek;
        return new CurrentRestDay
        {
            Employee = employee,
            DayName = dayEnum,
            PayrollDate = payrollDate,
            State = ChangeSchedState.DEFAULT
        };
    }

    protected override bool IsApplicable(EmployeeDTRRun employee, DateOnly payrollDate)
    {
        if (employee == null || employee.RestDays == null || employee.RestDays.Count == 0)
            return false;

        DayName dayEnum = (DayName)payrollDate.DayOfWeek;

        // Check if the current day is overridden in _cancelledOutDayOff
        bool hasValidRestDay = employee.RestDays
            .Where(restDay => restDay.DayName == dayEnum)
            .Any(restDay => !_cancelledOutDayOff.Any(cancelled => cancelled.DayName == restDay.DayName
                                                 && cancelled.EmployeeId == employee.Id
                                                 && cancelled.PayrollDate == payrollDate));

        return hasValidRestDay;
    }
}
