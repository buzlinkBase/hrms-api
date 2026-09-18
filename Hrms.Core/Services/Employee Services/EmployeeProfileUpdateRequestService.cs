using Hrms.Core.Services.Approvals;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Hrms.Core.Services;

public record ApproveProfileUpdateResult(bool HasConflict, DateTime? EmployeeUpdatedAt);

public class EmployeeProfileUpdateRequestService : BaseService<EmployeeProfileUpdateRequest>
{
    private readonly ApprovalEngineService _approvalEngine;

    public EmployeeProfileUpdateRequestService(IUnitOfWorkService uow, ApprovalEngineService approvalEngine) : base(uow)
    {
        _approvalEngine = approvalEngine;
    }

    public async Task AddAsync(EmployeeProfileUpdateRequest model, CancellationToken token)
    {
        var employee = await Repository.FindOneAsync<Employee>(model.EmployeeId, token)
            ?? throw new NotFoundException("Employee not found");

        var hasOpenRequest = await GetQueryable(x =>
            x.EmployeeId == model.EmployeeId && x.ApprovalStatus == ApprovalStatus.ForApproval)
            .AnyAsync(token);
        if (hasOpenRequest)
            throw new InvalidOperationException("You already have a profile update request awaiting approval.");

        model.ApprovalStatus = ApprovalStatus.ForApproval;
        model.EmployeeSnapshotUpdatedAt = employee.UpdatedAt;
        await CreateAsync(model, token);
        await _approvalEngine.StartAsync(ApprovalApplicationType.ProfileUpdate, model.Id, model.EmployeeId, token);
        await CommitChangesAsync(token);
    }

    // forceApply: the approver explicitly confirmed applying this request despite Employee
    // having been edited elsewhere since it was submitted (see EmployeeSnapshotUpdatedAt).
    // Returns HasConflict=true (without touching the approval engine or committing anything --
    // a declined/aborted conflict must not consume an approval step) when a conflict exists and
    // forceApply wasn't set, so the caller can surface it and let the approver decide.
    public async Task<ApproveProfileUpdateResult> ApproveAsync(
        Guid id, Guid? approverEmployeeId, bool approverHasOverride, string? note, bool forceApply, CancellationToken token)
    {
        var request = await GetOneAsync(id, token)
            ?? throw new NotFoundException("Record not found");
        if (request.ApprovalStatus != ApprovalStatus.ForApproval)
            throw new InvalidOperationException("This request is not awaiting approval.");

        var employee = await Repository.FindOneAsync<Employee>(request.EmployeeId, token)
            ?? throw new NotFoundException("Employee not found");

        if (!forceApply && request.EmployeeSnapshotUpdatedAt != employee.UpdatedAt)
            return new ApproveProfileUpdateResult(true, employee.UpdatedAt);

        var approverId = approverEmployeeId ?? throw new InvalidOperationException(
            "Your account isn't linked to an Employee record, so this approval action can't be recorded. Contact an admin to link your account.");

        var result = await _approvalEngine.RecordActionAsync(
            ApprovalApplicationType.ProfileUpdate, request.Id, request.EmployeeId,
            approverId, approverHasOverride, ApprovalActionType.Approved, note, token);

        if (result.InstanceStatus != ApprovalInstanceStatus.Approved)
        {
            // More steps remain -- stays ForApproval, Employee untouched until the final step.
            await CommitChangesAsync(token);
            return new ApproveProfileUpdateResult(false, null);
        }

        // Fully approved -- apply the staged edits onto the real Employee record.
        employee.Contact = request.NewContact ?? employee.Contact;
        employee.Address1 = request.NewAddress1 ?? employee.Address1;
        employee.Address2 = request.NewAddress2 ?? employee.Address2;
        employee.CivilStatus = request.NewCivilStatus ?? employee.CivilStatus;
        employee.DOB = request.NewDOB ?? employee.DOB;
        employee.BloodType = request.NewBloodType ?? employee.BloodType;
        Repository.Update(employee);

        request.ApprovalStatus = ApprovalStatus.Approved;
        await ModifyAsync(request, token);
        await CommitChangesAsync(token);
        return new ApproveProfileUpdateResult(false, null);
    }

    public async Task DeclineAsync(
        Guid id, Guid? approverEmployeeId, bool approverHasOverride, string? note, CancellationToken token)
    {
        var request = await GetOneAsync(id, token)
            ?? throw new NotFoundException("Record not found");
        if (request.ApprovalStatus != ApprovalStatus.ForApproval)
            throw new InvalidOperationException("This request is not awaiting approval.");

        var approverId = approverEmployeeId ?? throw new InvalidOperationException(
            "Your account isn't linked to an Employee record, so this approval action can't be recorded. Contact an admin to link your account.");

        var result = await _approvalEngine.RecordActionAsync(
            ApprovalApplicationType.ProfileUpdate, request.Id, request.EmployeeId,
            approverId, approverHasOverride, ApprovalActionType.Declined, note, token);

        request.ApprovalStatus = ApprovalEngineService.MapInstanceStatus(result.InstanceStatus);
        await ModifyAsync(request, token);
        await CommitChangesAsync(token);
    }

    // Self-service cancel of the employee's own still-pending request.
    public async Task WithdrawAsync(Guid id, Guid employeeId, CancellationToken token)
    {
        var request = await GetOneAsync(id, token);
        if (request == null || request.EmployeeId != employeeId)
            throw new NotFoundException("Request not found");
        if (request.ApprovalStatus != ApprovalStatus.ForApproval)
            throw new InvalidOperationException("Only pending requests can be withdrawn.");

        request.ApprovalStatus = ApprovalStatus.Withdrawn;
        await ModifyAsync(request, token);
        await CommitChangesAsync(token);
    }

    public async Task<List<EmployeeProfileUpdateRequestModel>> FindAllAsync(Guid? employeeId, CancellationToken token)
    {
        var query = Context.EmployeeProfileUpdateRequests
            .Include(x => x.Employee)
            .AsQueryable();

        if (employeeId.HasValue) query = query.Where(x => x.EmployeeId == employeeId.Value);

        var requests = await query.OrderByDescending(x => x.CreatedAt).ToListAsync(token);
        return requests.Select(ToModel).ToList();
    }

    public async Task<EmployeeProfileUpdateRequestModel?> FindOneWithCurrentValuesAsync(Guid id, CancellationToken token)
    {
        var request = await Context.EmployeeProfileUpdateRequests
            .Include(x => x.Employee)
            .FirstOrDefaultAsync(x => x.Id == id, token);
        return request == null ? null : ToModel(request);
    }

    private static EmployeeProfileUpdateRequestModel ToModel(EmployeeProfileUpdateRequest request)
    {
        var employee = request.Employee;
        return new EmployeeProfileUpdateRequestModel
        {
            Id = request.Id,
            EmployeeId = request.EmployeeId,
            EmployeeName = employee != null ? $"{employee.LastName}, {employee.FirstName} {employee.MiddleName}" : null,
            ApprovalStatus = request.ApprovalStatus,
            CreatedAt = request.CreatedAt,
            Remarks = request.Remarks,
            NewContact = request.NewContact,
            NewAddress1 = request.NewAddress1,
            NewAddress2 = request.NewAddress2,
            NewCivilStatus = request.NewCivilStatus,
            NewDOB = request.NewDOB,
            NewBloodType = request.NewBloodType,
            CurrentContact = employee?.Contact,
            CurrentAddress1 = employee?.Address1,
            CurrentAddress2 = employee?.Address2,
            CurrentCivilStatus = employee?.CivilStatus,
            CurrentDOB = employee?.DOB,
            CurrentBloodType = employee?.BloodType,
            HasConflict = request.ApprovalStatus == ApprovalStatus.ForApproval
                && employee != null
                && request.EmployeeSnapshotUpdatedAt != employee.UpdatedAt,
        };
    }
}
