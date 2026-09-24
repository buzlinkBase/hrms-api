using Asp.Versioning;
using Hrms.Api.Extensions;
using Hrms.Core;
using Hrms.Core.Services;
using Hrms.Core.Services.Approvals;
using Hrms.Domain;
using Hrms.Domain.Entities.Approvals;
using Hrms.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    // Shared read-only endpoint for every Applications type's approval progress/history -- used
    // by each module's detail-page timeline and by the approve/decline modal's eligibility check.
    // See ApprovalWorkflowsController for the Setup-side config CRUD.
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
    public class ApprovalsController : ControllerBase
    {
        private readonly ApprovalEngineService _approvalEngine;
        private readonly EmployeeService _employeeService;
        private readonly PayrollBatchService _payrollBatchService;

        public ApprovalsController(ApprovalEngineService approvalEngine, EmployeeService employeeService, PayrollBatchService payrollBatchService)
        {
            _approvalEngine = approvalEngine;
            _employeeService = employeeService;
            _payrollBatchService = payrollBatchService;
        }

        // Mirrors the permission code prefixes each application's own controller already gates
        // its View/Approve actions with (RequirePermissionAttribute can't be parametrized per-
        // route, so this shared controller checks manually instead of via the attribute).
        // PayrollPosting is the one exception: it's a single shared approval type across all 4
        // Payroll run types (Regular/13th Month/Last Pay/Year-End Adjustment), each gated by a
        // DIFFERENT permission prefix ("Payroll Run" vs "13th Month Run" vs...) -- a static
        // switch can't express that, so it needs an async lookup of the actual PayrollBatch to
        // resolve which run type applicationId belongs to, via the exact same
        // PayrollType -> RunTypeFeature mapping PayrollsController.ValidateBatchPermissionAsync
        // already uses for the same purpose.
        private async Task<string> ResolvePermissionPrefixAsync(ApprovalApplicationType type, Guid applicationId, CancellationToken token)
        {
            if (type == ApprovalApplicationType.PayrollPosting || type == ApprovalApplicationType.PayrollPostingDeletion)
            {
                var batch = await _payrollBatchService.FineOneAsync(applicationId, token)
                    ?? throw new NotFoundException("Payroll batch not found.");
                return PayrollsController.RunTypeFeature[batch.PayrollType];
            }

            return type switch
            {
                ApprovalApplicationType.Leave => "Leave",
                ApprovalApplicationType.Overtime => "Overtime",
                ApprovalApplicationType.OfficialBusiness => "Official Business",
                ApprovalApplicationType.PassSlip => "Pass Slip",
                ApprovalApplicationType.Loan => "Loan/Deduction",
                ApprovalApplicationType.ProfileUpdate => "Profile Update",
                ApprovalApplicationType.Dtr => "DTR Master",
                ApprovalApplicationType.DtrDeletion => "DTR Master",
                _ => throw new ArgumentOutOfRangeException(nameof(type)),
            };
        }

        // Two ways in: the admin/HR coarse {Row}:View permission (today's case), or being the
        // applicant looking at their own submission's progress from the Employee Portal -- the
        // portal's self-service screens have no {Row}:View grant at all, so without this an
        // employee could never see "pending with X" on their own request.
        [HttpGet("{applicationType}/{applicationId}")]
        [ProducesResponseType(typeof(ResponseModel<ApprovalInstanceResponse>), 200)]
        public async Task<IActionResult> Get(ApprovalApplicationType applicationType, Guid applicationId, CancellationToken token)
        {
            var instance = await _approvalEngine.GetInstanceAsync(applicationType, applicationId, token);
            if (instance == null) return NotFound();

            if (!User.HasAnyPermission($"{await ResolvePermissionPrefixAsync(applicationType, applicationId, token)}:View"))
            {
                var callerEmployeeId = await _employeeService.ResolveEmployeeIdAsync(
                    User.GetRequiredUserId(), User.GetUserClaim("email"), token);
                var isOwner = callerEmployeeId is { } id && id == instance.ApplicantEmployeeId;
                if (!isOwner) return Forbid();
            }

            var currentStep = instance.Status == ApprovalInstanceStatus.InProgress
                ? instance.Workflow?.Steps.SingleOrDefault(s => s.StepNumber == instance.CurrentStepNumber)
                : null;

            return Ok(new ApprovalInstanceResponse
            {
                ApplicationType = instance.ApplicationType,
                ApplicationId = instance.ApplicationId,
                ApprovalWorkflowId = instance.ApprovalWorkflowId,
                CurrentStepNumber = instance.CurrentStepNumber,
                TotalSteps = instance.Workflow?.Steps.Count ?? 1,
                Status = instance.Status,
                CurrentStepNoteRequirement = currentStep?.NoteRequirement ?? NoteRequirement.None,
                CurrentStepApproverType = currentStep?.ApproverType,
                CurrentStepApproverLabel = instance.ReassignedApproverEmployeeId is { } reassignedId
                    ? await ResolveEmployeeLabelAsync(reassignedId, token)
                    : ResolveApproverLabel(currentStep),
                Actions = instance.Actions
                    .OrderBy(a => a.CreatedAt)
                    .Select(a => new ApprovalActionResponse
                    {
                        StepNumber = a.StepNumber,
                        ActorEmployeeId = a.ActorEmployeeId,
                        Action = a.Action,
                        Note = a.Note,
                        CreatedAt = a.CreatedAt,
                    })
                    .ToList(),
                // Every step of the workflow, not just the current one -- lets the Approval
                // Progress timeline show who (or which department) handles each upcoming step.
                Steps = (instance.Workflow?.Steps ?? [])
                    .OrderBy(s => s.StepNumber)
                    .Select(s => new ApprovalStepSummaryResponse
                    {
                        StepNumber = s.StepNumber,
                        ApproverType = s.ApproverType,
                        ApproverLabel = ResolveApproverLabel(s),
                        NoteRequirement = s.NoteRequirement,
                    })
                    .ToList(),
            });
        }

        private async Task<string?> ResolveEmployeeLabelAsync(Guid employeeId, CancellationToken token)
        {
            var employee = await _employeeService.FineOneAsync(employeeId, token);
            return employee == null ? null : $"{employee.FirstName} {employee.LastName}".Trim();
        }

        private static string? ResolveApproverLabel(ApprovalWorkflowStep? step) => step?.ApproverType switch
        {
            ApproverType.Person => step.ApproverEmployee != null
                ? $"{step.ApproverEmployee.FirstName} {step.ApproverEmployee.LastName}".Trim()
                : null,
            ApproverType.Department => step.ApproverDepartment?.Name,
            ApproverType.Position => step.ApproverPosition?.Name,
            ApproverType.ApplicantManager => "Your Manager",
            ApproverType.ApplicantDepartment => "Your Department",
            _ => null,
        };

        // Lets the frontend show/hide the Approve/Decline modal for the current caller without
        // guessing client-side -- mirrors ApprovalEngineService.RecordActionAsync's own
        // eligibility gate (Owner/Admin override isn't reflected here since it's an unconditional
        // "yes" for them; this endpoint answers "are you the assigned approver").
        [HttpGet("{applicationType}/{applicationId}/eligibility")]
        [ProducesResponseType(typeof(ResponseModel<bool>), 200)]
        public async Task<IActionResult> GetEligibility(ApprovalApplicationType applicationType, Guid applicationId, CancellationToken token)
        {
            if (!User.HasAnyPermission($"{await ResolvePermissionPrefixAsync(applicationType, applicationId, token)}:Approve"))
                return Ok(false);

            if (User.IsOwnerOrAdmin()) return Ok(true);

            var callerEmployeeId = await _employeeService.ResolveEmployeeIdAsync(
                User.GetRequiredUserId(), User.GetUserClaim("email"), token);
            if (callerEmployeeId is not { } id) return Ok(false);

            var eligible = await _approvalEngine.IsCallerEligibleAsync(applicationType, applicationId, id, token);
            return Ok(eligible);
        }

        // Owner/Admin-only escape hatch for a step whose configured/resolved approver can't
        // actually act -- reassigns the CURRENT step to a specific employee. See
        // ApprovalEngineService.ReassignApproverAsync for why this can't be a per-module
        // permission check the way Approve/Decline are (it's an admin power move, not a
        // workflow step action).
        [HttpPost("{applicationType}/{applicationId}/reassign")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Reassign(
            ApprovalApplicationType applicationType, Guid applicationId,
            [FromBody] ReassignApproverRequest body, CancellationToken token)
        {
            if (!User.IsOwnerOrAdmin()) return Forbid();

            var callerEmployeeId = await _employeeService.ResolveEmployeeIdAsync(
                User.GetRequiredUserId(), User.GetUserClaim("email"), token)
                ?? throw new InvalidOperationException(
                    "Your account isn't linked to an Employee record, so this action can't be attributed to you. Contact an admin to link your account.");

            await _approvalEngine.ReassignApproverAsync(
                applicationType, applicationId, body.NewApproverEmployeeId, callerEmployeeId, body.Note, token);
            // ApprovalEngineService never commits (callers own the transaction, same as every
            // other engine method) -- _employeeService shares the same scoped
            // IUnitOfWorkService, so its CommitChangesAsync finalizes the same transaction
            // ReassignApproverAsync just wrote to.
            await _employeeService.CommitChangesAsync(token);
            return Ok("success");
        }
    }
}
