using Hrms.Domain.Entities.Approvals;
using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Core.Services.Approvals;

public record ApprovalActionResult(ApprovalInstanceStatus InstanceStatus, int CurrentStepNumber);

// The runtime approval engine, shared across all 5 Applications types. Owns ApprovalInstance/
// ApprovalAction state only -- it never touches LeaveApplication.ApprovalStatus or its 4
// siblings directly (it has no generic way to, since they're 5 unrelated tables). Callers (each
// application's own service) call StartAsync right after creating their record, and
// RecordActionAsync from their approve/decline action, then apply the returned
// ApprovalActionResult.InstanceStatus onto their own entity's legacy ApprovalStatus field.
//
// Reads/writes go entirely through _uow.Repository's generic methods (Find<T>/AddAsync<T>/
// Update<T>) rather than raw Context access, matching how every other service in this project
// stays unit-testable against the standard IRepository/IUnitOfWorkService NSubstitute mocks --
// see ApprovalEligibilityTests and MeControllerTests' portal-application tests.
//
// Neither method commits -- callers own the transaction boundary so the instance/action write
// and the application's own ApprovalStatus write land in one CommitChangesAsync.
public class ApprovalEngineService
{
    private readonly IUnitOfWorkService _uow;
    private readonly ApproverEligibilityResolver _eligibility = new();

    public ApprovalEngineService(IUnitOfWorkService uow)
    {
        _uow = uow;
    }

    public async Task<ApprovalInstance> StartAsync(
        ApprovalApplicationType applicationType,
        Guid applicationId,
        Guid applicantEmployeeId,
        CancellationToken token = default)
    {
        var applicant = await _uow.Repository.Find<Employee>(e => e.Id == applicantEmployeeId)
            .AsNoTracking()
            .FirstOrDefaultAsync(token)
            ?? throw new NotFoundException("Applicant not found.");

        var workflow = await ResolveActiveWorkflowAsync(applicationType, applicant.DepartmentId, token);

        var instance = new ApprovalInstance
        {
            ApplicationType = applicationType,
            ApplicationId = applicationId,
            ApplicantEmployeeId = applicantEmployeeId,
            ApprovalWorkflowId = workflow?.Id,
            // Already-loaded (with Steps/NamedApprovers included by ResolveActiveWorkflowAsync)
            // so a caller that immediately reuses this returned instance -- see RecordActionAsync's
            // self-heal path -- doesn't need a second round-trip to see the same data a fresh
            // GetInstanceAsync query would have loaded.
            Workflow = workflow,
            CurrentStepNumber = 1,
            Status = ApprovalInstanceStatus.InProgress,
        };
        await _uow.Repository.AddAsync(instance, token);
        return instance;
    }

    // Applicant's own department override first, else the tenant-wide default, else null (the
    // implicit single-step legacy fallback -- see ApprovalInstance.ApprovalWorkflowId).
    private async Task<ApprovalWorkflow?> ResolveActiveWorkflowAsync(
        ApprovalApplicationType type, Guid? applicantDepartmentId, CancellationToken token)
    {
        if (applicantDepartmentId is { } departmentId)
        {
            var scoped = await _uow.Repository.Find<ApprovalWorkflow>(w =>
                    w.ApplicationType == type && w.IsActive && w.ScopeDepartmentId == departmentId)
                .Include(w => w.Steps).ThenInclude(s => s.NamedApprovers)
                .FirstOrDefaultAsync(token);
            if (scoped != null) return scoped;
        }

        return await _uow.Repository.Find<ApprovalWorkflow>(w =>
                w.ApplicationType == type && w.IsActive && w.ScopeDepartmentId == null)
            .Include(w => w.Steps).ThenInclude(s => s.NamedApprovers)
            .FirstOrDefaultAsync(token);
    }

    public async Task<ApprovalInstance?> GetInstanceAsync(
        ApprovalApplicationType type, Guid applicationId, CancellationToken token = default) =>
        await _uow.Repository.Find<ApprovalInstance>(i => i.ApplicationType == type && i.ApplicationId == applicationId)
            .Include(i => i.Workflow).ThenInclude(w => w!.Steps).ThenInclude(s => s.NamedApprovers)
            .Include(i => i.Actions)
            .FirstOrDefaultAsync(token);

    public async Task<bool> IsCallerEligibleAsync(
        ApprovalApplicationType type, Guid applicationId, Guid callerEmployeeId, CancellationToken token = default)
    {
        var instance = await GetInstanceAsync(type, applicationId, token);
        if (instance is null || instance.Status != ApprovalInstanceStatus.InProgress) return false;

        var caller = await _uow.Repository.Find<Employee>(e => e.Id == callerEmployeeId).AsNoTracking().FirstOrDefaultAsync(token);
        if (caller is null) return false;
        var applicant = await _uow.Repository.Find<Employee>(e => e.Id == instance.ApplicantEmployeeId).AsNoTracking().FirstOrDefaultAsync(token);
        if (applicant is null) return false;

        var currentStep = CurrentStep(instance);
        var namedApproverIds = currentStep?.NamedApprovers.Select(a => a.EmployeeId).ToArray() ?? [];
        return _eligibility.IsEligible(new ApprovalEligibilityContext(caller, applicant, currentStep, namedApproverIds));
    }

    // callerHasOverrideAccess: true for Owner/Admin, who may act on any pending step regardless
    // of assignment -- resolved by the caller (controller layer) from the same role/claims check
    // that already grants the coarse {Row}:Approve permission broadly, not by this service.
    //
    // applicantEmployeeId is only used to self-heal: an application filed before this engine
    // shipped (or otherwise missing its StartAsync call) has no ApprovalInstance row yet, so one
    // is lazily created here instead of failing the approve/decline action outright -- existing
    // in-flight requests keep working with no backfill migration required.
    public async Task<ApprovalActionResult> RecordActionAsync(
        ApprovalApplicationType type,
        Guid applicationId,
        Guid applicantEmployeeId,
        Guid callerEmployeeId,
        bool callerHasOverrideAccess,
        ApprovalActionType action,
        string? note,
        CancellationToken token = default)
    {
        var instance = await GetInstanceAsync(type, applicationId, token)
            ?? await StartAsync(type, applicationId, applicantEmployeeId, token);
        if (instance.Status != ApprovalInstanceStatus.InProgress)
            throw new InvalidOperationException("This application is no longer awaiting approval.");

        var caller = await _uow.Repository.Find<Employee>(e => e.Id == callerEmployeeId).AsNoTracking().FirstOrDefaultAsync(token)
            ?? throw new NotFoundException("Approver not found.");
        var applicant = await _uow.Repository.Find<Employee>(e => e.Id == instance.ApplicantEmployeeId).AsNoTracking().FirstOrDefaultAsync(token)
            ?? throw new NotFoundException("Applicant not found.");

        var currentStep = CurrentStep(instance);
        var namedApproverIds = currentStep?.NamedApprovers.Select(a => a.EmployeeId).ToArray() ?? [];
        var eligibilityContext = new ApprovalEligibilityContext(caller, applicant, currentStep, namedApproverIds);

        if (!callerHasOverrideAccess && !_eligibility.IsEligible(eligibilityContext))
            throw new UnauthorizedAccessException("You are not an eligible approver for this step.");

        var noteRequirement = currentStep?.NoteRequirement ?? NoteRequirement.None;
        if (noteRequirement == NoteRequirement.Required && string.IsNullOrWhiteSpace(note))
            throw new InvalidOperationException("A note is required to act on this step.");

        await _uow.Repository.AddAsync(new ApprovalAction
        {
            ApprovalInstanceId = instance.Id,
            StepNumber = instance.CurrentStepNumber,
            ActorEmployeeId = callerEmployeeId,
            Action = action,
            Note = noteRequirement == NoteRequirement.None ? null : note,
        }, token);

        if (action == ApprovalActionType.Declined)
        {
            instance.Status = ApprovalInstanceStatus.Declined;
            _uow.Repository.Update(instance);
            return new ApprovalActionResult(instance.Status, instance.CurrentStepNumber);
        }

        var minApprovals = currentStep?.MinApprovals ?? 1;
        var approverIdsForStep = instance.Actions
            .Where(a => a.StepNumber == instance.CurrentStepNumber && a.Action == ApprovalActionType.Approved)
            .Select(a => a.ActorEmployeeId)
            .Append(callerEmployeeId); // the action added above isn't in instance.Actions yet

        if (!ApprovalQuorum.IsStepCleared(minApprovals, approverIdsForStep))
        {
            _uow.Repository.Update(instance);
            return new ApprovalActionResult(instance.Status, instance.CurrentStepNumber);
        }

        var totalSteps = instance.Workflow?.Steps.Count ?? 1;
        if (instance.CurrentStepNumber >= totalSteps)
        {
            instance.Status = ApprovalInstanceStatus.Approved;
        }
        else
        {
            instance.CurrentStepNumber += 1;
        }

        _uow.Repository.Update(instance);
        return new ApprovalActionResult(instance.Status, instance.CurrentStepNumber);
    }

    private static ApprovalWorkflowStep? CurrentStep(ApprovalInstance instance) =>
        instance.Workflow?.Steps.SingleOrDefault(s => s.StepNumber == instance.CurrentStepNumber);

    // Maps the engine's own instance status back onto each of the 5 applications' pre-existing
    // ApprovalStatus enum, so every current report/query that reads that field keeps working
    // unchanged. InProgress means "still pending, just possibly advanced a step" -- there's no
    // partial-progress value on the legacy enum, so it stays ForApproval.
    public static ApprovalStatus MapInstanceStatus(ApprovalInstanceStatus status) => status switch
    {
        ApprovalInstanceStatus.Approved => ApprovalStatus.Approved,
        ApprovalInstanceStatus.Declined => ApprovalStatus.Declined,
        ApprovalInstanceStatus.Cancelled => ApprovalStatus.Cancelled,
        _ => ApprovalStatus.ForApproval,
    };
}
