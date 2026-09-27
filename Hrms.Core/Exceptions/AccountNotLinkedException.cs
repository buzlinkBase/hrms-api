namespace Hrms.Core;

/// <summary>
/// The signed-in account has no Employee record (EmployeeService.ResolveEmployeeIdAsync matched
/// neither its UserId nor its email), so an action that must be attributed to an employee
/// (approving, requesting, filing on someone's behalf) can't proceed. Services don't know who's
/// calling, so they throw this with only the <see cref="Reason"/>; the API's GlobalExceptionHandler
/// renders the user-facing message with the caller's own login email via <see cref="MessageFor"/>
/// -- the one thing an admin needs to link the account (ResolveEmployeeIdAsync falls back to
/// matching Employee.Email, so putting that email on the Employee record is all it takes).
/// </summary>
// Still an InvalidOperationException, which is what these sites threw before.
public class AccountNotLinkedException : InvalidOperationException
{
    /// <summary>What can't happen, e.g. "this approval action can't be recorded".</summary>
    public string Reason { get; }

    public AccountNotLinkedException(string reason) : base(MessageFor(null, reason))
    {
        Reason = reason;
    }

    public string MessageFor(string? email) => MessageFor(email, Reason);

    private static string MessageFor(string? email, string reason) =>
        string.IsNullOrWhiteSpace(email)
            ? $"Your account isn't linked to an Employee record, so {reason}. Ask an admin to enter your login email in the Email field of your Employee record -- that's all that's needed to link it."
            : $"Your account ({email}) isn't linked to an Employee record, so {reason}. Ask an admin to enter {email} in the Email field of your Employee record -- that's all that's needed to link it.";
}
