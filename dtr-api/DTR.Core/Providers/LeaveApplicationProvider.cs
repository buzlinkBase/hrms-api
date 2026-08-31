using Hrms.Domain.Entities;

namespace DTR.Core;

public class LeaveApplicationProvider
{
    private readonly Dictionary<Leavekey, List<LeaveApplication>> _leaveApplications;
    private readonly EmployeeDTRRun _employee;
    public LeaveApplicationProvider(Dictionary<Leavekey, List<LeaveApplication>> leaveApplocations, EmployeeDTRRun currentEmployee)
    {
        _leaveApplications = leaveApplocations;
        _employee = currentEmployee;
    }

    public List<LeaveApplication> GetApplications(DateOnly date)
    {
        var key = new Leavekey(_employee.Id);
        if (!_leaveApplications.TryGetValue(key, out var applications) || applications == null) return new List<LeaveApplication>();

        var apps = applications
            .Where(a => a.LeaveDateFrom <= date && date <= a.LeaveDateTo)
            .ToList();
        if (!apps.Any()) return new List<LeaveApplication>();

        var leaveApps = new List<LeaveApplication>();

        foreach (var app in apps)
        {
            TimeOnly? startTimeOnly = app.StartTime.HasValue ? TimeOnly.FromDateTime(app.StartTime.Value) : null;
            TimeOnly? endTimeOnly = app.EndTime.HasValue ? TimeOnly.FromDateTime(app.EndTime.Value) : null;
            var isCross = app.EndTime.HasValue ? app.LeaveDateTo.ToDateTime(TimeOnly.MinValue).Date < app.EndTime.Value.Date : false;

            leaveApps.Add(new LeaveApplication
            {
                Id = app.Id,
                LeaveId = app.LeaveId,
                Leave = app.Leave,
                LeaveDateFrom = date,
                LeaveDateTo = date,
                AuditTrailId = app.AuditTrailId,
                DayFraction = app.DayFraction,
                DurationType = app.DurationType,
                PayType = app.PayType,
                IsManualEntry = app.IsManualEntry,
                TotalMinutes = app.TotalMinutes,
                ReviewedOn = app.ReviewedOn,
                ReviewedBy = app.ReviewedBy,
                ApprovalStatus = app.ApprovalStatus,
                ApplicationRemarks = app.ApplicationRemarks,
                StartTime = startTimeOnly.HasValue ? date.ToDateTime(startTimeOnly.Value) : null,
                EndTime = !endTimeOnly.HasValue
                    ? null
                    : isCross ? date.AddDays(1).ToDateTime(endTimeOnly.Value) : date.ToDateTime(endTimeOnly.Value)
            });
        }
        return leaveApps;
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

    //public TravelOrderApplication? GetApplication(DateOnly date)
    //{
    //    var key = new TravelKey(_employee.Id);
    //    if (!_travelApplications.TryGetValue(key, out var applications))
    //    {
    //        return null;
    //    }

    //    var app = applications.FirstOrDefault(a => a.StartDate <= date && date <= a.EndDate);
    //    if (app == null || !app.StartTime.HasValue || !app.EndTime.HasValue)
    //    {
    //        return null;
    //    }

    //    TimeOnly startTimeOnly = TimeOnly.FromDateTime(app.StartTime.Value);
    //    TimeOnly endTimeOnly = TimeOnly.FromDateTime(app.EndTime.Value);
    //    var isCross = app.EndDate.ToDateTime(TimeOnly.MinValue).Date < app.EndTime.Value.Date;

    //    return new TravelOrderApplication
    //    {
    //        Id = app.Id,
    //        ApplicationDate = app.ApplicationDate,
    //        Destination = app.Destination,
    //        Cost = app.Cost,
    //        EmployeeId = app.EmployeeId,
    //        IsManualEntry = app.IsManualEntry,
    //        Purpose = app.Purpose,
    //        Reference = app.Reference,
    //        TenantId = app.TenantId,
    //        TotalMinutes = app.TotalMinutes,
    //        Status = app.Status,
    //        Classification = app.Classification,
    //        StartDate = date,
    //        EndDate = date,
    //        StartTime = date.ToDateTime(startTimeOnly),
    //        EndTime = isCross
    //        ? date.AddDays(1).ToDateTime(endTimeOnly)
    //        : date.ToDateTime(endTimeOnly)
    //    };
    //}

    public TravelOrderApplication? GetApplication(DateOnly date)
    {
        var key = new TravelKey(_employee.Id);
        if (!_travelApplications.TryGetValue(key, out var applications))
        {
            return null;
        }

        // Find application where 'date' falls strictly within [StartDate, EndDate]
        var app = applications.FirstOrDefault(a => a.StartDate <= date && date <= a.EndDate);
        if (app == null)
        {
            return null;
        }

        // Default to full-day bounds (00:00 to 23:59:59) if times are not specified
        TimeOnly startTimeOnly = app.StartTime.HasValue
            ? TimeOnly.FromDateTime(app.StartTime.Value)
            : TimeOnly.MinValue;

        TimeOnly endTimeOnly = app.EndTime.HasValue
            ? TimeOnly.FromDateTime(app.EndTime.Value)
            : TimeOnly.MaxValue;

        // Check if EndTime spans into the day after EndDate
        bool isCross = app.EndTime.HasValue && app.EndTime.Value.Date > app.EndDate.ToDateTime(TimeOnly.MinValue).Date;

        // Determine target EndDate for this single-day evaluation
        DateTime resolvedEndTime = (isCross && date == app.EndDate)
            ? date.AddDays(1).ToDateTime(endTimeOnly)
            : date.ToDateTime(endTimeOnly);

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
            EndTime = resolvedEndTime
        };
    }

}
