using Hrms.Core.Services.ClientPortal;
using MassTransit;

namespace Hrms.Core.Messaging;

/// <summary>
/// The Client Portal's answer to attendance HRIS sent or removed: how many rows it accepted, and which it refused (a day the client already
/// verified, or a date in a locked period). Kept as the connection's last sync, which is what the settings screen shows.
/// </summary>
public class ClientPortalAttendanceResultWorker : IConsumer<ClientPortalAttendanceResult>
{
    private readonly ClientPortalSettingsService _settings;

    public ClientPortalAttendanceResultWorker(ClientPortalSettingsService settings)
    {
        _settings = settings;
    }

    public async Task Consume(ConsumeContext<ClientPortalAttendanceResult> context)
    {
        var m = context.Message;
        await _settings.RecordResultAsync(m, context.CancellationToken);
        if (m.Rejected > 0)
            Log.Warning("Client portal [{Tenant}] refused {Rejected} of {Records} row(s) of {Batch} ({Operation}, part {Part}/{Parts})",
                m.TenantId, m.Rejected, m.Records, m.BatchCode, m.Operation, m.Part, m.Parts);
    }
}

public class ClientPortalAttendanceResultWorkerDefinition : ConsumerDefinition<ClientPortalAttendanceResultWorker>
{
    public ClientPortalAttendanceResultWorkerDefinition()
    {
        EndpointName = "hrms-client-portal-result-que";
        // The answers of one send are added up into one saved summary, so they are applied one at a time.
        ConcurrentMessageLimit = 1;
    }
}
