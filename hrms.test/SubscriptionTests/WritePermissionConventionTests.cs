using System.Reflection;
using Hrms.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Onepunch.Common.Web.Authorization;

namespace hrms.test.SubscriptionTests;

/// <summary>
/// The UI hides Save buttons by permission, but that is cosmetic: every POST/PUT/PATCH/DELETE must
/// be refused server-side too. A write action therefore needs [RequirePermission] (on the action or
/// its controller) unless it is pinned below with the reason its own checks are enough. Adding to
/// the list is a deliberate, reviewed change -- never a way to make this test pass.
/// </summary>
public class WritePermissionConventionTests
{
    // Actions that authorize inside the method/service (ownership of the caller's own record,
    // approver standing, inline HasAnyPermission, IsOwnerOrAdmin) or are internal service-to-service.
    private static readonly string[] SelfChecked =
    [
        "ApprovalDelegationsController.CreateMine",
        "ApprovalDelegationsController.DeactivateMine",
        "ApprovalsController.Reassign",
        "AttachmentsController.Delete",
        "AttachmentsController.LinkStaged",
        "AttachmentsController.Upload",
        "InternalStorageManagerController.Delete",
        "InternalStorageManagerController.PreviewDelete",
        "MeController.AcknowledgeMyPayslip",
        "MeController.CreateMyChangeRestDayRequest",
        "MeController.CreateMyLeaveApplication",
        "MeController.CreateMyLoanApplication",
        "MeController.CreateMyOvertimeApplication",
        "MeController.CreateMyPassSlipApplication",
        "MeController.CreateMyProfileUpdateRequest",
        "MeController.CreateMyTravelOrderApplication",
        "MeController.Punch",
        "MeController.SetBirthdaySharing",
        "MeController.SetContactSharing",
        "MeController.UpdateMyLeaveApplication",
        "MeController.UpdateMyLoanApplication",
        "MeController.UpdateMyOvertimeApplication",
        "MeController.UpdateMyPassSlipApplication",
        "MeController.UpdateMyTravelOrderApplication",
        "MeController.WithdrawMyChangeRestDayRequest",
        "MeController.WithdrawMyLeaveApplication",
        "MeController.WithdrawMyLoanApplication",
        "MeController.WithdrawMyOvertimeApplication",
        "MeController.WithdrawMyPassSlipApplication",
        "MeController.WithdrawMyProfileUpdateRequest",
        "MeController.WithdrawMyTravelOrderApplication",
        "MyAnnouncementsController.Acknowledge",
        "MyApprovalsController.Approve",
        "MyApprovalsController.Decline",
        "MyNotificationsController.ClearAll",
        "MyNotificationsController.MarkAllRead",
        "MyNotificationsController.MarkRead",
        "NotificationPreferencesController.UpdateMine",
        "PayrollsController.ApproveBatch",
        "PayrollsController.ApproveDeletion",
        "PayrollsController.DeclineBatch",
        "PayrollsController.DeclineDeletion",
        "PayrollsController.DeleteBatch",
        "PayrollsController.RequestDeletion",
        "PayrollsController.UnreleaseBatch",
        "PayrollsController.UnreleasePayout",
        "PayrollsController.UnsaveEmployee",
        "WorkSchedulePlansController.PostBatch",
        "WorkSchedulePlansController.PostGrid",
        "WorkSchedulePlansController.Put",
        "WorkSchedulePlansController.QueryGrid",
    ];

    [Fact]
    public void EveryWriteAction_RequiresAPermission_OrIsPinned()
    {
        var unguarded = WriteActions()
            .Where(a => !a.Guarded)
            .Select(a => a.Name)
            .OrderBy(n => n)
            .ToList();

        unguarded.Should().BeEquivalentTo(SelfChecked,
            "write endpoints must carry [RequirePermission]; the UI gate alone does not protect the API");
    }

    private static IEnumerable<(string Name, bool Guarded)> WriteActions() =>
        typeof(PayrollsController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(IsWrite)
                .Select(m => ($"{t.Name}.{m.Name}",
                    t.GetCustomAttribute<RequirePermissionAttribute>() != null
                    || m.GetCustomAttribute<RequirePermissionAttribute>() != null)));

    private static bool IsWrite(MethodInfo m) =>
        m.GetCustomAttributes().Any(a => a is HttpPostAttribute or HttpPutAttribute or HttpPatchAttribute or HttpDeleteAttribute);
}
