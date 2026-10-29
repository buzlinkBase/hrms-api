using System.Security.Claims;
using Hrms.Api.Providers;
using Microsoft.AspNetCore.Http;

namespace hrms.test.ServiceTests;

public class TenantProviderAccessorTests
{
    private static TenantProviderAccessor Build(string? tenantClaim, string? header, bool authenticated)
    {
        var context = new DefaultHttpContext();
        if (header != null) context.Request.Headers["X-Tenant-ID"] = header;
        var claims = tenantClaim == null ? [] : new[] { new Claim("tenantId", tenantClaim) };
        context.User = new ClaimsPrincipal(authenticated
            ? new ClaimsIdentity(claims, "Bearer")
            : new ClaimsIdentity(claims));
        return new TenantProviderAccessor(new HttpContextAccessor { HttpContext = context });
    }

    [Fact]
    public void ClaimWins_OverTheHeader()
    {
        var claim = Guid.NewGuid();
        Build(claim.ToString(), Guid.NewGuid().ToString(), authenticated: true).TenantId.Should().Be(claim);
    }

    [Fact]
    public void AuthenticatedUserWithoutTenantClaim_CannotPickATenantByHeader()
    {
        Build(null, Guid.NewGuid().ToString(), authenticated: true).TenantId.Should().Be(Guid.Empty);
    }

    [Fact]
    public void UnauthenticatedInternalCall_StillUsesTheHeader()
    {
        var header = Guid.NewGuid();
        Build(null, header.ToString(), authenticated: false).TenantId.Should().Be(header);
    }
}
