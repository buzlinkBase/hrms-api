namespace Hrms.Api.Providers;

public class WebTenantContextAccessor : ITenantProvider
{
    private Guid _tenantId;
    private readonly IHttpContextAccessor _httpContextAccessor;
    public WebTenantContextAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }
    public Guid TenantId
    {
        get
        {
            if (_tenantId != Guid.Empty) return _tenantId;
            var header = _httpContextAccessor.HttpContext?.Request?.Headers["X-Tenant-ID"].FirstOrDefault();
            return Guid.TryParse(header, out var id) ? id : Guid.Empty;
        }
        set => _tenantId = value;
    }
    public void SetTenantId(Guid tenantId) => _tenantId = tenantId;
}

public interface IConnectionStringProvider
{
    string? GetConnectionString(Guid tenantId);
}
public class ConnectionStringProvider : IConnectionStringProvider
{
    private readonly IConfiguration _configuration;
    public ConnectionStringProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    public string? GetConnectionString(Guid tenantId)
    {
        //we can get from redis or tenant service or local
        return _configuration.GetConnectionString("DefaultConnection");
    }
}
