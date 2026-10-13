using Serilog;
using System.Security.Claims;

namespace Hrms.Api.Extensions;

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

    public static string? GetUserClaim(this HttpContext context, string claim)
    {
        var user = context.User;
        return GetUserClaim(user, claim);
    }

    // HasPermission / HasAnyPermission / IsOwnerOrAdmin live in Onepunch.Common.Lib.Security
    // (PermissionClaims), shared with every API behind AuthApi's token.

    // The approval-workflow engine's escape hatch: Owner/Admin may act on any pending approval
    // step regardless of assignment (Person/Department/Position/Applicant's Manager/Applicant's
    // Department), since they already hold every {Row}:Approve permission broadly today. See
    // ApprovalEngineService.RecordActionAsync's callerHasOverrideAccess parameter.
    // Clearance to see confidential employees. Unlike HasPermission, the Admin role alone is NOT
    // enough -- the Owner decides which roles are cleared -- so only the Owner role or an explicit
    // grant of the permission counts.
    public static bool HasConfidentialClearance(this ClaimsPrincipal user) =>
        user.FindAll(ClaimTypes.Role).Any(c => c.Value == "Owner") ||
        user.FindAll("permission").Any(c => c.Value == Hrms.Core.Services.Security.ConfidentialAccessScope.ClearancePermission);

    // Partial clearances (explicit grants only, no role shortcut): Operations for day-to-day records,
    // Entries for cash bond / allowances / loans. Neither ever covers pay figures.
    public static Hrms.Core.Services.Security.ConfidentialGrants GetConfidentialGrants(this ClaimsPrincipal user)
    {
        var granted = user.FindAll("permission").Select(c => c.Value).ToHashSet();
        return new(
            granted.Contains(Hrms.Core.Services.Security.ConfidentialAccessScope.OperationsPermission),
            granted.Contains(Hrms.Core.Services.Security.ConfidentialAccessScope.EntriesPermission));
    }
}