using Hrms.Api.Controllers;
using hrms.test.TestSupport;
using MapsterMapper;
using NSubstitute;
using Onepunch.Common.Lib.Cache;

namespace hrms.test.ControllerTests;

/// <summary>
/// EmployeeAssignAssetsController's Redis cache stores mapped models (never lazy-loading entity
/// proxies, whose serialization walks and loads the navigation graph) under tenant-scoped keys --
/// a bare id key let one company's cached row be served to another, bypassing the tenant filter.
/// </summary>
public class EmployeeAssignAssetsControllerCacheTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    private static (EmployeeAssignAssetsController Controller, ICacheService Cache, SqliteHrmsContext Db) Build()
    {
        var db = new SqliteHrmsContext();
        var context = db.NewContext(Tenant);
        var cache = Substitute.For<ICacheService>();
        var tenant = Substitute.For<ITenantProvider>();
        tenant.TenantId.Returns(Tenant);
        var controller = new EmployeeAssignAssetsController(
            new EmployeeAssignAssetService(new UnitOfWorkService(context)), new Mapper(), cache, tenant);
        return (controller, cache, db);
    }

    [Fact]
    public async Task GetAll_CachesMappedModels_UnderATenantScopedKey()
    {
        var (controller, cache, db) = Build();
        using var _ = db;
        var employeeId = Guid.NewGuid();

        await controller.GetAll(employeeId, CancellationToken.None);

        await cache.Received(1).SetAsync(
            $"assign-assets:{Tenant}:employee:{employeeId}",
            Arg.Any<List<AssignAssetModel>>(),
            Arg.Any<TimeSpan>());
        await cache.DidNotReceiveWithAnyArgs().SetAsync<List<Hrms.Domain.Entities.EmployeeEntities.AssignAsset>>(default!, default!, default);
    }

    [Fact]
    public async Task GetAll_ServesTheCachedModels()
    {
        var (controller, cache, db) = Build();
        using var _ = db;
        var employeeId = Guid.NewGuid();
        var cached = new List<AssignAssetModel> { new() };
        cache.GetAsync<List<AssignAssetModel>>($"assign-assets:{Tenant}:employee:{employeeId}").Returns(cached);

        var result = await controller.GetAll(employeeId, CancellationToken.None);

        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>().Which.Value.Should().BeSameAs(cached);
        await cache.DidNotReceiveWithAnyArgs().SetAsync<List<AssignAssetModel>>(default!, default!, default);
    }
}
