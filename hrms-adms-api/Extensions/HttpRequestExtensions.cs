using System.Security.Claims;

namespace Hrms.adms.Extensions;

public static class HttpRequestExtensions
{
    public static string? GetHeader(this HttpRequest request, string key)
    {
        return request.Headers.TryGetValue(key, out var headerValue)
            ? headerValue.FirstOrDefault()
            : null;
    }

    // Specific helper for Bearer Tokens
    public static string? GetAuthorizationToken(this HttpRequest request)
    {
        var authHeader = request.GetHeader("Authorization");
        if (authHeader != null && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authHeader.Substring("Bearer ".Length).Trim();
        }
        return null;
    }

    public static Guid? GetUserId(this ClaimsPrincipal user)
    {
        if (user == null) throw new ArgumentNullException(nameof(user));
        // "sub" is the raw claim type AuthApi's JwtService.CreateTokenAsync mints -- AddJwtBearer's
        // MapInboundClaims = false (ServiceRegitrations.cs) keeps it from being rewritten to
        // ClaimTypes.NameIdentifier.
        var value = user.FindFirstValue("sub");
        return Guid.TryParse(value, out Guid guid) ? guid : null;
    }

    public static Guid GetRequiredUserId(this ClaimsPrincipal user)
    {
        if (user == null) throw new ArgumentNullException(nameof(user));
        return user.GetUserId()
               ?? throw new UnauthorizedAccessException("User ID claim is missing or invalid.");
    }

    public static string? GetUserClaim(this ClaimsPrincipal user, string claim)
    {
        if (user == null) throw new ArgumentNullException(nameof(user));
        if (string.IsNullOrWhiteSpace(claim))
        {
            Log.Logger.Error("TenantId is not found in the token user:{0} claim:{1}", user, claim);
            throw new ArgumentException("Claim type must be provided.", nameof(claim));
        }
        // FIX: Replaced case-sensitive FindFirstValue with a case-insensitive check
        return user.Claims
            .FirstOrDefault(c => string.Equals(c.Type, claim, StringComparison.OrdinalIgnoreCase))?.Value;
    }

    public static Guid ParseTenant(this HttpContext context)
    {
        var parseTenant = context.User.GetUserClaim("tenantId")?.ToString()
          ?? Guid.Empty.ToString();
        return Guid.Parse(parseTenant);
    }

    public static string? GetUserClaim(this HttpContext context, string claim)
    {
        var user = context.User;
        return GetUserClaim(user, claim);
    }

    // Mirrors hrms-api's Hrms.Api.Extensions.HttpRequestExtensions -- AuthApi's JwtService embeds
    // one "permission" claim per granted permission code, same shape on every service's tokens
    // since they all validate against the same issuer/audience/signing key.
    //
    // IsOwnerOrAdmin() short-circuits this -- RequirePermissionAttribute (and everything else
    // that calls this) otherwise 403s a caller whose token has a role claim but no permission
    // claims yet, e.g. the brief window right after workspace creation before the async
    // membership/permission rows exist (see WorkspaceService.Create in tenantstore). Owner/Admin
    // are guaranteed every permission in the catalog anyway (PermissionCatalogSeederService), so
    // this isn't a real bypass -- it's the same answer the claims would eventually give, sooner.
    public static bool HasPermission(this ClaimsPrincipal user, string code) =>
        user.IsOwnerOrAdmin() || user.FindAll("permission").Any(c => c.Value == code);

    public static bool HasAnyPermission(this ClaimsPrincipal user, params string[] codes) =>
        codes.Any(user.HasPermission);

    public static bool IsOwnerOrAdmin(this ClaimsPrincipal user) =>
        user.FindAll(ClaimTypes.Role).Any(c => c.Value is "Owner" or "Admin");
}