using MassTransit;

namespace Hrms.Core.Messaging;

public class TenantInitConfigWorker : IConsumer<TenantSetInitData>
{
    private readonly TenantConnectionStringInfo _tenantConnectionInfo;
    private readonly ITenantProvider _tenantProvider;
    private readonly AccountInitService _accountInitService;
    public TenantInitConfigWorker(
        TenantConnectionStringInfo tenantConnectionInfo,
        ITenantProvider tenantProvider,
        AccountInitService accountInitService)
    {
        _tenantConnectionInfo = tenantConnectionInfo;
        _tenantProvider = tenantProvider;
        _accountInitService = accountInitService;
    }

    public async Task Consume(ConsumeContext<TenantSetInitData> context)
    {
        var message = context.Message;
        _tenantConnectionInfo.TenantId = message.TenantId;
        _tenantConnectionInfo.ConnectionString = message.ConnectionString;
        _tenantProvider.SetTenantId(message.TenantId);
        var options = new TenantInitOptions(message.OtNdCalculationMethod, message.UserId);
        await _accountInitService.Create(options, context.CancellationToken);
        await _accountInitService.CommitChangesAsync(context.CancellationToken);
    }
}
