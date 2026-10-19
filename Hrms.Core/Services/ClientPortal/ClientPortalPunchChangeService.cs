using Hrms.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Hrms.Core.Services.ClientPortal;

/// <summary>
/// A raw punch was edited or deleted. The portal shows punches as the evidence behind each posted day, so the posted days the punch can belong
/// to are sent again (see <see cref="ClientPortalSyncRequestService.StageRecordsAsync"/>). Staged WITHOUT committing, through the outbox, and
/// never fails the punch change it rides on.
/// </summary>
public class ClientPortalPunchChangeService : BaseService<DailyRecord>
{
    // A punch can belong to the day before (night shift ending after midnight) or the day after (an early time-in): same window the
    // payload builder searches for punches, rounded out to whole days.
    private const int DaysAround = 1;

    private readonly ClientPortalSyncRequestService _sync;
    private readonly ILogger<ClientPortalPunchChangeService> _logger;

    public ClientPortalPunchChangeService(IUnitOfWorkService uow, ClientPortalSyncRequestService sync, ILogger<ClientPortalPunchChangeService> logger) : base(uow)
    {
        _sync = sync;
        _logger = logger;
    }

    /// <summary>Each punch as its employee and time (for an edit, pass both the old and the new time). Punches with no employee are ignored.</summary>
    public async Task<bool> StageAsync(IReadOnlyCollection<(Guid? EmployeeId, DateTime At)> punches, CancellationToken token)
    {
        var known = punches.Where(p => p.EmployeeId is not null).Select(p => (EmployeeId: p.EmployeeId!.Value, Day: DateOnly.FromDateTime(p.At))).Distinct().ToList();
        if (known.Count == 0)
            return false;

        try
        {
            var employeeIds = known.Select(p => p.EmployeeId).Distinct().ToList();
            var from = known.Min(p => p.Day).AddDays(-DaysAround);
            var to = known.Max(p => p.Day).AddDays(DaysAround);
            var candidates = await Context.IgnoringConfidentiality<DailyRecord>().AsNoTracking()
                .Where(r => r.Posted && employeeIds.Contains(r.EmployeeId) && r.WorkDate >= from && r.WorkDate <= to)
                .Select(r => new { r.Id, r.EmployeeId, r.WorkDate })
                .ToListAsync(token);

            var ids = candidates
                .Where(r => known.Any(p => p.EmployeeId == r.EmployeeId && Math.Abs(r.WorkDate.DayNumber - p.Day.DayNumber) <= DaysAround))
                .Select(r => r.Id)
                .ToList();
            return await _sync.StageRecordsAsync(ids, ClientPortalSyncRequested.PunchChanged, token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The punch is saved whatever happens to the portal; "send again" on the batch brings the portal up to date.
            _logger.LogWarning(ex, "Could not queue the Client Portal refresh for {Count} changed punch(es).", known.Count);
            return false;
        }
    }
}
