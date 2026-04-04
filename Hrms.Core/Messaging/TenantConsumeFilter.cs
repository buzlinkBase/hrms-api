using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace Hrms.Core.Messaging;

public class TenantConsumeFilter<T> : IFilter<ConsumeContext<T>> where T : class
{
    private readonly ICacheService _cacheService;
    public TenantConsumeFilter(ICacheService cacheService)
    {
        _cacheService = cacheService;
    }
    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        var scope = context.GetPayload<IServiceProvider>();
        var tenantProvider = scope.GetRequiredService<ITenantProvider>();
        var _connectionInfo = scope.GetRequiredService<TenantConnectionInfo>();
        var _connectionClient = scope.GetRequiredService<IConnectionClient>();

        if (context.Headers.TryGetHeader("X-Tenant-ID", out var value)
            && Guid.TryParse(value?.ToString(), out var tid))
        {
            // 1. Set the ID for the current scope
            tenantProvider.SetTenantId(tid);
            _connectionInfo.TenantId = tid;

            if (context.Message is TenantCreatedPayload)
            {
                await next.Send(context);
                return;
            }
            var key = $"connection:{tid}"; // Use colon for better Redis grouping
            var cache = await _cacheService.GetAsync<string>(key);
            if (!string.IsNullOrWhiteSpace(cache))
            {
                _connectionInfo.ConnectionString = cache;
            }
            else
            {
                var response = await _connectionClient.FindConnectionAsync(tid, "hrms");
                if (response != null && response.Data != null && !response.Data.Success)
                {
                    _connectionInfo.ConnectionString = response.Data.ConnectionString;
                    //TODO cache connection string in Redis with an appropriate expiration time
                    await _cacheService.SetAsync(key, _connectionInfo.ConnectionString, TimeSpan.FromHours(1));
                }
                else
                {
                    throw new Exception($"Operational DB for Tenant {tid} not found.");
                }
            }
        }
        else
        {
            // Optional: Block execution if tenant is required
            throw new InvalidOperationException("Tenant ID header is missing.");
        }

        await next.Send(context);
    }
    public void Probe(ProbeContext context) => context.CreateFilterScope("hrms-tenant-filter");
}