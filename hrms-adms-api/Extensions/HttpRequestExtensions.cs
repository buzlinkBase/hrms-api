using System.Security.Claims;
using Serilog; // Assuming you are using Serilog based on Log.Logger

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
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
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
}