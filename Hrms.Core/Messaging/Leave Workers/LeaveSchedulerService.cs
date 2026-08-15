using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hrms.Core.Messaging.LeaveWorkers;

public class LeaveSchedulerOptions
{
    // Hour of day (UTC) to fire the daily scheduler check. Default midnight.
    public int RunAtHour { get; set; } = 0;
}

// Wakes once per day at the configured hour. Discovers all active tenant IDs by querying the
// distinct TenantId values present in the Leaves table (bypasses the per-tenant query filter).
// Publishes the appropriate leave trigger message for each tenant with an explicit X-Tenant-ID
// header so that TenantConsumeFilter routes each consumer to the correct tenant database.
public class LeaveSchedulerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<LeaveSchedulerOptions> _options;
    private readonly ILogger<LeaveSchedulerService> _logger;

    public LeaveSchedulerService(
        IServiceScopeFactory scopeFactory,
        IOptions<LeaveSchedulerOptions> options,
        ILogger<LeaveSchedulerService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LeaveSchedulerService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            await WaitUntilNextRun(stoppingToken);
            if (stoppingToken.IsCancellationRequested) break;

            await RunAsync(stoppingToken);
        }
    }

    private async Task WaitUntilNextRun(CancellationToken token)
    {
        var now = DateTime.UtcNow;
        var targetHour = _options.Value.RunAtHour;
        var next = now.Date.AddHours(targetHour);

        if (next <= now)
            next = next.AddDays(1);

        _logger.LogDebug("LeaveScheduler next run in {Delay}", next - now);
        await Task.Delay(next - now, token);
    }

    private async Task RunAsync(CancellationToken token)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var tenantIds = await GetActiveTenantIdsAsync(token);

        if (tenantIds.Count == 0)
        {
            _logger.LogWarning("LeaveScheduler: no active tenants found in Leaves table — skipping");
            return;
        }

        foreach (var tenantId in tenantIds)
        {
            try
            {
                await PublishForTenant(tenantId, today, token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LeaveScheduler: error publishing for tenant {TenantId}", tenantId);
            }
        }
    }

    // Query the shared DB with no tenant filter to discover all tenant IDs that have
    // at least one Leave type configured. This is the source of truth for which tenants
    // are active in a shared-database multi-tenant setup.
    private async Task<List<Guid>> GetActiveTenantIdsAsync(CancellationToken token)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HrmsContext>();

        return await context.Leaves
            .IgnoreQueryFilters()
            .Where(x => x.DeletedAt == null)
            .Select(x => x.TenantId)
            .Distinct()
            .ToListAsync(token);
    }

    private async Task PublishForTenant(Guid tenantId, DateOnly today, CancellationToken token)
    {
        using var scope = _scopeFactory.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
        var tenantProvider = scope.ServiceProvider.GetRequiredService<ITenantProvider>();

        tenantProvider.SetTenantId(tenantId);

        // Period grant — Jan 1
        if (today.Month == 1 && today.Day == 1)
        {
            await publisher.Publish<RunLeavePeriodGrant>(new RunLeavePeriodGrant(today.Year), SetTenantHeader(tenantId), token);
            _logger.LogInformation("LeaveScheduler [{Tenant}]: published RunLeavePeriodGrant {Year}", tenantId, today.Year);
        }

        // Monthly accrual — 1st of every month
        if (today.Day == 1)
        {
            await publisher.Publish<RunLeaveAccrual>(new RunLeaveAccrual(today), SetTenantHeader(tenantId), token);
            _logger.LogInformation("LeaveScheduler [{Tenant}]: published RunLeaveAccrual {Date}", tenantId, today);
        }

        // Year-end carry-over — Dec 31
        if (today.Month == 12 && today.Day == 31)
        {
            await publisher.Publish<RunLeaveCarryOver>(new RunLeaveCarryOver(today.Year), SetTenantHeader(tenantId), token);
            _logger.LogInformation("LeaveScheduler [{Tenant}]: published RunLeaveCarryOver {Year}", tenantId, today.Year);
        }
    }

    // Bypass TenantPublishFilter by setting the header explicitly per tenant rather than
    // relying on ITenantProvider, which avoids a single-tenant assumption in the scheduler scope.
    private static Action<PublishContext> SetTenantHeader(Guid tenantId) =>
        ctx => ctx.Headers.Set("X-Tenant-ID", tenantId.ToString());
}
