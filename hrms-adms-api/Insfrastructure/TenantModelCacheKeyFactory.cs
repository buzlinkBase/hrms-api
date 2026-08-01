using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Hrms.adms.Insfrastructure;

public class TenantModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
    {
        var provider = context.GetService<ITenantProvider>();
        return (context.GetType(), provider?.TenantId ?? Guid.Empty, designTime);
    }
}
