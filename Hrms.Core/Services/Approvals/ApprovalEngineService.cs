using Hrms.Domain.Entities;
using Hrms.Domain.Entities.Approvals;
using Hrms.Domain.Entities.EmployeeEntities;
using MassTransit;

namespace Hrms.Core.Services.Approvals;

public record ApprovalActionResult(ApprovalInstanceStatus InstanceStatus, int CurrentStepNumber);

// The runtime approval engine, shared across every Applications type (Leave/Overtime/Official
// Business/Pass Slip/Loan/Profile Update/Payroll Posting). Owns ApprovalInstance/
// ApprovalAction state only -- it never touches LeaveApplication.ApprovalStatus or its
// siblings directly (it has no generic way to, since they're unrelated tables). Callers (each
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
// and the application's own ApprovalStatus write land in one CommitChangesAsync. Publishing
// notification events (see PublishStepNotificationAsync/PublishResolutionNotificationAsync)
// happens before that commit too, on purpose -- the EF outbox (AddEntityFrameworkOutbox +
// UseBusOutbox in RabbitMqConfiguration) only actually delivers a Publish call made within the
// same DbContext transaction as the eventual SaveChanges, so it has to happen here, not after.
public class ApprovalEngineService
{
    private readonly IUnitOfWorkService _uow;
    private readonly IPublishEndpoint _publisher;
    private readonly ApproverEligibilityResolver _eligibility = new();

    public ApprovalEngineService(IUnitOfWorkService uow, IPublishEndpoint publisher)
    {
        _uow = uow;
        _publisher = publisher;
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

        // IX_ApprovalInstances_ApplicationType_ApplicationId is unique -- "one live instance per
        // application record" (see ApprovalInstanceConfig). A prior cycle for this exact
        // application can already exist and be resolved (e.g. a declined DTR/Payroll deletion
        // request being re-requested) without ever being cleaned up, so blindly inserting a new
        // row here would violate that constraint. Reuse and reset that same row for the new
        // cycle instead -- its past Actions are left in place as audit history ("declined, then
        // re-requested"), only the cycle state resets. An instance still InProgress is returned
        // as-is (idempotent) rather than restarted out from under whoever's already reviewing it.
        var existing = await GetInstanceAsync(applicationType, applicationId, token);
        if (existing != null)
        {
            if (existing.Status == ApprovalInstanceStatus.InProgress) return existing;

            existing.ApplicantEmployeeId = applicantEmployeeId;
            existing.ApprovalWorkflowId = workflow?.Id;
            existing.Workflow = workflow;
            existing.CurrentStepNumber = 1;
            existing.Status = ApprovalInstanceStatus.InProgress;
            existing.CycleNumber += 1;
            _uow.Repository.Update(existing);

            var restartStep = workflow?.Steps.SingleOrDefault(s => s.StepNumber == 1);
            if (restartStep != null)
                await PublishStepNotificationAsync(existing, restartStep, applicant, token);

            return existing;
        }

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

        var firstStep = workflow?.Steps.SingleOrDefault(s => s.StepNumber == 1);
        if (firstStep != null)
            await PublishStepNotificationAsync(instance, firstStep, applicant, token);

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
            .Include(i => i.Workflow).ThenInclude(w => w!.Steps).ThenInclude(s => s.ApproverEmployee)
            .Include(i => i.Workflow).ThenInclude(w => w!.Steps).ThenInclude(s => s.ApproverDepartment)
            .Include(i => i.Workflow).ThenInclude(w => w!.Steps).ThenInclude(s => s.ApproverPosition)
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
        return _eligibility.IsEligible(new ApprovalEligibilityContext(
            caller, applicant, currentStep, namedApproverIds, instance.ReassignedApproverEmployeeId));
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
        var instance = await GetInstanceAsync(type, applicationId, token);
        // Self-healed instances are already tracked as Added (StartAsync's own AddAsync call) --
        // calling Repository.Update on them below would downgrade that tracked state to Modified,
        // which skips the INSERT entirely (the row is never actually written), silently
        // orphaning the ApprovalAction row added further down (FOREIGN KEY constraint failed on
        // ApprovalInstanceId -- caught by PayrollBatchLifecycleServiceTests' real-SQLite
        // coverage). Guard every Update(instance) call below on NOT being this self-heal path;
        // the existing-instance path's own Update() calls are untouched.
        var isNewInstance = instance == null;
        instance ??= await StartAsync(type, applicationId, applicantEmployeeId, token);
        if (instance.Status != ApprovalInstanceStatus.InProgress)
            throw new InvalidOperationException("This application is no longer awaiting approval.");

        var caller = await _uow.Repository.Find<Employee>(e => e.Id == callerEmployeeId).AsNoTracking().FirstOrDefaultAsync(token)
            ?? throw new NotFoundException("Approver not found.");
        var applicant = await _uow.Repository.Find<Employee>(e => e.Id == instance.ApplicantEmployeeId).AsNoTracking().FirstOrDefaultAsync(token)
            ?? throw new NotFoundException("Applicant not found.");

        var currentStep = CurrentStep(instance);

        // Idempotency guard: a UI double-click or a client retry after a dropped response can
        // send the same actor's action for the same step twice. ApprovalQuorum.IsStepCleared
        // already de-dupes actors via .Distinct() so a repeat can't inflate quorum, but without
        // this check it would still insert a duplicate ApprovalAction row and -- if quorum was
        // exactly cleared by the first call -- re-run the step-advance/resolution logic and
        // publish duplicate notifications. Scoped to CurrentStepNumber + CycleNumber together
        // (not "ever acted on this instance"), so this only catches a repeat within THIS cycle's
        // current step: a legitimate later action by the same person on a step the instance has
        // since moved on to still goes through normally, and so does a fresh cycle after a
        // restart (StartAsync's restart branch bumps CycleNumber but reuses StepNumber=1, and
        // deliberately keeps the prior cycle's ApprovalAction rows as audit history -- without
        // CycleNumber in this filter, a re-request from the same original approver would be
        // wrongly treated as a duplicate of their action from the cycle that already got
        // declined). Excludes Reassigned since that's a different kind of event (an admin may
        // legitimately reassign more than once), and applies regardless of
        // callerHasOverrideAccess -- an override doesn't make a repeat submission any less of a
        // repeat.
        var alreadyActedOnCurrentStep = instance.Actions.Any(a =>
            a.StepNumber == instance.CurrentStepNumber &&
            a.CycleNumber == instance.CycleNumber &&
            a.ActorEmployeeId == callerEmployeeId &&
            a.Action != ApprovalActionType.Reassigned);
        if (alreadyActedOnCurrentStep)
            return new ApprovalActionResult(instance.Status, instance.CurrentStepNumber);

        var namedApproverIds = currentStep?.NamedApprovers.Select(a => a.EmployeeId).ToArray() ?? [];
        var eligibilityContext = new ApprovalEligibilityContext(
            caller, applicant, currentStep, namedApproverIds, instance.ReassignedApproverEmployeeId);

        if (!callerHasOverrideAccess && !_eligibility.IsEligible(eligibilityContext))
            throw new UnauthorizedAccessException("You are not an eligible approver for this step.");

        var noteRequirement = currentStep?.NoteRequirement ?? NoteRequirement.None;
        if (noteRequirement == NoteRequirement.Required && string.IsNullOrWhiteSpace(note))
            throw new InvalidOperationException("A note is required to act on this step.");

        await _uow.Repository.AddAsync(new ApprovalAction
        {
            ApprovalInstanceId = instance.Id,
            StepNumber = instance.CurrentStepNumber,
            CycleNumber = instance.CycleNumber,
            ActorEmployeeId = callerEmployeeId,
            Action = action,
            Note = noteRequirement == NoteRequirement.None ? null : note,
        }, token);

        if (action == ApprovalActionType.Declined)
        {
            instance.Status = ApprovalInstanceStatus.Declined;
            instance.ReassignedApproverEmployeeId = null;
            if (!isNewInstance) _uow.Repository.Update(instance);
            await PublishResolutionNotificationAsync(instance, applicant, note, token);
            return new ApprovalActionResult(instance.Status, instance.CurrentStepNumber);
        }

        var minApprovals = currentStep?.MinApprovals ?? 1;
        var approverIdsForStep = instance.Actions
            .Where(a => a.StepNumber == instance.CurrentStepNumber && a.Action == ApprovalActionType.Approved)
            .Select(a => a.ActorEmployeeId)
            .Append(callerEmployeeId); // the action added above isn't in instance.Actions yet

        if (!ApprovalQuorum.IsStepCleared(minApprovals, approverIdsForStep))
        {
            if (!isNewInstance) _uow.Repository.Update(instance);
            return new ApprovalActionResult(instance.Status, instance.CurrentStepNumber);
        }

        var totalSteps = instance.Workflow?.Steps.Count ?? 1;
        if (instance.CurrentStepNumber >= totalSteps)
        {
            instance.Status = ApprovalInstanceStatus.Approved;
            instance.ReassignedApproverEmployeeId = null;
            if (!isNewInstance) _uow.Repository.Update(instance);
            await PublishResolutionNotificationAsync(instance, applicant, note, token);
        }
        else
        {
            instance.CurrentStepNumber += 1;
            instance.ReassignedApproverEmployeeId = null;
            if (!isNewInstance) _uow.Repository.Update(instance);
            var nextStep = CurrentStep(instance);
            if (nextStep != null)
                await PublishStepNotificationAsync(instance, nextStep, applicant, token);
        }

        return new ApprovalActionResult(instance.Status, instance.CurrentStepNumber);
    }

    // Owner/Admin-only escape hatch for a step whose configured/resolved approver can't
    // actually act (out of office, left the company, wrongly assigned) -- swaps the CURRENT
    // step's sole eligible approver to a specific employee, without touching the shared
    // ApprovalWorkflow/ApprovalWorkflowStep template (which is permanently frozen once any
    // instance references it -- see ApprovalWorkflowService.EnsureEditableAsync). The override
    // is scoped to exactly this step: RecordActionAsync clears it the moment the step advances
    // or the instance resolves, so it never carries into a step it wasn't meant for. No
    // eligibility check on the act of reassigning itself -- gated entirely by the caller
    // (controller layer) being Owner/Admin, same as callerHasOverrideAccess elsewhere.
    public async Task ReassignApproverAsync(
        ApprovalApplicationType type,
        Guid applicationId,
        Guid newApproverEmployeeId,
        Guid reassignedByEmployeeId,
        string? note,
        CancellationToken token = default)
    {
        var instance = await GetInstanceAsync(type, applicationId, token)
            ?? throw new NotFoundException("No approval instance found for this application.");
        if (instance.Status != ApprovalInstanceStatus.InProgress)
            throw new InvalidOperationException("This application is no longer awaiting approval.");

        var newApprover = await _uow.Repository.Find<Employee>(e => e.Id == newApproverEmployeeId)
            .AsNoTracking().FirstOrDefaultAsync(token)
            ?? throw new NotFoundException("Employee not found.");
        var applicant = await _uow.Repository.Find<Employee>(e => e.Id == instance.ApplicantEmployeeId)
            .AsNoTracking().FirstOrDefaultAsync(token)
            ?? throw new NotFoundException("Applicant not found.");

        instance.ReassignedApproverEmployeeId = newApproverEmployeeId;
        _uow.Repository.Update(instance);

        await _uow.Repository.AddAsync(new ApprovalAction
        {
            ApprovalInstanceId = instance.Id,
            StepNumber = instance.CurrentStepNumber,
            CycleNumber = instance.CycleNumber,
            ActorEmployeeId = reassignedByEmployeeId,
            Action = ApprovalActionType.Reassigned,
            Note = note,
        }, token);

        if (!string.IsNullOrWhiteSpace(newApprover.Email) || newApprover.UserId != null)
        {
            var (deliverEmail, deliverPush) = await ResolveDeliveryFlagsAsync(newApprover.Id, instance.ApplicationType, token);

            await _publisher.Publish(new ApprovalNotificationRequested
            {
                RecipientEmail = newApprover.Email ?? string.Empty,
                RecipientName = $"{newApprover.FirstName} {newApprover.LastName}".Trim(),
                ApplicationTypeLabel = ApplicationTypeLabel(instance.ApplicationType),
                ApplicantName = $"{applicant.FirstName} {applicant.LastName}".Trim(),
                StatusLabel = "Pending Your Approval",
                StepNumber = instance.CurrentStepNumber,
                TotalSteps = instance.Workflow?.Steps.Count ?? 1,
                ApplicationType = instance.ApplicationType.ToString(),
                ApplicationId = instance.ApplicationId,
                ApprovalInstanceId = instance.Id,
                RecipientUserId = newApprover.UserId,
                DeliverEmail = deliverEmail && !string.IsNullOrWhiteSpace(newApprover.Email),
                DeliverPush = deliverPush && newApprover.UserId != null,
            }, token);
        }
    }

    private static ApprovalWorkflowStep? CurrentStep(ApprovalInstance instance) =>
        instance.Workflow?.Steps.SingleOrDefault(s => s.StepNumber == instance.CurrentStepNumber);

    // Who a step's ApproverType actually resolves to right now, for notification purposes -- the
    // same resolution rules ApproverEligibilityResolver checks against a single caller, just
    // materialized into the full candidate list here since a notification may need to reach
    // several people (a whole department/position pool) rather than approve/reject one.
    private async Task<List<Employee>> ResolveNotificationRecipientsAsync(
        ApprovalWorkflowStep step, Employee applicant, CancellationToken token)
    {
        List<Employee> candidates;
        switch (step.ApproverType)
        {
            case ApproverType.Person:
                candidates = step.ApproverEmployeeId is { } personId
                    ? await _uow.Repository.Find<Employee>(e => e.Id == personId).AsNoTracking().ToListAsync(token)
                    : [];
                break;
            case ApproverType.Department:
                candidates = step.ApproverDepartmentId is { } deptId
                    ? await _uow.Repository.Find<Employee>(e => e.DepartmentId == deptId && e.UserId != null).AsNoTracking().ToListAsync(token)
                    : [];
                break;
            case ApproverType.Position:
                candidates = step.ApproverPositionId is { } posId
                    ? await _uow.Repository.Find<Employee>(e => e.PositionId == posId && e.UserId != null).AsNoTracking().ToListAsync(token)
                    : [];
                break;
            case ApproverType.ApplicantManager:
                candidates = applicant.ManagerId is { } managerId
                    ? await _uow.Repository.Find<Employee>(e => e.Id == managerId).AsNoTracking().ToListAsync(token)
                    : [];
                break;
            case ApproverType.ApplicantDepartment:
                candidates = applicant.DepartmentId is { } applicantDeptId
                    ? await _uow.Repository.Find<Employee>(e => e.DepartmentId == applicantDeptId && e.UserId != null).AsNoTracking().ToListAsync(token)
                    : [];
                break;
            default:
                candidates = [];
                break;
        }

        var namedIds = step.NamedApprovers.Select(a => a.EmployeeId).ToHashSet();
        if (namedIds.Count > 0)
            candidates = candidates.Where(c => namedIds.Contains(c.Id)).ToList();

        // Reachable via at least one channel -- email, push (needs a linked portal login), or
        // both. A candidate with neither is filtered out since no notification could ever reach
        // them either way.
        return candidates
            .Where(c => !string.IsNullOrWhiteSpace(c.Email) || c.UserId != null)
            .ToList();
    }

    private async Task PublishStepNotificationAsync(
        ApprovalInstance instance, ApprovalWorkflowStep step, Employee applicant, CancellationToken token)
    {
        var recipients = await ResolveNotificationRecipientsAsync(step, applicant, token);
        var totalSteps = instance.Workflow?.Steps.Count ?? 1;
        var applicantName = $"{applicant.FirstName} {applicant.LastName}".Trim();

        foreach (var recipient in recipients)
        {
            var (deliverEmail, deliverPush) = await ResolveDeliveryFlagsAsync(recipient.Id, instance.ApplicationType, token);

            await _publisher.Publish(new ApprovalNotificationRequested
            {
                RecipientEmail = recipient.Email ?? string.Empty,
                RecipientName = $"{recipient.FirstName} {recipient.LastName}".Trim(),
                ApplicationTypeLabel = ApplicationTypeLabel(instance.ApplicationType),
                ApplicantName = applicantName,
                StatusLabel = "Pending Your Approval",
                StepNumber = instance.CurrentStepNumber,
                TotalSteps = totalSteps,
                ApplicationType = instance.ApplicationType.ToString(),
                ApplicationId = instance.ApplicationId,
                ApprovalInstanceId = instance.Id,
                RecipientUserId = recipient.UserId,
                DeliverEmail = deliverEmail && !string.IsNullOrWhiteSpace(recipient.Email),
                DeliverPush = deliverPush && recipient.UserId != null,
            }, token);
        }
    }

    private async Task PublishResolutionNotificationAsync(
        ApprovalInstance instance, Employee applicant, string? note, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(applicant.Email) && applicant.UserId == null) return;

        var (deliverEmail, deliverPush) = await ResolveDeliveryFlagsAsync(applicant.Id, instance.ApplicationType, token);

        await _publisher.Publish(new ApprovalNotificationRequested
        {
            RecipientEmail = applicant.Email ?? string.Empty,
            RecipientName = $"{applicant.FirstName} {applicant.LastName}".Trim(),
            ApplicationTypeLabel = ApplicationTypeLabel(instance.ApplicationType),
            ApplicantName = $"{applicant.FirstName} {applicant.LastName}".Trim(),
            StatusLabel = instance.Status == ApprovalInstanceStatus.Approved ? "Approved" : "Declined",
            Note = note,
            ApplicationType = instance.ApplicationType.ToString(),
            ApplicationId = instance.ApplicationId,
            ApprovalInstanceId = instance.Id,
            RecipientUserId = applicant.UserId,
            DeliverEmail = deliverEmail && !string.IsNullOrWhiteSpace(applicant.Email),
            DeliverPush = deliverPush && applicant.UserId != null,
        }, token);
    }

    // No row = both channels on -- opt-out model, avoids a backfill migration; see
    // NotificationPreference's own doc comment. Queried directly via _uow rather than through
    // NotificationPreferenceService so this engine's constructor doesn't pick up an extra
    // dependency purely for this lookup -- that service exists for the employee-facing settings
    // CRUD (get/update own preferences), a different concern from this internal resolution.
    private async Task<(bool Email, bool Push)> ResolveDeliveryFlagsAsync(
        Guid employeeId, ApprovalApplicationType applicationType, CancellationToken token)
    {
        var preference = await _uow.Repository
            .Find<NotificationPreference>(x => x.EmployeeId == employeeId && x.ApplicationType == applicationType)
            .AsNoTracking()
            .FirstOrDefaultAsync(token);
        return preference == null ? (true, true) : (preference.EmailEnabled, preference.PushEnabled);
    }

    private static string ApplicationTypeLabel(ApprovalApplicationType type) => type switch
    {
        ApprovalApplicationType.Leave => "Leave",
        ApprovalApplicationType.Overtime => "Overtime",
        ApprovalApplicationType.OfficialBusiness => "Official Business",
        ApprovalApplicationType.PassSlip => "Pass Slip",
        ApprovalApplicationType.Loan => "Loan/Deduction",
        ApprovalApplicationType.ProfileUpdate => "Profile Update",
        ApprovalApplicationType.PayrollPosting => "Payroll Posting",
        ApprovalApplicationType.Dtr => "DTR Posting",
        ApprovalApplicationType.DtrDeletion => "DTR Deletion",
        ApprovalApplicationType.PayrollPostingDeletion => "Payroll Posting Deletion",
        _ => type.ToString(),
    };

    // Maps the engine's own instance status back onto each application's pre-existing
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
