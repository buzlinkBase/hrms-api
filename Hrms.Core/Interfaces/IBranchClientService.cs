using Refit;
namespace Hrms.Core.Interfaces;
public interface IBranchClient
{
    [Get("/api/v1/branches/tenant/{tenant}")]
    Task<List<BranchModel>> FindAllAsync([AliasAs("tenant-id")] Guid tenantId); 
}
