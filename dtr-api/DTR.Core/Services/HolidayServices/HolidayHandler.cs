using System;
using System.Collections.Generic;
using System.Linq;
using Hrms.Domain.Entities;

namespace DTR.Core;

public abstract class HolidayHandler
{
    protected readonly List<HolidayModel> Holidays;
    protected readonly Dictionary<Holidaykey, List<ChangeHoliday>> Changes;
    protected HolidayHandler NextHandler { get; set; }

    protected HolidayHandler(List<HolidayModel> holidays, Dictionary<Holidaykey, List<ChangeHoliday>> changes)
    {
        Holidays = holidays ?? new();
        Changes = changes ?? new();
    }

    public void SetNextHandler(HolidayHandler handler) => NextHandler = handler;

    public List<HolidayInfo> Handle(EmployeeDTRRun employee, DateOnly payrollDate)
    {
        var result = GetCurrentHoliday(employee, payrollDate);
        if (result != null && result.Count > 0)
        {
            return result;
        }

        return NextHandler?.Handle(employee, payrollDate) ?? new List<HolidayInfo>();
    }

    protected abstract List<HolidayInfo> GetCurrentHoliday(EmployeeDTRRun employee, DateOnly payrollDate);
}

public class OverrideHolidayHandler : HolidayHandler
{
    private readonly Dictionary<Holidaykey, List<HolidayInfo>> _holidayCache = new();

    public OverrideHolidayHandler(List<HolidayModel> holidays, Dictionary<Holidaykey, List<ChangeHoliday>> changes)
        : base(holidays, changes) { }

    protected override List<HolidayInfo> GetCurrentHoliday(EmployeeDTRRun employee, DateOnly payrollDate)
    {
        var key = new Holidaykey(employee.Id, payrollDate);

        if (_holidayCache.TryGetValue(key, out var cached))
            return cached;

        var result = new List<HolidayInfo>();

        // Directly query the dictionary for O(1) lookup
        if (Changes.TryGetValue(key, out var keyChanges) && Holidays.Count > 0)
        {
            var trackedHolidayIds = Holidays.Select(h => h.Id).ToHashSet();

            result = keyChanges
                .Where(x => x.State == ChangeSchedState.REPLACEMENT && trackedHolidayIds.Contains(x.HolidayId))
                .Select(x => new HolidayInfo
                {
                    EmployeeId = x.EmployeeId,
                    HolType = x.Holiday.HolType,
                    AreaId = x.Holiday.AreaId,
                    PayrollDate = x.PayrollDate,
                    State = ChangeSchedState.REPLACEMENT,
                    WorkType = x.Holiday.WorkType,
                    HolidayId = x.HolidayId,
                    IsPaid = x.Holiday.IsPaid,
                })
                .ToList();
        }

        _holidayCache[key] = result;
        return result;
    }
}

public class FallBackHolidayHandler : HolidayHandler
{
    private readonly Dictionary<Holidaykey, List<HolidayInfo>> _holidayCache = new();

    public FallBackHolidayHandler(List<HolidayModel> holidays, Dictionary<Holidaykey, List<ChangeHoliday>> changes)
        : base(holidays, changes) { }

    protected override List<HolidayInfo> GetCurrentHoliday(EmployeeDTRRun employee, DateOnly payrollDate)
    {
        var key = new Holidaykey(employee.Id, payrollDate);

        if (_holidayCache.TryGetValue(key, out var cached))
            return cached;

        // Filter holidays relevant ONLY to the current payroll date
        var dateHolidays = Holidays.Where(h => h.HolDate == payrollDate).ToList();
        if (dateHolidays.Count == 0)
        {
            _holidayCache[key] = new List<HolidayInfo>();
            return _holidayCache[key];
        }

        // Check if overridden for this employee and date
        var overriddenIds = new HashSet<Guid>();
        if (Changes.TryGetValue(key, out var keyChanges))
        {
            overriddenIds = keyChanges
                .Where(x => x.State == ChangeSchedState.OVERRIDEN)
                .Select(x => x.HolidayId)
                .ToHashSet();
        }

        var result = dateHolidays
            .Where(h => !overriddenIds.Contains(h.Id))
            .Select(x => new HolidayInfo
            {
                EmployeeId = employee.Id,
                HolType = x.HolType,
                AreaId = x.AreaId,
                PayrollDate = payrollDate,
                State = ChangeSchedState.DEFAULT,
                WorkType = x.WorkType,
                HolidayId = x.Id,
                IsPaid = x.IsPaid,
            })
            .ToList();

        _holidayCache[key] = result;
        return result;
    }
}