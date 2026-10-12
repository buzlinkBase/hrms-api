using System.Security.Claims;
using Hrms.Core.Services.Attachments;

namespace Hrms.Api.Extensions;

public static class AttachmentCallerExtensions
{
    /// <summary>
    /// The signed-in user as the attachment service sees them. <paramref name="employeeId"/> is their
    /// own employee record (null for an account with none), which is what lets an employee attach to
    /// their own requests in the Employee Portal.
    /// </summary>
    public static AttachmentCaller ToAttachmentCaller(this ClaimsPrincipal user, Guid? employeeId) =>
        new(user.GetUserId(), employeeId, codes => user.HasAnyPermission(codes));
}
