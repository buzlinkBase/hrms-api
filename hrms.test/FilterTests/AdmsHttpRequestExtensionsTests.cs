using System.Security.Claims;
using Hrms.adms.Extensions;

namespace hrms.test.FilterTests;

/// <summary>
/// hrms-adms-api's own copy of HasPermission/HasAnyPermission/IsOwnerOrAdmin -- mirrors
/// hrms-api's Hrms.Api.Extensions.HttpRequestExtensions (see RequirePermissionAttributeTests for
/// that copy's equivalent coverage), duplicated here since hrms-adms-api doesn't reference
/// hrms-api's web project.
/// </summary>
public class AdmsHttpRequestExtensionsTests
{
    private static ClaimsPrincipal WithRole(string role) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)]));

    private static ClaimsPrincipal WithPermissions(params string[] codes) =>
        new(new ClaimsIdentity(codes.Select(c => new Claim("permission", c))));

    [Theory]
    [InlineData("Owner")]
    [InlineData("Admin")]
    public void HasPermission_ReturnsTrue_ForOwnerOrAdmin_EvenWithNoPermissionClaimsAtAll(string role)
    {
        WithRole(role).HasPermission("Biometric Setup:View").Should().BeTrue();
    }

    [Fact]
    public void HasPermission_ReturnsFalse_ForNonOwnerAdminCaller_WithoutTheCode()
    {
        WithRole("Employee").HasPermission("Biometric Setup:View").Should().BeFalse();
    }

    [Fact]
    public void HasPermission_ReturnsTrue_WhenCallerHoldsTheCodeDirectly()
    {
        WithPermissions("Biometric Setup:View").HasPermission("Biometric Setup:View").Should().BeTrue();
    }

    [Fact]
    public void HasAnyPermission_ReturnsTrue_ForOwner_RegardlessOfRequestedCodes()
    {
        WithRole("Owner").HasAnyPermission("Biometric Setup:View", "Biometric Setup:Edit").Should().BeTrue();
    }
}
