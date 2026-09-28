namespace Hrms.Core.Services.Approvals;

// NotificationHub group addressing one person's approval pushes by company + login email.
// Employee.UserId is only filled when an invitation named that employee; everyone else is matched
// to their account by email (EmployeeService.ResolveEmployeeIdAsync), so Clients.User(UserId)
// never reached them. Every connection joins its own group from its token's tenantId + email
// claims (NotificationHub.OnConnectedAsync), and ApprovalPushNotificationWorker targets it.
// Scoped per company because one login can belong to several companies.
public static class ApprovalRecipientGroups
{
    public static string For(Guid tenantId, string email) =>
        $"approval-recipient:{tenantId}:{email.Trim().ToLowerInvariant()}";
}
