using Hrms.Core.Services.Approvals;
using Hrms.Domain.Entities;
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

    public async Task AddAsync(PassSlipApplication model, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(model.Remarks))
            throw new InvalidOperationException("Remarks explaining this pass slip is required.");

        model.ApprovalStatus = ApprovalStatus.ForApproval;
        await CreateAsync(model, token);
        await _approvalEngine.StartAsync(ApprovalApplicationType.PassSlip, model.Id, model.EmployeeId, token);
        await CommitChangesAsync(token);
    }

    public async Task UpdateAsync(UpdatePassSlipApplication payload, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(payload.Remarks))
            throw new InvalidOperationException("Remarks explaining this pass slip is required.");

        var existing = await Context.PassSlipApplications.FindAsync(new object[] { payload.Id }, token);
        if (existing == null) throw new NotFoundException("Record not found");
        payload.Adapt(existing);
        await ModifyAsync(existing, token);
        await CommitChangesAsync(token);
    }

    public async Task ApproveAsync(
        Guid id, Guid? approverEmployeeId, bool approverHasOverride, string? note, CancellationToken token)
    {
        var passSlip = await Context.PassSlipApplications.FindAsync(new object[] { id }, token);
        if (passSlip == null) throw new NotFoundException("Record not found");
        if (passSlip.ApprovalStatus != ApprovalStatus.ForApproval)
            throw new InvalidOperationException("This pass slip is not awaiting approval.");

        var approverId = approverEmployeeId ?? throw new InvalidOperationException(
            "Your account isn't linked to an Employee record, so this approval action can't be recorded. Contact an admin to link your account.");

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

        var records = new List<Attendance>
        {
            new Attendance
            {
                EmployeeId = passSlip.EmployeeId,
                WorkDateTime = passSlip.DepartureTime,
                Workstate = 1,
                LogSource = LOGSOURCE.MANUAL,
                LogRemarks = $"Pass Slip - {passSlip.Purpose}",
                BatchCode = batchCode,
                UserName = string.Empty,
            }
        };

        if (passSlip.ReturnTime.HasValue)
        {
            records.Add(new Attendance
            {
                EmployeeId = passSlip.EmployeeId,
                WorkDateTime = passSlip.ReturnTime.Value,
                Workstate = 0,
                LogSource = LOGSOURCE.MANUAL,
                LogRemarks = $"Pass Slip Return - {passSlip.Purpose}",
                BatchCode = batchCode,
                UserName = string.Empty,
            });
        }

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

        var approverId = approverEmployeeId ?? throw new InvalidOperationException(
            "Your account isn't linked to an Employee record, so this approval action can't be recorded. Contact an admin to link your account.");

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
        await CommitChangesAsync(token);
    }

    public async Task<List<PassSlipApplication>> FindAllAsync(DateOnly? from, DateOnly? to, Guid? employeeId, CancellationToken token)
    {
        var query = Context.PassSlipApplications
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
        await RemoveAsync(id, token);
        await CommitChangesAsync(token);
    }
}
