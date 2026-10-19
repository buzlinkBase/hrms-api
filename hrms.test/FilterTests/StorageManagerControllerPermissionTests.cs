using System.Reflection;
using Hrms.Api;
using Hrms.Api.Controllers;
using Hrms.Api.Filters;
using Microsoft.AspNetCore.Authorization;

namespace hrms.test.FilterTests;

/// <summary>
/// Billing > Storage is gated on the "Storage" catalog row (tenantstore's HrmsPermissionCatalog):
/// View to look, Manage to delete. The internal twin has no user permission -- it is Api-key only.
/// </summary>
public class StorageManagerControllerPermissionTests
{
    [Theory]
    [InlineData(nameof(StorageManagerController.Breakdown), new[] { "Storage:View", "Storage:Manage" })]
    [InlineData(nameof(StorageManagerController.Files), new[] { "Storage:View", "Storage:Manage" })]
    [InlineData(nameof(StorageManagerController.PreviewDelete), new[] { "Storage:Manage" })]
    [InlineData(nameof(StorageManagerController.Delete), new[] { "Storage:Manage" })]
    public void Action_RequiresItsPermission(string actionName, string[] codes)
    {
        var method = typeof(StorageManagerController).GetMethod(actionName)!;

        method.GetCustomAttribute<RequirePermissionAttribute>()!.Codes.Should().Equal(codes);
    }

    [Fact]
    public void InternalController_IsApiKeyOnly()
    {
        var type = typeof(InternalStorageManagerController);

        type.GetCustomAttribute<AllowAnonymousAttribute>().Should().NotBeNull();
        type.GetCustomAttribute<ApiKeyAttribute>().Should().NotBeNull();
    }
}
