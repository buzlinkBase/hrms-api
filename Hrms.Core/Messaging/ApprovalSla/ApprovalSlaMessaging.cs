using Hrms.Core.Messaging.Filter;
using Hrms.Core.Services.Approvals;
using Hrms.Domain.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Hrms.Core.Messaging.ApprovalSla;

/// <summary>Run one company's reminder / escalation sweep (the company comes from the message's tenant header).</summary>
public sealed record RunApprovalSlaSweep(DateTime QueuedAtUtc);

/// <summary>
/// Hourly Quartz job behind approval reminders and escalation. Quartz's clustered store fires it on
/// one API instance. It finds the companies that have requests waiting on a step with timing set and
/// queues one <see cref="RunApprovalSlaSweep"/> for each; <see cref="ApprovalSlaWorker"/> does the work.
/// </summary>
[DisallowConcurrentExecution]
public sealed class ApprovalSlaJob : IJob
{
    public static readonly JobKey Key = new("approval-sla", "hrms");
    public static readonly TriggerKey TriggerKey = new("approval-sla-trigger", "hrms");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ApprovalSlaJob> _logger;

    public ApprovalSlaJob(IServiceScopeFactory scopeFactory, ILogger<ApprovalSlaJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var token = context.CancellationToken;
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrmsContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var tenants = await db.ApprovalInstances.IgnoreQueryFilters()
            .Where(i => i.DeletedAt == null && i.TenantId != Guid.Empty && i.Status == ApprovalInstanceStatus.InProgress &&
                        i.ApprovalWorkflowId != null &&
                        i.Workflow!.Steps.Any(s => s.RemindAfterHours != null || s.EscalateAfterHours != null))
            .Select(i => i.TenantId)
            .Distinct()
            .ToListAsync(token);

        foreach (var tenantId in tenants)
        {
            await publisher.Publish(
                new RunApprovalSlaSweep(DateTime.UtcNow),
                ctx => ctx.Headers.Set(TenantPublishFilter<RunApprovalSlaSweep>.HeaderName, tenantId.ToString()),
                token);
        }

        _logger.LogInformation("Approval reminders: queued a sweep for {Count} company(ies)", tenants.Count);
        // One save so the bus outbox hands the scoped publishes to the broker.
        await db.SaveChangesAsync(token);
    }
}

public class ApprovalSlaWorker : IConsumer<RunApprovalSlaSweep>
{
    private readonly ApprovalSlaService _service;
    private readonly ILogger<ApprovalSlaWorker> _logger;

    public ApprovalSlaWorker(ApprovalSlaService service, ILogger<ApprovalSlaWorker> logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<RunApprovalSlaSweep> context)
    {
        var result = await _service.RunAsync(DateTime.UtcNow, context.CancellationToken);
        if (result.Total > 0)
            _logger.LogInformation("Approval reminders: {Reminded} reminded, {Escalated} escalated", result.Reminded, result.Escalated);
    }
}

public class ApprovalSlaWorkerDefinition : ConsumerDefinition<ApprovalSlaWorker>
{
    public ApprovalSlaWorkerDefinition()
    {
        EndpointName = "hrms-approval-sla-que";
        ConcurrentMessageLimit = 1;
    }
}
