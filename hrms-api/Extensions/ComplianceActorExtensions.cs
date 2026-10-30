using Hrms.Core.Services.Compliance;

namespace Hrms.Api.Extensions;

public static class ComplianceActorExtensions
{
    // The signed-in caller, as a deviation acknowledgment records them.
    public static ComplianceActor ToComplianceActor(this HttpContext context) =>
        new(context.User.GetUserId(), context.User.GetUserClaim("email"), context.Connection.RemoteIpAddress?.ToString());
}
