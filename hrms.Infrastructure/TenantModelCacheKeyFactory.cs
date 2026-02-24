using BuzlinkRepository;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
namespace Hrms.Infrastructure;

public class TenantModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
    {
        var provider = context.GetService<ITenantProvider>();
        return (context.GetType(), provider?.TenantId ?? Guid.Empty, designTime);
        //if (context is HrmsContext tenantContext)
        //{
        //    return (context.GetType(), tenantContext.TenantId, designTime);
        //}
        //return (context.GetType(), designTime);
    }
}