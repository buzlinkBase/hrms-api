using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace Hrms.Core.Messaging;

public class TenantInitConfigWorker : IConsumer<TenantSetInitData>
{
    private readonly TenantConnectionStringInfo _tenantConnectionInfo;
    private readonly ITenantProvider _tenantProvider;
    private readonly AccountInitService _accountInitService;
    private readonly IServiceScopeFactory _factory;
    public TenantInitConfigWorker(
        TenantConnectionStringInfo tenantConnectionInfo,
        ITenantProvider tenantProvider,
        AccountInitService accountInitService,
        IServiceScopeFactory factory)
    {
        _tenantConnectionInfo = tenantConnectionInfo;
        _tenantProvider = tenantProvider;
        _accountInitService = accountInitService;
        _factory = factory;
    }

    public async Task Consume(ConsumeContext<TenantSetInitData> context)
    {

        var message = context.Message;
        using var scope = _factory.CreateScope();
        _tenantConnectionInfo.TenantId = message.TenantId;
        _tenantConnectionInfo.ConnectionString = message.ConnectionString;
        _tenantProvider.SetTenantId(message.TenantId);
        await _accountInitService.Create(context.CancellationToken);
        await _accountInitService.CommitChangesAsync(context.CancellationToken);
    }
}
