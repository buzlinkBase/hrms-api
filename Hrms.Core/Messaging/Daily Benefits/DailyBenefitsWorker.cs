using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hrms.Core.Messaging.DailyBenefits;

/// <summary>
/// Runs one company's daily benefits steps in order. They used to be separate messages handled
/// by separate queues at the same moment, so on a fiscal year's first day the carry-over, the
/// period grant and the accrual could all update the same new-year leave record at once and
/// overwrite each other. Here they run one after another.
///
/// Each step gets its own scope (its own unit of work), set to this message's company: one
/// step's failed or half-tracked changes can't leak into the next. If a step throws, the whole
/// message is retried -- every step is idempotent, so steps that already ran just find nothing
/// left to do.
/// </summary>
public class DailyBenefitsWorker : IConsumer<RunDailyBenefits>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITenantProvider _tenantProvider;
    private readonly TenantConnectionStringInfo _connectionInfo;
    private readonly IEnumerable<IDailyBenefitStep> _steps;
    private readonly ILogger<DailyBenefitsWorker> _logger;

    public DailyBenefitsWorker(
        IServiceScopeFactory scopeFactory,
        ITenantProvider tenantProvider,
        TenantConnectionStringInfo connectionInfo,
        IEnumerable<IDailyBenefitStep> steps,
        ILogger<DailyBenefitsWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _tenantProvider = tenantProvider;
        _connectionInfo = connectionInfo;
        _steps = steps;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<RunDailyBenefits> context)
    {
        var run = context.Message;
        var token = context.CancellationToken;

        foreach (var step in _steps.Where(s => s.AppliesOn(run)).OrderBy(s => s.Order))
        {
            using var scope = _scopeFactory.CreateScope();
            var services = scope.ServiceProvider;
            services.GetRequiredService<ITenantProvider>().SetTenantId(_tenantProvider.TenantId);
            var connection = services.GetRequiredService<TenantConnectionStringInfo>();
            connection.TenantId = _connectionInfo.TenantId;
            connection.ConnectionString = _connectionInfo.ConnectionString;

            await step.RunAsync(services, run, token);
            _logger.LogInformation("Daily benefits [{Tenant}] {Date}: {Step} done", _tenantProvider.TenantId, run.Date, step.Name);
        }
    }
}

public class DailyBenefitsWorkerDefinition : ConsumerDefinition<DailyBenefitsWorker>
{
    public DailyBenefitsWorkerDefinition()
    {
        EndpointName = "hrms-daily-benefits-que";
        ConcurrentMessageLimit = 1;
    }
}
