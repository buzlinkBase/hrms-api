using DTR.Core;
using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;
using Onepunch.Common.Lib.Exceptions;

namespace Hrms.Core.Services;

/// <summary>One punch the employee made (or was recorded) today.</summary>
public sealed record PortalPunchEntry(DateTime At, LOGSOURCE Source);

/// <summary>
/// What the Punch In/Out page shows. All times are Manila wall-clock, like every other stored punch.
/// <see cref="NextAllowedAt"/> is null when the employee can punch right now.
/// </summary>
public sealed record PortalPunchStatus(
    DateTime Now,
    int CooldownMinutes,
    DateTime? LastPunchAt,
    DateTime? NextAllowedAt,
    IReadOnlyList<PortalPunchEntry> Today);

/// <summary>
/// Punching from My Portal instead of a biometric device or the mobile app. The caller (MeController)
/// has already checked the company switched the page on; this only enforces the cooldown -- no two
/// punches closer than the company's PortalPunchCooldownMinutes, counted against the employee's latest
/// punch of ANY source, so tapping the device and then the portal can't double-punch either.
/// The log goes through <see cref="AttendanceService.AddRangeAsync"/> like every other source.
/// Works inside the caller's unit of work; the caller commits.
/// </summary>
public class PortalPunchService : BaseService<Attendance>
{
    private static readonly TimeZoneInfo Manila = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");

    private readonly AttendanceService _attendance;
    private readonly GeneralSettingService _settings;
    private readonly TimeProvider _clock;

    public PortalPunchService(
        IUnitOfWorkService uow,
        AttendanceService attendance,
        GeneralSettingService settings,
        TimeProvider? clock = null) : base(uow)
    {
        _attendance = attendance;
        _settings = settings;
        _clock = clock ?? TimeProvider.System;
    }

    private DateTime ManilaNow() => TimeZoneInfo.ConvertTimeFromUtc(_clock.GetUtcNow().UtcDateTime, Manila);

    private async Task<int> CooldownMinutesAsync()
    {
        var s = await _settings.GetSettingsAsync(PayrollSettingsIdentity.IdentityType);
        var minutes = s.TryGetValue(PayrollSettingsIdentity.KeyPortalPunchCooldownMinutes, out var setting)
            ? GeneralSettingsUtil.ParseInt(setting.Value, PayrollSettingsIdentity.DefaultPortalPunchCooldownMinutes)
            : PayrollSettingsIdentity.DefaultPortalPunchCooldownMinutes;
        return Math.Clamp(minutes, 1, 120);
    }

    // The employee's latest punch that has already happened (a manual log dated ahead doesn't block).
    private async Task<DateTime?> LatestPunchAsync(Guid employeeId, DateTime now, CancellationToken token)
    {
        var latest = await Context.Attendances.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.WorkDateTime <= now)
            .OrderByDescending(x => x.WorkDateTime)
            .Select(x => (DateTime?)x.WorkDateTime)
            .FirstOrDefaultAsync(token);
        return latest;
    }

    public async Task<PortalPunchStatus> GetStatusAsync(Guid employeeId, CancellationToken token)
    {
        var now = ManilaNow();
        var cooldown = await CooldownMinutesAsync();
        var last = await LatestPunchAsync(employeeId, now, token);
        var today = now.Date;

        var entries = await Context.Attendances.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.WorkDateTime >= today && x.WorkDateTime < today.AddDays(1))
            .OrderBy(x => x.WorkDateTime)
            .Select(x => new PortalPunchEntry(x.WorkDateTime, x.LogSource))
            .ToListAsync(token);

        return new PortalPunchStatus(now, cooldown, last, NextAllowed(last, cooldown, now), entries);
    }

    /// <summary>Records a punch for the employee at the server's current time and returns that time. Throws when too soon.</summary>
    public async Task<DateTime> PunchAsync(Guid employeeId, CancellationToken token)
    {
        var now = ManilaNow();
        var cooldown = await CooldownMinutesAsync();
        var last = await LatestPunchAsync(employeeId, now, token);

        if (NextAllowed(last, cooldown, now) is { } next)
        {
            var wait = next - now;
            var seconds = (int)Math.Ceiling(wait.TotalSeconds);
            throw new GuardException(
                $"You punched too recently. Please wait {seconds / 60}m {seconds % 60:00}s before punching again.");
        }

        var employee = await Context.Employees.AsNoTracking()
            .Where(x => x.Id == employeeId)
            .Select(x => new ManualAttendanceEmployee(x.Id, x.BioId, x.BranchId, x.DepartmentId, x.ClientId, x.AreaId))
            .FirstOrDefaultAsync(token)
            ?? throw new NotFoundException("Employee not found.");

        var branchOfArea = await AttendanceLocationResolver.LoadBranchOfAreaAsync(Context, [employee.AreaId], token);
        var location = AttendanceLocationResolver.Resolve(AttendanceLocation.None, employee.Location, branchOfArea);

        // Whole seconds, unspecified kind: stored as wall-clock like every other punch.
        var punchAt = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second);
        await _attendance.AddRangeAsync(
        [
            new Attendance
            {
                BatchCode = "PORTAL-" + now.ToString("yyyyMMdd"),
                EmployeeId = employee.Id,
                BioId = employee.BioId,
                WorkDateTime = punchAt,
                BranchId = location.BranchId,
                DepartmentId = location.DepartmentId,
                ClientId = location.ClientId,
                OperationAreaId = location.OperationAreaId,
                DeviceName = "Employee Portal",
                IP = string.Empty,
                LogSource = LOGSOURCE.PORTAL,
            },
        ], token);

        return punchAt;
    }

    // After the cooldown the next punch is allowed; null when it already is.
    private static DateTime? NextAllowed(DateTime? last, int cooldownMinutes, DateTime now)
    {
        if (last is not { } at) return null;
        var next = at.AddMinutes(cooldownMinutes);
        return next > now ? next : null;
    }
}
