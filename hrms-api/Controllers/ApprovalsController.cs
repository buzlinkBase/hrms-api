using Asp.Versioning;
using Hrms.Api.Extensions;
using Hrms.Core.Services.Approvals;
using Hrms.Domain;
using Hrms.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    // Shared read-only endpoint for all 5 Applications types' approval progress/history -- used
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

        public ApprovalsController(ApprovalEngineService approvalEngine, EmployeeService employeeService)
        {
            _approvalEngine = approvalEngine;
            _employeeService = employeeService;
        }

        // Mirrors the permission code prefixes each of the 5 controllers already gate their own
        // View/Approve actions with (RequirePermissionAttribute can't be parametrized per-route,
        // so this shared controller checks manually instead of via the attribute).
        private static string PermissionPrefix(ApprovalApplicationType type) => type switch
        {
            ApprovalApplicationType.Leave => "Leave",
            ApprovalApplicationType.Overtime => "Overtime",
            ApprovalApplicationType.OfficialBusiness => "Official Business",
            ApprovalApplicationType.PassSlip => "Pass Slip",
            ApprovalApplicationType.Loan => "Loan/Deduction",
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

        [HttpGet("{applicationType}/{applicationId}")]
        [ProducesResponseType(typeof(ResponseModel<ApprovalInstanceResponse>), 200)]
        public async Task<IActionResult> Get(ApprovalApplicationType applicationType, Guid applicationId, CancellationToken token)
        {
            if (!User.HasAnyPermission($"{PermissionPrefix(applicationType)}:View"))
                return Forbid();

            var instance = await _approvalEngine.GetInstanceAsync(applicationType, applicationId, token);
            if (instance == null) return NotFound();

            var currentStep = instance.Workflow?.Steps.SingleOrDefault(s => s.StepNumber == instance.CurrentStepNumber);

            return Ok(new ApprovalInstanceResponse
            {
                ApplicationType = instance.ApplicationType,
                ApplicationId = instance.ApplicationId,
                ApprovalWorkflowId = instance.ApprovalWorkflowId,
                CurrentStepNumber = instance.CurrentStepNumber,
                TotalSteps = instance.Workflow?.Steps.Count ?? 1,
                Status = instance.Status,
                CurrentStepNoteRequirement = currentStep?.NoteRequirement ?? NoteRequirement.None,
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
            });
        }

        // Lets the frontend show/hide the Approve/Decline modal for the current caller without
        // guessing client-side -- mirrors ApprovalEngineService.RecordActionAsync's own
        // eligibility gate (Owner/Admin override isn't reflected here since it's an unconditional
        // "yes" for them; this endpoint answers "are you the assigned approver").
        [HttpGet("{applicationType}/{applicationId}/eligibility")]
        [ProducesResponseType(typeof(ResponseModel<bool>), 200)]
        public async Task<IActionResult> GetEligibility(ApprovalApplicationType applicationType, Guid applicationId, CancellationToken token)
        {
            if (!User.HasAnyPermission($"{PermissionPrefix(applicationType)}:Approve"))
                return Ok(false);

            if (User.IsOwnerOrAdmin()) return Ok(true);

            var callerEmployeeId = await _employeeService.ResolveEmployeeIdAsync(
                User.GetRequiredUserId(), User.GetUserClaim("email"), token);
            if (callerEmployeeId is not { } id) return Ok(false);

            var eligible = await _approvalEngine.IsCallerEligibleAsync(applicationType, applicationId, id, token);
            return Ok(eligible);
        }
    }
}
