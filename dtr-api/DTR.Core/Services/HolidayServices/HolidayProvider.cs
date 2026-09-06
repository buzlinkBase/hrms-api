namespace DTR.Core;

public static class HolidayProviderFactory
{
    public static HolidayProviderBase Create(EmployeeDTRRun employee, Dictionary<Holidaykey, List<HolidayInfo>> holidays)
    {
        return new DefaultHolidayProvider(holidays);
    }
}

public sealed class DefaultHolidayProvider : HolidayProviderBase
{
    public DefaultHolidayProvider(Dictionary<Holidaykey, List<HolidayInfo>> holidays) : base(holidays)
    {
    }
    public override TimeRecordCollection GetHolidayDuringDate(HolidayType type, EmployeeDTRRun employee, DateOnly date)
    {
        var shift = new CurrentShift()
        {
            //Employee = employee,
            StartTime = date.ToDateTime(TimeOnly.MinValue),
            EndTime = date.AddDays(1).ToDateTime(TimeOnly.MinValue)
        };
        return GetHolidayDuringShift(type, employee, shift);
    }
    public override TimeRecordCollection GetHolidayDuringShift(HolidayType type, EmployeeDTRRun employee, CurrentShift shift)
    {
        var shiftRange = new TimeRecord(shift.StartTime, shift.EndTime);

        var holidayKeys = new[]
        {
             new Holidaykey(employee.Id, DateOnly.FromDateTime(shift.StartTime)),
             new Holidaykey(employee.Id, DateOnly.FromDateTime(shift.EndTime))
        };

        var filterChain = new HolidayFilterChain();
        var holidaySlices = holidayKeys
            .SelectMany(key =>
                _holidays.TryGetValue(key, out var holidays)
                    ? holidays?.Where(h => h.HolType == type && filterChain.IsApplicable(h, employee)) ?? []
                    : Enumerable.Empty<HolidayInfo>())
            .GroupBy(h => new { h.HolidayId })
            .Select(g =>
            {
                var h = g.First();
                var record = new TimeRecord(
                    h.PayrollDate.ToDateTime(TimeOnly.MinValue),
                    //h.PayrollDate.ToDateTime(TimeOnly.MaxValue),
                    h.PayrollDate.AddDays(1).ToDateTime(TimeOnly.MinValue),
                    $"Holiday_{h.HolType}");

                return record.Intersect(shiftRange, $"Holiday_{type}");
            })
            .Where(r => r != null)
            .OfType<TimeRecord>();

        return holidaySlices.ToTimeRecordCollection();
    }

    public override List<HolidayInfo?> GetHolidayInfoDuringShift(HolidayType type, EmployeeDTRRun employee, CurrentShift shift)
    {
        var shiftRange = new TimeRecord(shift.StartTime, shift.EndTime);
        var holidayKeys = new[]
        {
             new Holidaykey(employee.Id, DateOnly.FromDateTime(shift.StartTime)),
             new Holidaykey(employee.Id, DateOnly.FromDateTime(shift.EndTime))
        };
        var filterChain = new HolidayFilterChain();
        var holidaySlices = holidayKeys
            .SelectMany(key =>
                _holidays.TryGetValue(key, out var holidays)
                    ? holidays?.Where(h => h.HolType == type && filterChain.IsApplicable(h, employee)) ?? []
                    : Enumerable.Empty<HolidayInfo>())
            .OrderBy(h => h.PayrollDate)
            .ThenByDescending(x => x.WorkType)
            .GroupBy(h => new { h.HolidayId })
            .Select(x => x.FirstOrDefault())
            .ToList();
        return holidaySlices;
    }
}
public abstract class HolidayProviderBase
{
    protected readonly Dictionary<Holidaykey, List<HolidayInfo>> _holidays;
    public HolidayProviderBase(Dictionary<Holidaykey, List<HolidayInfo>> holidays)
    {
        _holidays = holidays;
    }
    public abstract TimeRecordCollection GetHolidayDuringDate(HolidayType type, EmployeeDTRRun employee, DateOnly date);
    public abstract TimeRecordCollection GetHolidayDuringShift(HolidayType type, EmployeeDTRRun employee, CurrentShift shift);
    public abstract List<HolidayInfo?> GetHolidayInfoDuringShift(HolidayType type, EmployeeDTRRun employee, CurrentShift shift);
}
