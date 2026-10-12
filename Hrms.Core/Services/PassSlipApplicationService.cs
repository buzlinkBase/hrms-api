using Hrms.Core.Services.Applications;
using Hrms.Core.Services.Approvals;
using Hrms.Core.Services.Attachments;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.Attachments;
using Hrms.Domain.ValueObjects;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Hrms.Core.Services;

public class PassSlipApplicationService : BaseService<PassSlipApplication>
{
    private readonly AttendanceService _attendanceService;
    private readonly ApprovalEngineService _approvalEngine;

    public PassSlipApplicationService(IUnitOfWorkService uow, AttendanceService attendanceService, ApprovalEngineService approvalEngine) : base(uow)
    {
        _attendanceService = attendanceService;
        _approvalEngine = approvalEngine;
    }

    public async Task AddAsync(
        PassSlipApplication model, CancellationToken token,
        AttachmentIntake? attachments = null, List<Guid>? attachmentIds = null)
    {
        if (string.IsNullOrWhiteSpace(model.Remarks))
            throw new InvalidOperationException("Remarks explaining this pass slip is required.");

        model.ApprovalStatus = ApprovalStatus.ForApproval;
        await CreateAsync(model, token);
        // Files the form uploaded before the pass slip existed now belong to it.
        if (attachments != null)
            await attachments.Service.LinkAsync(AttachmentOwnerType.PassSlip, model.Id, attachmentIds, attachments.Caller, token);
        await _approvalEngine.StartAsync(ApprovalApplicationType.PassSlip, model.Id, model.EmployeeId, token);
        await CommitChangesAsync(token);
    }

    public async Task UpdateAsync(UpdatePassSlipApplication payload, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(payload.Remarks))
            throw new InvalidOperationException("Remarks explaining this pass slip is required.");

        var existing = await Context.PassSlipApplications.FindAsync(new object[] { payload.Id }, token);
        if (existing == null) throw new NotFoundException("Record not found");

        // Plain field edit -- never changes ApprovalStatus; only Approve/Decline/Revoke do.
        var previousStatus = existing.ApprovalStatus;
        payload.Adapt(existing);
        existing.ApprovalStatus = previousStatus;

        // Changed content goes back to the first approver if anyone already signed off.
        if (existing.ApprovalStatus == ApprovalStatus.ForApproval)
            await _approvalEngine.RestartIfProgressedAsync(ApprovalApplicationType.PassSlip, existing.Id, token);

        await ModifyAsync(existing, token);
        await CommitChangesAsync(token);
    }

    // Self-service edit: only the caller's own, still-pending pass slip. See
    // MeController.UpdateMyPassSlipApplication.
    public async Task UpdateOwnAsync(UpdatePassSlipApplication payload, Guid employeeId, CancellationToken token)
    {
        var existing = await Context.PassSlipApplications.AsNoTracking().FirstOrDefaultAsync(x => x.Id == payload.Id, token);
        if (existing == null || existing.EmployeeId != employeeId)
            throw new NotFoundException("Pass slip not found.");
        if (existing.ApprovalStatus != ApprovalStatus.ForApproval)
            throw new InvalidOperationException("Only applications still awaiting approval can be edited.");

        await UpdateAsync(payload, token);
    }

    public async Task ApproveAsync(
        Guid id, Guid? approverEmployeeId, bool approverHasOverride, string? note, CancellationToken token)
    {
        var passSlip = await Context.PassSlipApplications.FindAsync(new object[] { id }, token);
        if (passSlip == null) throw new NotFoundException("Record not found");
        if (passSlip.ApprovalStatus != ApprovalStatus.ForApproval)
            throw new InvalidOperationException("This pass slip is not awaiting approval.");

        var approverId = approverEmployeeId ?? throw new AccountNotLinkedException("this approval action can't be recorded");

        var result = await _approvalEngine.RecordActionAsync(
            ApprovalApplicationType.PassSlip, passSlip.Id, passSlip.EmployeeId,
            approverId, approverHasOverride, ApprovalActionType.Approved, note, token);

        if (result.InstanceStatus != ApprovalInstanceStatus.Approved)
        {
            // More steps remain -- stays ForApproval, no attendance batch yet. Only the final
            // step's approval actually creates the DTR attendance records below.
            await CommitChangesAsync(token);
            return;
        }

        var batchCode = $"PASS-{id:N}";
        passSlip.ApprovalStatus = ApprovalStatus.Approved;
        passSlip.BatchCode = batchCode;

        // The pass slip's punches are stamped with the employee's Bio ID and own branch/client/
        // project site/department, like any other log (they used to carry none of them). Read
        // past the confidentiality filter: only these fields, and nothing is returned to the caller.
        var employee = await Context.EmployeesForIntegrityChecks().AsNoTracking()
            .Where(x => x.Id == passSlip.EmployeeId)
            .Select(x => new { x.BioId, Location = new AttendanceLocation(x.BranchId, x.ClientId, x.AreaId, x.DepartmentId) })
            .FirstOrDefaultAsync(token);
        var location = AttendanceLocationResolver.Resolve(AttendanceLocation.None, employee?.Location, _ => null);

        Attendance PassSlipLog(DateTime time, int workstate, string remarks) => new()
        {
            EmployeeId = passSlip.EmployeeId,
            BioId = employee?.BioId,
            WorkDateTime = time,
            Workstate = workstate,
            LogSource = LOGSOURCE.MANUAL,
            LogRemarks = remarks,
            BatchCode = batchCode,
            UserName = string.Empty,
            BranchId = location.BranchId,
            ClientId = location.ClientId,
            OperationAreaId = location.OperationAreaId,
            DepartmentId = location.DepartmentId,
        };

        var records = new List<Attendance> { PassSlipLog(passSlip.DepartureTime, 1, $"Pass Slip - {passSlip.Purpose}") };
        if (passSlip.ReturnTime.HasValue)
            records.Add(PassSlipLog(passSlip.ReturnTime.Value, 0, $"Pass Slip Return - {passSlip.Purpose}"));

        await _attendanceService.AddRangeAsync(records, token);
        await ModifyAsync(passSlip, token);
        await CommitChangesAsync(token);
    }

    // New -- Pass Slip had no reject-a-still-pending-request action before this feature (only
    // Revoke, which un-approves an already-approved slip; see below). Mirrors Approve's shape.
    public async Task DeclineAsync(
        Guid id, Guid? approverEmployeeId, bool approverHasOverride, string? note, CancellationToken token)
    {
        var passSlip = await Context.PassSlipApplications.FindAsync(new object[] { id }, token);
        if (passSlip == null) throw new NotFoundException("Record not found");
        if (passSlip.ApprovalStatus != ApprovalStatus.ForApproval)
            throw new InvalidOperationException("This pass slip is not awaiting approval.");

        var approverId = approverEmployeeId ?? throw new AccountNotLinkedException("this approval action can't be recorded");

        var result = await _approvalEngine.RecordActionAsync(
            ApprovalApplicationType.PassSlip, passSlip.Id, passSlip.EmployeeId,
            approverId, approverHasOverride, ApprovalActionType.Declined, note, token);

        passSlip.ApprovalStatus = ApprovalEngineService.MapInstanceStatus(result.InstanceStatus);
        await ModifyAsync(passSlip, token);
        await CommitChangesAsync(token);
    }

    public async Task RevokeAsync(Guid id, CancellationToken token)
    {
        var passSlip = await Context.PassSlipApplications.FindAsync(new object[] { id }, token);
        if (passSlip == null) throw new NotFoundException("Record not found");

        if (!string.IsNullOrEmpty(passSlip.BatchCode))
            await _attendanceService.RemoveBatch(passSlip.BatchCode, token);

        passSlip.ApprovalStatus = ApprovalStatus.Cancelled;
        passSlip.BatchCode = string.Empty;
        await ModifyAsync(passSlip, token);
        // The applicant used to learn about a revoked pass slip only by noticing it in their list.
        await _approvalEngine.NotifyApplicantAsync(ApprovalApplicationType.PassSlip, passSlip.Id, passSlip.EmployeeId,
            ApprovalEngineService.CancelledStatusLabel, note: null, token);
        await CommitChangesAsync(token);
    }

    // Self-service cancel of the employee's own still-pending application. A ForApproval pass
    // slip never went through ApproveAsync, so it has no BatchCode/attendance records to unwind
    // -- unlike RevokeAsync, which reverses an already-approved one. See MeController's
    // pass-slip-applications/{id}/withdraw endpoint.
    public async Task WithdrawAsync(Guid id, Guid employeeId, CancellationToken token)
    {
        var passSlip = await Context.PassSlipApplications.FindAsync(new object[] { id }, token);
        if (passSlip == null || passSlip.EmployeeId != employeeId)
            throw new NotFoundException("Application not found");
        if (passSlip.ApprovalStatus != ApprovalStatus.ForApproval)
            throw new InvalidOperationException("Only pending applications can be withdrawn.");

        passSlip.ApprovalStatus = ApprovalStatus.Withdrawn;
        await ModifyAsync(passSlip, token);
        // Its approval request stops too -- see ApprovalEngineService.CancelAsync.
        await _approvalEngine.CancelAsync(ApprovalApplicationType.PassSlip, passSlip.Id, token);
        await CommitChangesAsync(token);
    }

    public async Task<List<PassSlipApplication>> FindAllAsync(DateOnly? from, DateOnly? to, Guid? employeeId, CancellationToken token)
    {
        var query = Context.PassSlipApplications
            .AsNoTracking()
            .Include(x => x.Employee)
            .AsQueryable();

        if (from.HasValue) query = query.Where(x => x.ApplicationDate >= from.Value);
        if (to.HasValue) query = query.Where(x => x.ApplicationDate <= to.Value);
        if (employeeId.HasValue) query = query.Where(x => x.EmployeeId == employeeId.Value);

        return await query.OrderByDescending(x => x.ApplicationDate).ToListAsync(token);
    }

    public async Task<PassSlipApplication?> FineOneAsync(Guid id, CancellationToken token)
    {
        return await Context.PassSlipApplications
            .Include(x => x.Employee)
            .FirstOrDefaultAsync(x => x.Id == id, token);
    }

    public async Task Delete(Guid id, CancellationToken token)
    {
        var existing = await Context.PassSlipApplications.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, token);
        if (existing != null)
            await SavedDtrDeletionPolicy.EnsureDeletableAsync(Context.DailyTimeRecords, existing.ApprovalStatus,
                existing.EmployeeId, existing.ApplicationDate, existing.ApplicationDate, "pass slip", token);

        await RemoveAsync(id, token);
        await CommitChangesAsync(token);
    }
}
