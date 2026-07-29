using MassTransit;
using Onepunch.Common.Lib.Cache;

namespace Hrms.Core.Messaging.Filter;

public class TenantConsumeFilter<T> : IFilter<ConsumeContext<T>>
    where T : class
{
    private readonly ICacheService _cacheService;
    private readonly ITenantProvider _tenantProvider;
    private readonly TenantConnectionStringInfo _connectionInfo;
    private readonly IConnectionClient _connectionClient;
    public TenantConsumeFilter(
        ICacheService cacheService,
        ITenantProvider tenantProvider,
        TenantConnectionStringInfo connectionInfo,
        IConnectionClient connectionClient)
    {
        _cacheService = cacheService;
        _tenantProvider = tenantProvider;
        _connectionInfo = connectionInfo;
        _connectionClient = connectionClient;
    }

    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        if (!context.Headers.TryGetHeader("X-Tenant-ID", out var value) || !Guid.TryParse(value?.ToString(), out var tid))
        {
            throw new InvalidOperationException("Tenant ID header is missing or invalid.");
        }

        // 1. Set the ID for the current scoped services
        _tenantProvider.SetTenantId(tid);
        _connectionInfo.TenantId = tid;

        // 2. Short-circuit if this is a creation event (no DB exists yet). TenantCreatedPayload
        // specifically is Flow C's "tenant exists, HRIS hasn't provisioned its DB yet" signal —
        // TenantCreatedWorker/ITenantProvisioner is responsible for setting the connection
        // string itself once the DB is created, so resolving one here would always fail.
        if (context.Message is TenantCreationCompleted)
        {
            await next.Send(context);
            return;
        }

        // 3. Resolve Connection String
        var key = $"connection:{tid}";
        var cachedConnectionString = await _cacheService.GetAsync<string>(key);
        if (!string.IsNullOrWhiteSpace(cachedConnectionString))
        {
            _connectionInfo.ConnectionString = cachedConnectionString;
        }
        else
        {
            var response = await _connectionClient.FindConnectionAsync(tid, "hrms");
            if (response?.Data != null && response.Data.Success)
            {
                _connectionInfo.ConnectionString = response.Data.ConnectionString;
                await _cacheService.SetAsync(key, _connectionInfo.ConnectionString, TimeSpan.FromDays(7));
            }
            else
            {
                throw new Exception($"Operational DB for Tenant {tid} not found.");
            }
        }
        await next.Send(context);
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("hrms-tenant-filter");
}