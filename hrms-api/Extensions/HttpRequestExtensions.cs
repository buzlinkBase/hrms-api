using Serilog;
using System.Security.Claims;

namespace Hrms.Api.Extensions;

public static class HttpRequestExtensions
{
    // TEMP-ALLOW-ALL (2026-09-17): flip to false to restore normal permission checks in
    // HasPermission below. Search "TEMP-ALLOW-ALL" for every place this flag gates a check.
    // static (not const) deliberately -- a const bool would let the compiler fold the branch and
    // flag the real check below as unreachable (CS0162).
    private static readonly bool TempAllowAll = true;

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

    public static string? GetUserClaim(this HttpContext context, string claim)
    {
        var user = context.User;
        return GetUserClaim(user, claim);
    }

    public static bool HasPermission(this ClaimsPrincipal user, string code)
    {
        if (TempAllowAll) return true;
        return user.IsOwnerOrAdmin() || user.FindAll("permission").Any(c => c.Value == code);
    }

    public static bool HasAnyPermission(this ClaimsPrincipal user, params string[] codes) =>
        codes.Any(user.HasPermission);

    // The approval-workflow engine's escape hatch: Owner/Admin may act on any pending approval
    // step regardless of assignment (Person/Department/Position/Applicant's Manager/Applicant's
    // Department), since they already hold every {Row}:Approve permission broadly today. See
    // ApprovalEngineService.RecordActionAsync's callerHasOverrideAccess parameter.
    public static bool IsOwnerOrAdmin(this ClaimsPrincipal user) =>
        user.FindAll(ClaimTypes.Role).Any(c => c.Value is "Owner" or "Admin");
}