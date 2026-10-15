using Hrms.Domain.Entities;

namespace Hrms.Core.Services.ClientPortal;

/// <summary>
/// Sends one posted DTR batch to the Client Portal: its clients, sites, guards, scheduled headcount and daily records, parents
/// first. A rejected row is reported back in the summary rather than failing the batch (resending the same bad row would only
/// be rejected again); a portal that cannot be reached throws so the message is retried.
/// </summary>
public class ClientPortalSyncService : BaseService<DailyRecord>
{
    private readonly ClientPortalSettingsService _settings;
    private readonly IClientPortalClient _client;

    public ClientPortalSyncService(IUnitOfWorkService uow, ClientPortalSettingsService settings, IClientPortalClient client) : base(uow)
    {
        _settings = settings;
        _client = client;
    }

    /// <summary>Null when the connection is off, or the batch is gone or not posted (nothing to send).</summary>
    public async Task<ClientPortalSyncSummary?> SyncBatchAsync(Guid dtrBatchId, CancellationToken token)
    {
        var credentials = await _settings.GetCredentialsAsync(token);
        if (credentials is null)
            return null;

        var batch = await Context.DTRBatches.AsNoTracking().FirstOrDefaultAsync(x => x.Id == dtrBatchId, token);
        if (batch is null || !batch.IsPosted)
            return null;

        try
        {
            var payload = ClientPortalPayloadBuilder.Build(await LoadAsync(batch.BatchCode, token));
            var results = new[]
            {
                await _client.PutAsync(credentials, "clients", payload.Clients, token),
                await _client.PutAsync(credentials, "sites", payload.Sites, token),
                await _client.PutAsync(credentials, "posts", payload.Posts, token),
                await _client.PutAsync(credentials, "guards", payload.Guards, token),
                await _client.PutAsync(credentials, "schedules", payload.Schedules, token),
                await _client.PutAsync(credentials, "dtr", payload.Records, token),
            };

            var errors = results.SelectMany(r => r.Errors).Take(50).ToList();
            var dtr = results[^1];
            var summary = new ClientPortalSyncSummary(batch.BatchCode, DateTime.UtcNow, payload.Records.Count, dtr.Accepted, results.Sum(r => r.Rejected),
                Succeeded: errors.Count == 0, Message: errors.Count == 0 ? null : "Some rows were refused by the portal.", Errors: errors);
            await _settings.RecordSyncAsync(summary, token);
            return summary;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await _settings.RecordSyncAsync(new ClientPortalSyncSummary(batch.BatchCode, DateTime.UtcNow, 0, 0, 0, false, "The portal could not be reached. It will be retried.", null), token);
            throw;
        }
    }

    private async Task<PortalSourceData> LoadAsync(string batchCode, CancellationToken token)
    {
        var records = await Context.DailyTimeRecords.AsNoTracking().Where(r => r.BatchCode == batchCode).ToListAsync(token);

        var clientIds = records.Where(r => r.ClientId != null).Select(r => r.ClientId!.Value).Distinct().ToList();
        var areaIds = records.Where(r => r.AreaId != null).Select(r => r.AreaId!.Value).Distinct().ToList();
        var employeeIds = records.Select(r => r.EmployeeId).Distinct().ToList();
        var shiftIds = records.Where(r => r.ShiftId != null).Select(r => r.ShiftId!.Value).Distinct().ToList();

        var clients = await Context.Clients.AsNoTracking().Where(c => clientIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, token);
        var areas = await Context.Areas.AsNoTracking().Where(a => areaIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, token);
        var employees = await Context.Employees.AsNoTracking().Where(e => employeeIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, token);
        var breaks = await Context.TimeShifts.AsNoTracking().Where(s => shiftIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.BreakDurationMinutes, token);

        var worked = records.Where(r => r.StartTime != null || r.EndTime != null).ToList();
        var punches = new List<Attendance>();
        var scheduled = new List<ScheduledHeadcount>();
        if (records.Count > 0)
        {
            var from = records.Min(r => r.WorkDate);
            var to = records.Max(r => r.WorkDate);
            var fromTime = from.ToDateTime(TimeOnly.MinValue).AddDays(-1);
            var toTime = to.ToDateTime(TimeOnly.MaxValue).AddDays(2);
            var workedEmployees = worked.Select(r => r.EmployeeId).Distinct().ToList();
            punches = await Context.Attendances.AsNoTracking()
                .Where(p => p.EmployeeId != null && workedEmployees.Contains(p.EmployeeId.Value) && p.WorkDateTime >= fromTime && p.WorkDateTime <= toTime)
                .ToListAsync(token);

            var plans = await Context.WorkSchedulePlans.AsNoTracking()
                .Where(p => p.PayrollDate >= from && p.PayrollDate <= to && p.Employee.ClientId != null && p.Employee.AreaId != null && clientIds.Contains(p.Employee.ClientId.Value))
                .Select(p => new { ClientId = p.Employee.ClientId!.Value, AreaId = p.Employee.AreaId!.Value, p.PayrollDate, ShiftName = p.TimeShift.ShiftName })
                .ToListAsync(token);
            scheduled = plans.GroupBy(p => (p.ClientId, p.AreaId, p.PayrollDate, p.ShiftName))
                .Select(g => new ScheduledHeadcount(g.Key.ClientId, g.Key.AreaId, g.Key.PayrollDate, g.Key.ShiftName, g.Count()))
                .ToList();
        }

        return new PortalSourceData(records, clients, areas, employees, breaks, punches, scheduled);
    }
}
