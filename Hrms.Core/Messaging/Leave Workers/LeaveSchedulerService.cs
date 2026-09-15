using Hrms.Core.Messaging.BenefitWorkers;
using MassTransit;
using Microsoft.EntityFrameworkCore;
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
// Reads each tenant's FiscalYearStartMonth from GeneralSettings and publishes the appropriate
// leave trigger message with an explicit X-Tenant-ID header. Despite the name, this is now the
// general "monthly tenant-scoped tick" trigger for more than just Leave -- Uniform Allowance's
// accrual (a non-leave benefit) rides the same 1st-of-the-month publish below rather than a
// second scheduler, since the daily-timer/tenant-enumeration machinery is identical either way.
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

    // Reads the tenant's FiscalYearStartMonth from GeneralSettings.
    // Falls back to 1 (calendar year) if not configured.
    private async Task<int> GetFiscalYearStartMonthAsync(Guid tenantId, CancellationToken token)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HrmsContext>();

        var setting = await context.GeneralSettings
            .IgnoreQueryFilters()
            .Where(x => x.IdentityType == "PayrollSettings"
                     && x.Description == "FiscalYearStartMonth")
            .Select(x => x.Value)
            .FirstOrDefaultAsync(token);

        return int.TryParse(setting, out var month) && month >= 1 && month <= 12 ? month : 1;
    }

    private async Task PublishForTenant(Guid tenantId, DateOnly today, CancellationToken token)
    {
        var fiscalStartMonth = await GetFiscalYearStartMonthAsync(tenantId, token);

        using var scope = _scopeFactory.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
        var tenantProvider = scope.ServiceProvider.GetRequiredService<ITenantProvider>();

        tenantProvider.SetTenantId(tenantId);

        // ── Fiscal year period grant — first day of the fiscal year ────────────
        if (FiscalYearHelper.IsFiscalYearStart(today, fiscalStartMonth))
        {
            var fiscalYear = FiscalYearHelper.FiscalYearOf(today, fiscalStartMonth);
            await publisher.Publish<RunLeavePeriodGrant>(
                new RunLeavePeriodGrant(fiscalYear, fiscalStartMonth),
                SetTenantHeader(tenantId),
                token);
            _logger.LogInformation(
                "LeaveScheduler [{Tenant}]: published RunLeavePeriodGrant FY{Year} (startMonth={Month})",
                tenantId, fiscalYear, fiscalStartMonth);
        }

        // ── Monthly accrual — 1st of every month ──────────────────────────────
        if (today.Day == 1)
        {
            await publisher.Publish<RunLeaveAccrual>(
                new RunLeaveAccrual(today, fiscalStartMonth),
                SetTenantHeader(tenantId),
                token);
            _logger.LogInformation(
                "LeaveScheduler [{Tenant}]: published RunLeaveAccrual {Date}", tenantId, today);

            // Uniform Allowance's monthly accrual rides this same tick -- see
            // UniformAllowanceAccrualWorker (Hrms.Core.Messaging.BenefitWorkers). Not
            // fiscal-year-anchored, so no FiscalYearStartMonth is needed here.
            await publisher.Publish<RunUniformAllowanceAccrual>(
                new RunUniformAllowanceAccrual(today),
                SetTenantHeader(tenantId),
                token);
            _logger.LogInformation(
                "LeaveScheduler [{Tenant}]: published RunUniformAllowanceAccrual {Date}", tenantId, today);
        }

        // ── Fiscal year-end carry-over — last day of the fiscal year ──────────
        if (FiscalYearHelper.IsFiscalYearEnd(today, fiscalStartMonth))
        {
            var fiscalYear = FiscalYearHelper.FiscalYearOf(today, fiscalStartMonth);
            await publisher.Publish<RunLeaveCarryOver>(
                new RunLeaveCarryOver(fiscalYear, fiscalStartMonth),
                SetTenantHeader(tenantId),
                token);
            _logger.LogInformation(
                "LeaveScheduler [{Tenant}]: published RunLeaveCarryOver FY{Year} (startMonth={Month})",
                tenantId, fiscalYear, fiscalStartMonth);
        }
    }

    private static Action<PublishContext> SetTenantHeader(Guid tenantId) =>
        ctx => ctx.Headers.Set("X-Tenant-ID", tenantId.ToString());
}
