using Hrms.Domain.Entities;
using Hrms.Domain.Entities.Attachments;

namespace Hrms.Core.Services.Attachments.Handlers;

// Employee 201-file document records (Employee > Documents tab). HR-managed: employees don't
// attach to these themselves.
public sealed class EmployeeRecordAttachmentHandler(IUnitOfWorkService uow) : IAttachmentOwnerHandler
{
    public AttachmentOwnerType OwnerType => AttachmentOwnerType.EmployeeRecord;
    public string Area => "attachments-employee-documents";
    public string[] ViewPermissions => ["Workforce Setup:View", "Workforce Setup:Edit", "Workforce Setup:Create"];
    public string[] ManagePermissions => ["Workforce Setup:Edit", "Workforce Setup:Create"];
    public bool AllowsSelfService => false;

    public async Task<AttachmentOwnerInfo?> FindAsync(Guid ownerId, CancellationToken token)
    {
        var record = await uow.Context.EmployeeRecords.AsNoTracking()
            .Where(r => r.Id == ownerId).Select(r => new { r.EmployeeId }).FirstOrDefaultAsync(token);
        return record == null ? null : new AttachmentOwnerInfo(record.EmployeeId, false);
    }
}

// Assets issued to an employee (Employee > Assets tab): photos of the item and of its condition.
public sealed class AssignAssetAttachmentHandler(IUnitOfWorkService uow) : IAttachmentOwnerHandler
{
    public AttachmentOwnerType OwnerType => AttachmentOwnerType.AssignAsset;
    public string Area => "attachments-employee-assets";
    public string[] ViewPermissions => ["Workforce Setup:View", "Workforce Setup:Edit", "Workforce Setup:Create"];
    public string[] ManagePermissions => ["Workforce Setup:Edit", "Workforce Setup:Create"];
    public bool AllowsSelfService => false;

    public async Task<AttachmentOwnerInfo?> FindAsync(Guid ownerId, CancellationToken token)
    {
        var asset = await uow.Context.AssignAssets.AsNoTracking()
            .Where(a => a.Id == ownerId).Select(a => new { a.EmployeeId }).FirstOrDefaultAsync(token);
        return asset == null ? null : new AttachmentOwnerInfo(asset.EmployeeId, false);
    }
}

// Official business / travel orders: itinerary, invitation or other proof of the trip. The employee
// can attach to their own order while it is pending; HR with Official Business permissions can on anyone's.
public sealed class TravelOrderAttachmentHandler(IUnitOfWorkService uow) : IAttachmentOwnerHandler
{
    public AttachmentOwnerType OwnerType => AttachmentOwnerType.TravelOrder;
    public string Area => "attachments-official-business";
    public string[] ViewPermissions =>
        ["Official Business:View", "Official Business:Edit", "Official Business:Create", "Official Business:Approve"];
    public string[] ManagePermissions => ["Official Business:Edit", "Official Business:Create"];
    public bool AllowsSelfService => true;

    public async Task<AttachmentOwnerInfo?> FindAsync(Guid ownerId, CancellationToken token)
    {
        var order = await uow.Context.TravelOrderApplications.AsNoTracking()
            .Where(a => a.Id == ownerId).Select(a => new { a.EmployeeId, a.ApprovalStatus }).FirstOrDefaultAsync(token);
        return order == null
            ? null
            : new AttachmentOwnerInfo(order.EmployeeId, order.ApprovalStatus != ApprovalStatus.ForApproval);
    }
}

// Pass slips: supporting files for the trip out. Same rules as official business.
public sealed class PassSlipAttachmentHandler(IUnitOfWorkService uow) : IAttachmentOwnerHandler
{
    public AttachmentOwnerType OwnerType => AttachmentOwnerType.PassSlip;
    public string Area => "attachments-pass-slip";
    public string[] ViewPermissions => ["Pass Slip:View", "Pass Slip:Edit", "Pass Slip:Create", "Pass Slip:Approve"];
    public string[] ManagePermissions => ["Pass Slip:Edit", "Pass Slip:Create"];
    public bool AllowsSelfService => true;

    public async Task<AttachmentOwnerInfo?> FindAsync(Guid ownerId, CancellationToken token)
    {
        var slip = await uow.Context.PassSlipApplications.AsNoTracking()
            .Where(a => a.Id == ownerId).Select(a => new { a.EmployeeId, a.ApprovalStatus }).FirstOrDefaultAsync(token);
        return slip == null
            ? null
            : new AttachmentOwnerInfo(slip.EmployeeId, slip.ApprovalStatus != ApprovalStatus.ForApproval);
    }
}

// Leave applications: supporting documents such as a medical certificate. The employee can attach
// to their own application while it is pending; HR with Leave permissions can on anyone's.
public sealed class LeaveApplicationAttachmentHandler(IUnitOfWorkService uow) : IAttachmentOwnerHandler
{
    public AttachmentOwnerType OwnerType => AttachmentOwnerType.LeaveApplication;
    public string Area => "attachments-leave";
    public string[] ViewPermissions => ["Leave:View", "Leave:Edit", "Leave:Create", "Leave:Approve"];
    public string[] ManagePermissions => ["Leave:Edit", "Leave:Create"];
    public bool AllowsSelfService => true;

    public async Task<AttachmentOwnerInfo?> FindAsync(Guid ownerId, CancellationToken token)
    {
        var application = await uow.Context.leaveApplications.AsNoTracking()
            .Where(a => a.Id == ownerId).Select(a => new { a.EmployeeId, a.ApprovalStatus }).FirstOrDefaultAsync(token);
        return application == null
            ? null
            : new AttachmentOwnerInfo(application.EmployeeId, application.ApprovalStatus != ApprovalStatus.ForApproval);
    }
}
