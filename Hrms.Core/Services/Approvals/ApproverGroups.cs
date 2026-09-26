namespace Hrms.Core.Services.Approvals;

// NotificationHub groups for the implicit legacy fallback step (no workflow configured for a
// tenant/type). That step has no named approver -- anyone holding the type's {Row}:Approve
// permission can act on it (see ApproverEligibilityResolver's Step == null branch), and those
// permissions live in the JWT, not in the HRIS database. So instead of resolving recipients
// here, every hub connection joins one group per type it may approve (computed from its own
// claims on connect -- see NotificationHub.OnConnectedAsync) and the fallback notification is
// pushed to that group. One group per (tenant, type) rather than per permission code, so an
// Owner/Admin -- who joins every type's group -- still receives each push exactly once.
public static class ApproverGroups
{
    // Mirrors the [RequirePermission("...:Approve")] gate on each type's approve endpoint.
    // Types with no dedicated approve permission (payroll posting/deletion) are left out, so
    // their fallback push reaches Owner/Admin only.
    private static readonly IReadOnlyDictionary<ApprovalApplicationType, string> ApprovePermissions =
        new Dictionary<ApprovalApplicationType, string>
        {
            [ApprovalApplicationType.Leave] = "Leave:Approve",
            [ApprovalApplicationType.Overtime] = "Overtime:Approve",
            [ApprovalApplicationType.OfficialBusiness] = "Official Business:Approve",
            [ApprovalApplicationType.PassSlip] = "Pass Slip:Approve",
            [ApprovalApplicationType.Loan] = "Loan/Deduction:Approve",
            [ApprovalApplicationType.ProfileUpdate] = "Profile Update:Approve",
            [ApprovalApplicationType.Dtr] = "DTR Master:Approve",
            [ApprovalApplicationType.DtrDeletion] = "DTR Master:Approve",
        };

    public static string For(Guid tenantId, ApprovalApplicationType type) => $"approvers:{tenantId}:{type}";

    public static IEnumerable<string> GroupsFor(Guid tenantId, bool isOwnerOrAdmin, IReadOnlySet<string> permissions) =>
        Enum.GetValues<ApprovalApplicationType>()
            .Where(type => isOwnerOrAdmin
                || (ApprovePermissions.TryGetValue(type, out var permission) && permissions.Contains(permission)))
            .Select(type => For(tenantId, type));
}
