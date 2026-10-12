using Hrms.Domain.Entities.Attachments;

namespace Hrms.Core.Services.Attachments;

/// <summary>Who is asking: their own employee record (portal) and which permissions they hold.</summary>
public sealed record AttachmentCaller(Guid? UserId, Guid? EmployeeId, Func<string[], bool> HasAnyPermission);

/// <summary>What a service needs to claim a form's staged files for the record it just saved.</summary>
public sealed record AttachmentIntake(AttachmentService Service, AttachmentCaller Caller);

/// <summary>What a handler knows about the record a file is attached to.</summary>
/// <param name="EmployeeId">Whose record it is, for the "own record" rule; null if it belongs to nobody in particular.</param>
/// <param name="Locked">True once the record can no longer change (e.g. a decided application).</param>
public sealed record AttachmentOwnerInfo(Guid? EmployeeId, bool Locked);

/// <summary>
/// One feature that can carry attachments. The attachment service knows nothing about leave,
/// assets or documents: it asks the handler registered for the owner type whether the record
/// exists, who owns it, whether it is locked, and what the caller may do. A new feature is one new
/// handler -- the service, controller and screens stay as they are.
/// </summary>
public interface IAttachmentOwnerHandler
{
    AttachmentOwnerType OwnerType { get; }

    /// <summary>The storage folder under the company, e.g. "attachments-leave" (one folder name, no slashes).</summary>
    string Area { get; }

    /// <summary>Permission codes (any-of) that allow seeing the files of any record of this type.</summary>
    string[] ViewPermissions { get; }

    /// <summary>Permission codes (any-of) that allow adding and removing files on any record of this type.</summary>
    string[] ManagePermissions { get; }

    /// <summary>True when an employee may attach files to their own records (the Employee Portal).</summary>
    bool AllowsSelfService { get; }

    /// <summary>The record, or null when it doesn't exist (or the caller can't see it).</summary>
    Task<AttachmentOwnerInfo?> FindAsync(Guid ownerId, CancellationToken token);
}
