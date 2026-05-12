using Hrms.Api.Extensions;

namespace Hrms.Api.Providers;

public class TenantProviderAccessor : ITenantProvider
{
    private Guid _tenantId;
    private readonly IHttpContextAccessor _httpContextAccessor;
    public TenantProviderAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid TenantId
    {
        get
        {
            if (_tenantId != Guid.Empty) return _tenantId;
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return Guid.Empty;
            // 1. Try to get from Header
            var header = context.Request.Headers["X-Tenant-ID"].FirstOrDefault();
            if (Guid.TryParse(header, out var headerId))
            {
                _tenantId = headerId;
                return _tenantId;
            }
            // 2. Fallback: Try to get from JWT Claims
            var ClaimTenantId = context.User.GetUserClaim("TenantId");
            return Guid.TryParse(ClaimTenantId, out var tenantId) && tenantId != Guid.Empty
                ? tenantId
                : Guid.Empty;
        }
    }
    public void SetTenantId(Guid tenantId) => _tenantId = tenantId;
}