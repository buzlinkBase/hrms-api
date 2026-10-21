using Hrms.Core.Services.ClientPortal;
using Hrms.Core.Services.Deployments;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Hrms.Core.Messaging.Deployments;

/// <summary>
/// Daily, just after midnight Philippine time: regular deployments that start today (entered ahead, e.g. a transfer next week) move
/// their employees' client and project site, for every company at once; then each company with the Client Portal on sends the coming
/// week's manning there.
/// </summary>
[DisallowConcurrentExecution]
public sealed class DeploymentSyncJob : IJob
{
    public static readonly JobKey Key = new("deployment-sync", "hrms");
    public static readonly TriggerKey TriggerKey = new("deployment-sync-trigger", "hrms");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeploymentSyncJob> _logger;

    public DeploymentSyncJob(IServiceScopeFactory scopeFactory, ILogger<DeploymentSyncJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrmsContext>();
        var changed = await DeploymentService.SyncAllCompaniesAsync(db, DeploymentClock.Today(), context.CancellationToken);
        if (changed > 0)
            await db.SaveChangesAsync(context.CancellationToken);
        _logger.LogInformation("Deployment sync: {Count} employee(s) moved to the client and site of their deployment starting today", changed);

        // The coming week's manning to the Client Portal, for live monitoring. A failure leaves yesterday's plan there, so it is only logged.
        try
        {
            var publisher = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
            var sent = await ClientPortalLiveService.PublishManningForAllCompaniesAsync(db, publisher, DeploymentClock.Today(), context.CancellationToken);
            _logger.LogInformation("Deployment sync: manning sent to the Client Portal for {Count} client(s)", sent);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Deployment sync: could not send manning to the Client Portal");
        }
    }
}
