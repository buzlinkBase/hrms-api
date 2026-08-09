using Hrms.Domain.Entities;

namespace DTR.Core;

public class LeaveApplicationProvider
{
    private readonly Dictionary<Leavekey, List<LeaveApplication>> _leaveApplications;
    private readonly EmployeeDTRRun _employee;
    public LeaveApplicationProvider(Dictionary<Leavekey, List<LeaveApplication>> leaveApplocations,
        EmployeeDTRRun currentEmployee)
    {
        _leaveApplications = leaveApplocations;
        _employee = currentEmployee;
    }
    public LeaveApplication? GetApplication(DateOnly date)
    {
        var key = new Leavekey(_employee.Id);
        if (_leaveApplications.TryGetValue(key, out var applications))
        {
            return applications.FirstOrDefault(app =>
                app.LeaveDateFrom <= date && date <= app.LeaveDateTo);
        }
        return null;
    }
}


public class TravelApplicationProvider
{
    private readonly Dictionary<TravelKey, List<TravelOrderApplication>> _travelApplications;
    private readonly EmployeeDTRRun _employee;
    public TravelApplicationProvider(Dictionary<TravelKey, List<TravelOrderApplication>> travels,
        EmployeeDTRRun currentEmployee)
    {
        _travelApplications = travels;
        _employee = currentEmployee;
    }

    public TravelOrderApplication? GetApplication(DateOnly date)
    {
        var key = new TravelKey(_employee.Id);
        if (!_travelApplications.TryGetValue(key, out var applications))
        {
            return null;
        }

        var app = applications.FirstOrDefault(a => a.StartDate <= date && date <= a.EndDate);
        if (app == null || !app.StartTime.HasValue || !app.EndTime.HasValue)
        {
            return null;
        }

        TimeOnly startTimeOnly = TimeOnly.FromDateTime(app.StartTime.Value);
        TimeOnly endTimeOnly = TimeOnly.FromDateTime(app.EndTime.Value);
        var isCross = app.EndDate.ToDateTime(TimeOnly.MinValue).Date < app.EndTime.Value.Date;

        return new TravelOrderApplication
        {
            Id = app.Id,
            ApplicationDate = app.ApplicationDate,
            Destination = app.Destination,
            Cost = app.Cost,
            EmployeeId = app.EmployeeId,
            IsManualEntry = app.IsManualEntry,
            Purpose = app.Purpose,
            Reference = app.Reference,
            TenantId = app.TenantId,
            TotalMinutes = app.TotalMinutes,
            Status = app.Status,
            Classification = app.Classification,
            StartDate = date,
            EndDate = date,
            StartTime = date.ToDateTime(startTimeOnly),
            EndTime = isCross
            ? date.AddDays(1).ToDateTime(endTimeOnly)
            : date.ToDateTime(endTimeOnly)
        };
    }
}
