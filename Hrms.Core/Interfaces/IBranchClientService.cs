using Refit;
namespace Hrms.Core.Interfaces;

public interface IBranchClient
{
    [Get("/api/v1/branches/tenant")]
    Task<List<Domain.ValueObjects.BranchModel>> FindAllAsync([AliasAs("tenant-id")] Guid tenantId);
}
