using Asp.Versioning;
using BuzlinkRepository;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hrms.Api.Controllers;

[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[ApiController]
public class DashboardController : ControllerBase
{
    private readonly IUnitOfWorkService _uow;

    public DashboardController(IUnitOfWorkService uow)
    {
        _uow = uow;
    }

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview(CancellationToken token)
    {
        TimeZoneInfo phZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
        DateTime phNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, phZone);
        DateOnly today = DateOnly.FromDateTime(phNow);
        DateOnly monthStart = new DateOnly(today.Year, today.Month, 1);
        DateOnly sevenDaysAgo = today.AddDays(-6);
        DateTime thirtyDaysAgo = phNow.AddDays(-30);
        DateTime sevenDaysAgoUtc = phNow.AddDays(-7);

        // ── Stats ────────────────────────────────────────────────────────────────
        var totalEmployees = await _uow.Context.Employees.CountAsync(token);

        var presentToday = await _uow.Repository
            .Find<DailyRecord>(d => d.WorkDate == today && d.StartTime != null && d.AbsentCount == 0)
            .AsNoTracking()
            .CountAsync(token);

        var lateToday = await _uow.Repository
            .Find<DailyRecord>(d => d.WorkDate == today && d.LateMinutes > 0 && d.AbsentCount == 0)
            .AsNoTracking()
            .CountAsync(token);

        var absentToday = await _uow.Repository
            .Find<DailyRecord>(d => d.WorkDate == today && d.AbsentCount > 0)
            .AsNoTracking()
            .CountAsync(token);

        var onLeaveToday = await _uow.Repository
            .Find<DailyRecord>(d => d.WorkDate == today && d.LeaveHours > 0)
            .AsNoTracking()
            .CountAsync(token);

        var pendingLeaveCount = await _uow.Repository
            .Find<LeaveApplication>(l => l.ApprovalStatus == ApprovalStatus.ForApproval)
            .AsNoTracking()
            .CountAsync(token);

        var pendingOTCount = await _uow.Repository
            .Find<OverTimeApplication>(o => o.ApprovalStatus == ApprovalStatus.ForApproval)
            .AsNoTracking()
            .CountAsync(token);

        var newHires = await _uow.Repository
            .Find<Employee>(e => e.HireDate >= monthStart)
            .AsNoTracking()
            .CountAsync(token);

        // ── Attendance Trend (last 7 days) ────────────────────────────────────
        var trend = await _uow.Repository
            .Find<DailyRecord>(d => d.WorkDate >= sevenDaysAgo && d.WorkDate <= today)
            .AsNoTracking()
            .GroupBy(d => d.WorkDate)
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                Date    = g.Key,
                Present = g.Count(d => d.StartTime != null && d.AbsentCount == 0),
                Late    = g.Count(d => d.LateMinutes > 0 && d.AbsentCount == 0),
                Absent  = g.Count(d => d.AbsentCount > 0),
            })
            .ToListAsync(token);

        // ── Department Headcount ──────────────────────────────────────────────
        var deptHeadcount = await _uow.Repository
            .Find<Employee>(e => e.DateResigned == null && e.DepartmentId != null)
            .AsNoTracking()
            .GroupBy(e => new { DepartmentId = e.DepartmentId!.Value, DeptName = e.Department!.Name })
            .Select(g => new
            {
                DepartmentId = g.Key.DepartmentId,
                DeptName     = g.Key.DeptName,
                Count        = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .Take(8)
            .ToListAsync(token);

        // ── Upcoming Holidays ─────────────────────────────────────────────────
        var holidays = await _uow.Repository
            .Find<Holiday>(h => h.HolDate >= today)
            .AsNoTracking()
            .OrderBy(h => h.HolDate)
            .Take(5)
            .Select(h => new { h.Id, h.Description, h.HolDate, h.HolType })
            .ToListAsync(token);

        // ── Pending Requests (detail) ─────────────────────────────────────────
        var pendingLeaveApps = await _uow.Repository
            .Find<LeaveApplication>(l => l.ApprovalStatus == ApprovalStatus.ForApproval)
            .AsNoTracking()
            .OrderByDescending(l => l.CreatedAt)
            .Take(5)
            .Select(l => new { l.Id, l.EmployeeId, l.CreatedAt })
            .ToListAsync(token);

        var pendingOTApps = await _uow.Repository
            .Find<OverTimeApplication>(o => o.ApprovalStatus == ApprovalStatus.ForApproval)
            .AsNoTracking()
            .OrderByDescending(o => o.CreatedAt)
            .Take(5)
            .Select(o => new { o.Id, o.EmployeeId, o.CreatedAt })
            .ToListAsync(token);

        var pendingEmpIds = pendingLeaveApps.Select(l => l.EmployeeId)
            .Union(pendingOTApps.Select(o => o.EmployeeId))
            .ToHashSet();

        var pendingEmployees = pendingEmpIds.Count > 0
            ? await _uow.Repository
                .Find<Employee>(e => pendingEmpIds.Contains(e.Id))
                .AsNoTracking()
                .Select(e => new { e.Id, Name = e.FirstName + " " + e.LastName })
                .ToListAsync(token)
            : [];

        var pendingEmpDict = pendingEmployees.ToDictionary(e => e.Id, e => e.Name.Trim());

        var pendingRequestDetails = pendingLeaveApps
            .Select(l => new
            {
                Id           = l.Id.ToString(),
                Type         = "leave",
                EmployeeName = pendingEmpDict.GetValueOrDefault(l.EmployeeId, "Unknown"),
                SubmittedAt  = l.CreatedAt,
                Status       = "pending"
            })
            .Concat(pendingOTApps.Select(o => new
            {
                Id           = o.Id.ToString(),
                Type         = "overtime",
                EmployeeName = pendingEmpDict.GetValueOrDefault(o.EmployeeId, "Unknown"),
                SubmittedAt  = o.CreatedAt,
                Status       = "pending"
            }))
            .OrderByDescending(x => x.SubmittedAt)
            .Take(5)
            .ToList();

        // ── Recent Activity ───────────────────────────────────────────────────
        var recentLeaveApps = await _uow.Repository
            .Find<LeaveApplication>(l => l.CreatedAt >= sevenDaysAgoUtc)
            .AsNoTracking()
            .OrderByDescending(l => l.CreatedAt)
            .Take(5)
            .Select(l => new { l.Id, l.EmployeeId, l.CreatedAt })
            .ToListAsync(token);

        var recentOTApps = await _uow.Repository
            .Find<OverTimeApplication>(o => o.CreatedAt >= sevenDaysAgoUtc)
            .AsNoTracking()
            .OrderByDescending(o => o.CreatedAt)
            .Take(5)
            .Select(o => new { o.Id, o.EmployeeId, o.CreatedAt })
            .ToListAsync(token);

        var recentNewHires = await _uow.Repository
            .Find<Employee>(e => e.CreatedAt >= thirtyDaysAgo)
            .AsNoTracking()
            .OrderByDescending(e => e.CreatedAt)
            .Take(3)
            .Select(e => new { e.Id, Name = e.FirstName + " " + e.LastName, e.CreatedAt })
            .ToListAsync(token);

        var activityEmpIds = recentLeaveApps.Select(l => l.EmployeeId)
            .Union(recentOTApps.Select(o => o.EmployeeId))
            .ToHashSet();

        var activityEmployees = activityEmpIds.Count > 0
            ? await _uow.Repository
                .Find<Employee>(e => activityEmpIds.Contains(e.Id))
                .AsNoTracking()
                .Select(e => new { e.Id, Name = e.FirstName + " " + e.LastName })
                .ToListAsync(token)
            : [];

        var activityEmpDict = activityEmployees.ToDictionary(e => e.Id, e => e.Name.Trim());

        var recentActivity = recentLeaveApps
            .Select(l => new
            {
                Id           = l.Id.ToString(),
                Type         = "leave-request",
                EmployeeName = activityEmpDict.GetValueOrDefault(l.EmployeeId, "Unknown"),
                Description  = "Filed a leave application",
                Timestamp    = l.CreatedAt
            })
            .Concat(recentOTApps.Select(o => new
            {
                Id           = o.Id.ToString(),
                Type         = "schedule-change",
                EmployeeName = activityEmpDict.GetValueOrDefault(o.EmployeeId, "Unknown"),
                Description  = "Filed an overtime application",
                Timestamp    = o.CreatedAt
            }))
            .Concat(recentNewHires.Select(e => new
            {
                Id           = e.Id.ToString(),
                Type         = "new-hire",
                EmployeeName = e.Name.Trim(),
                Description  = "Joined the organization",
                Timestamp    = e.CreatedAt
            }))
            .OrderByDescending(a => a.Timestamp)
            .Take(10)
            .ToList();

        // ── Response ──────────────────────────────────────────────────────────
        return Ok(new
        {
            stats = new
            {
                totalEmployees,
                presentToday,
                lateToday,
                absentToday,
                onLeaveToday,
                pendingRequests   = pendingLeaveCount + pendingOTCount,
                newHiresThisMonth = newHires,
            },
            attendanceTrend = trend.Select(t => new
            {
                date    = t.Date.ToString("ddd"),
                present = t.Present,
                late    = t.Late,
                absent  = t.Absent,
            }),
            departmentHeadcount = deptHeadcount.Select(d => new
            {
                departmentId   = d.DepartmentId.ToString(),
                departmentName = d.DeptName,
                headcount      = d.Count,
            }),
            upcomingHolidays = holidays.Select(h => new
            {
                id   = h.Id.ToString(),
                name = h.Description,
                date = h.HolDate.ToString("yyyy-MM-dd"),
                type = h.HolType == HolidayType.LEGAL ? "Regular" : "Special",
            }),
            recentActivity = recentActivity.Select(a => new
            {
                id           = a.Id,
                type         = a.Type,
                employeeName = a.EmployeeName,
                description  = a.Description,
                timestamp    = a.Timestamp,
            }),
            pendingRequests = pendingRequestDetails.Select(p => new
            {
                id           = p.Id,
                type         = p.Type,
                employeeName = p.EmployeeName,
                submittedAt  = p.SubmittedAt,
                status       = p.Status,
            }),
        });
    }
}
