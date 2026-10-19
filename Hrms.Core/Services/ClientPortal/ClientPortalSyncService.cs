using Hrms.Domain.Entities;
using MassTransit;

namespace Hrms.Core.Services.ClientPortal;

/// <summary>
/// Sends posted DTR rows to the Client Portal over the message bus: their clients, sites, guards, scheduled headcount and the daily
/// records themselves, split into messages the portal applies one by one (<see cref="ClientPortalAttendanceUpserted"/>). The portal answers
/// each message with what it accepted and refused (<see cref="ClientPortalAttendanceResult"/>), which is what the settings screen shows.
/// </summary>
public class ClientPortalSyncService : BaseService<DailyRecord>
{
    private readonly ClientPortalSettingsService _settings;
    private readonly IPublishEndpoint _publisher;
    private readonly ITenantProvider _tenant;

    public ClientPortalSyncService(IUnitOfWorkService uow, ClientPortalSettingsService settings, IPublishEndpoint publisher, ITenantProvider tenant) : base(uow)
    {
        _settings = settings;
        _publisher = publisher;
        _tenant = tenant;
    }

    /// <summary>A whole posted batch. Null when the connection is off, or the batch is gone or not posted (nothing to send).</summary>
    public async Task<ClientPortalSyncSummary?> SyncBatchAsync(Guid dtrBatchId, string reason, CancellationToken token)
    {
        if (!await _settings.IsEnabledAsync(token))
            return null;

        var batch = await Context.DTRBatches.AsNoTracking().FirstOrDefaultAsync(x => x.Id == dtrBatchId, token);
        if (batch is null || !batch.IsPosted)
            return null;

        var records = await Context.IgnoringConfidentiality<DailyRecord>().AsNoTracking().Where(r => r.BatchCode == batch.BatchCode).ToListAsync(token);
        return await SendAsync(batch.BatchCode, records, reason, token);
    }

    /// <summary>
    /// Only some rows, for example the posted days a changed punch belongs to. Rows that are no longer posted are skipped (their removal
    /// was sent when they were unposted). Null when the connection is off or none of the rows is posted.
    /// </summary>
    public async Task<ClientPortalSyncSummary?> SyncRecordsAsync(IReadOnlyCollection<Guid> dailyRecordIds, string reason, CancellationToken token)
    {
        if (dailyRecordIds.Count == 0 || !await _settings.IsEnabledAsync(token))
            return null;

        var records = await Context.IgnoringConfidentiality<DailyRecord>().AsNoTracking()
            .Where(r => dailyRecordIds.Contains(r.Id) && r.Posted)
            .ToListAsync(token);
        if (records.Count == 0)
            return null;

        var codes = records.Select(r => r.BatchCode).Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList();
        return await SendAsync(codes.Count == 1 ? codes[0]! : string.Join(", ", codes), records, reason, token);
    }

    private async Task<ClientPortalSyncSummary?> SendAsync(string batchCode, List<DailyRecord> records, string reason, CancellationToken token)
    {
        var payload = ClientPortalPayloadBuilder.Build(await LoadAsync(records, token));
        var messages = ClientPortalPayloadBuilder.Split(payload, _tenant.TenantId, batchCode, reason);
        foreach (var message in messages)
            await _publisher.Publish(message, token);

        var summary = new ClientPortalSyncSummary(batchCode, DateTime.UtcNow, payload.Records.Count, 0, 0,
            Succeeded: true,
            Message: messages.Count == 0 ? "Nothing to send: no worked days with a client and site." : "Sent to the portal. Waiting for its answer.",
            Errors: null,
            Operation: ClientPortalAttendanceResult.Upsert,
            Waiting: messages.Count > 0,
            PartsReceived: 0,
            Parts: messages.Count);
        await _settings.RecordSyncAsync(summary, token);
        return summary;
    }

    private async Task<PortalSourceData> LoadAsync(List<DailyRecord> records, CancellationToken token)
    {
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
