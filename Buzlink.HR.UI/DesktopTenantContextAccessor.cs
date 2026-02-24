using Buzlink.HR.UI.Providers;
using BuzlinkRepository;
using Hrms.Infrastructure;
namespace Buzlink.HR.UI
{
    public class DesktopTenantContextAccessor : ITenantContextAccessor
    {
        private readonly ITenantProvider _tenantProvider;
        public DesktopTenantContextAccessor(ITenantProvider tenantProvider)
        {
            _tenantProvider = tenantProvider;
        }
        public Guid GetTenantId() => _tenantProvider.TenantId;
    }
}

public class WinAppConfigurationProvider : IAppConfigurationProvider
{
    private readonly IHRConnectionResolver _hRConnectionResolver;
    public WinAppConfigurationProvider(IHRConnectionResolver hRConnectionResolver)
    {
        _hRConnectionResolver = hRConnectionResolver;
    }
    public string? GetConnectionString(string name)
    {
        return _hRConnectionResolver.GetConnectionString();
    }
}

public class WinConnectionMetadataProvider : IDbConnectionProvider
{
    private readonly IHRConnectionResolver _hRConnectionResolver;

    public WinConnectionMetadataProvider(IHRConnectionResolver hRConnectionResolver)
    {
        _hRConnectionResolver = hRConnectionResolver;
    }
    public string? GetConnectionString(Guid tenantId)
    {
        return _hRConnectionResolver.GetConnectionString();
    }
}
public class Winplatform : IPlatform
{
    public PlatformType GetPlatForm() => PlatformType.WIN;
}
