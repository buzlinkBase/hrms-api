using MassTransit;
using Microsoft.Extensions.Configuration;
using Onepunch.Common.Lib.Interfaces;

namespace Hrms.Core.Messaging;
public class TenantConsumeFilter<T> : IFilter<ConsumeContext<T>> where T : class
{
    private readonly IConnectionClient _connectionClient;
    private readonly ITenantProvider _tenantProvider;
    private readonly IConfiguration _configuration;
    private readonly TenantConnectionInfo _connectionInfo;
    public TenantConsumeFilter(IConnectionClient client,
        ITenantProvider tenantProvider,
        IConfiguration configuration,
        TenantConnectionInfo info)
    {
        _connectionClient = client;
        _tenantProvider = tenantProvider;
        _configuration = configuration;
        _connectionInfo = info;
    }

    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        if (context.Headers.TryGetHeader("X-Tenant-ID", out var value) 
            && Guid.TryParse(value.ToString(), out var tid))
        {
            _tenantProvider.SetTenantId(tid);
            _connectionInfo.TenantId = tid;
            var response = await _connectionClient.FindConnectionAsync(tid);
            _connectionInfo.ConnectionString = response.Data 
                ?? _configuration.GetConnectionString("DefaultConnection");
        }
        await next.Send(context);
    }
    public void Probe(ProbeContext context) => context.CreateFilterScope("hrms-tenant-filter");
}