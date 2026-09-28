using Hrms.Core.Services;
using Hrms.Core.Services.Auditing;
using MassTransit;
using Onepunch.AuditEngine;
using Onepunch.AuditEngine.MassTransit;

namespace Hrms.Core.Messaging;

// Keeps a read copy of the audit events AuthApi (sign-ins/sign-outs) and TenantApi (role and
// membership changes) record in their own databases, so Security > Audit lists everything for a
// company from hrms-api alone. TenantConsumeFilter has already switched this scope to the
// company named in the message's X-Tenant-ID header, so the copy lands in that company's
// database. Stored exactly as received (the sender's hash chain intact) and idempotent by event
// id, so a redelivered message is harmless. The sending service's own table stays the
// authoritative record; if this copy lags, nothing is lost.
public class AuditReplicaWorker : IConsumer<AuditEventRecorded>
{
    private readonly IAuditReplicaWriter _replicas;
    private readonly IUnitOfWorkService _uow;

    public AuditReplicaWorker(IAuditReplicaWriter replicas, IUnitOfWorkService uow)
    {
        _replicas = replicas;
        _uow = uow;
    }

    public async Task Consume(ConsumeContext<AuditEventRecorded> context)
    {
        // hrms-api's own events are written locally, never through here.
        if (context.Message.Source == HrmsAuditRegistration.SourceName) return;

        await _replicas.WriteReplicaAsync(context.Message.ToEvent(), context.CancellationToken);
        await _uow.CommitChangesAsync("", context.CancellationToken);
    }
}
