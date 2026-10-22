using Onepunch.Common.Lib.Exceptions;

namespace Hrms.Core.Services;

/// <summary>
/// Permission codes for changing attendance logs -- one Edit/Delete pair per log source, each on its
/// own tenantstore catalog row (Attendance Manual Entry, Upload Attendance, Sync Attendance).
/// Constants so controllers can name them in [RequirePermission].
/// </summary>
public static class AttendanceLogPermissions
{
    public const string View = "Attendance Manual Entry:View";
    public const string Edit = "Attendance Manual Entry:Edit";
    public const string Delete = "Attendance Manual Entry:Delete";
    public const string UploadView = "Upload Attendance:View";
    // Also gates uploading attendance files.
    public const string EditUploaded = "Upload Attendance:Edit";
    // Also gates deleting a whole upload.
    public const string DeleteUploaded = "Upload Attendance:Delete";
    public const string EditSynced = "Sync Attendance:Edit";
    public const string DeleteSynced = "Sync Attendance:Delete";
}

/// <summary>Who is changing an attendance log: what they may do, and the name kept on a saved original.</summary>
public sealed record AttendanceLogCaller(Func<string, bool> HasPermission, Guid? UserId, string? Name)
{
    /// <summary>Holds every permission -- system callers and tests.</summary>
    public static AttendanceLogCaller System { get; } = new(_ => true, null, "System");
}

/// <param name="KeepsOriginal">Editing or removing saves the original (AttendanceOriginalLog), which
/// can be restored and keeps a re-upload/re-sync from bringing the punch back.</param>
public sealed record AttendanceLogSourceRule(string EditCode, string DeleteCode, bool KeepsOriginal);

/// <summary>
/// Which log sources a user may edit or remove, and the permission each needs. A source with no rule
/// (biometric, mobile, geofence, other) can't be changed by anyone.
/// </summary>
public static class AttendanceLogSourcePolicy
{
    private static readonly IReadOnlyDictionary<LOGSOURCE, AttendanceLogSourceRule> Rules =
        new Dictionary<LOGSOURCE, AttendanceLogSourceRule>
        {
            [LOGSOURCE.MANUAL] = new(AttendanceLogPermissions.Edit, AttendanceLogPermissions.Delete, KeepsOriginal: false),
            [LOGSOURCE.UPLOADED] = new(AttendanceLogPermissions.EditUploaded, AttendanceLogPermissions.DeleteUploaded, KeepsOriginal: true),
            [LOGSOURCE.SYNC] = new(AttendanceLogPermissions.EditSynced, AttendanceLogPermissions.DeleteSynced, KeepsOriginal: true),
        };

    public static AttendanceLogSourceRule EnsureCanEdit(LOGSOURCE source, AttendanceLogCaller caller) =>
        Ensure(source, caller, rule => rule.EditCode, "edit");

    public static AttendanceLogSourceRule EnsureCanDelete(LOGSOURCE source, AttendanceLogCaller caller) =>
        Ensure(source, caller, rule => rule.DeleteCode, "remove");

    private static AttendanceLogSourceRule Ensure(
        LOGSOURCE source, AttendanceLogCaller caller, Func<AttendanceLogSourceRule, string> codeOf, string verb)
    {
        if (!Rules.TryGetValue(source, out var rule))
            throw new ValidationException($"{source} attendance logs can't be changed. Only manual, uploaded and synced logs can be edited or removed.");
        var code = codeOf(rule);
        if (!caller.HasPermission(code))
            throw new ForbiddenException($"You don't have permission to {verb} {source} attendance logs. Requires {code}.");
        return rule;
    }
}
