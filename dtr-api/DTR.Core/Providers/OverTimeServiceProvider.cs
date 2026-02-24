using Hrms.Domain.Entities;

namespace DTR.Core;

public class OverTimeServiceProvider
{
    private readonly Dictionary<OTKey, OverTimeApplication?> _overTimeApplications;
    private readonly EmployeeDTRRun _employee;
    public OverTimeServiceProvider(Dictionary<OTKey, OverTimeApplication?> overTimeApplications, EmployeeDTRRun currentEmployee)
    {
        _overTimeApplications = overTimeApplications;
        _employee = currentEmployee;
    }

    public OverTimeApplication? GetOT(DateOnly date)
    {
        var key = new OTKey(_employee.Id, date);
        return _overTimeApplications.TryGetValue(key, out var application) ? application : null;
    }
    public bool HasOTApplication(DateOnly date)
    {
        return GetOT(date) != null;
    }
}
