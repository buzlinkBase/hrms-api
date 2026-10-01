using Asp.Versioning;
using Hrms.Api.Filters;
using Hrms.Core.Services;
using Hrms.Core.Services.Approvals;
using Hrms.Domain;
using Hrms.Domain.Entities.Approvals;
using Hrms.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;
using Onepunch.Common.Lib.Subscriptions;

namespace Hrms.Api.Controllers
{
    // Setup > Approval Workflows config screen -- CRUD for the saved policy that
    // ApprovalEngineService reads at runtime. See ApprovalsController for the read-only
    // per-application instance/history endpoint the other 5 modules' detail pages use.
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
    public class ApprovalWorkflowsController : ControllerBase
    {
        private readonly ApprovalWorkflowService _service;
        private readonly EmployeeService _employeeService;
        private readonly IEntitlementProvider _entitlements;
        private readonly ITenantProvider _tenantProvider;

        public ApprovalWorkflowsController(ApprovalWorkflowService service, EmployeeService employeeService,
            IEntitlementProvider entitlements, ITenantProvider tenantProvider)
        {
            _service = service;
            _employeeService = employeeService;
            _entitlements = entitlements;
            _tenantProvider = tenantProvider;
        }

        // Single-step workflows come with every plan; more than one step is approvals.multilevel.
        // Checked on save and on activation, so a workflow saved under Premium can't be switched
        // back on after a downgrade.
        private async Task EnsureStepsAllowedAsync(int stepCount, CancellationToken token)
        {
            if (stepCount <= 1) return;
            var snapshot = await _entitlements.GetAsync(_tenantProvider.TenantId, token);
            if (!snapshot.HasFeature(EntitlementKeys.ApprovalsMultilevel))
                throw PlanRestrictionException.FeatureRequired(EntitlementKeys.ApprovalsMultilevel,
                    "Approval workflows with more than one step need a plan with multi-level approvals.");
        }

        private async Task EnsureStepsAllowedAsync(Guid workflowId, CancellationToken token)
        {
            var workflow = await _service.GetAsync(workflowId, token);
            if (workflow != null) await EnsureStepsAllowedAsync(workflow.Steps.Count, token);
        }

        [HttpGet]
        [RequirePermission("Approval Workflows:View")]
        [ProducesResponseType(typeof(ResponseModel<List<ApprovalWorkflowResponse>>), 200)]
        public async Task<IActionResult> Get([FromQuery] ApprovalApplicationType applicationType, CancellationToken token)
        {
            // Close requests whose application was withdrawn/removed, and let any scheduled
            // version take over whose current workflow has drained -- so what's shown is current.
            await _service.ReconcileAsync(applicationType, token);
            var workflows = await _service.ListAsync(applicationType, token);
            var usage = await _service.GetUsageAsync(workflows.Select(w => w.Id).ToList(), token);
            return Ok(workflows.Select(w => ToResponse(w, usage[w.Id], workflows)));
        }

        [HttpGet("{id}")]
        [RequirePermission("Approval Workflows:View")]
        [ProducesResponseType(typeof(ResponseModel<ApprovalWorkflowResponse>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var workflow = await _service.GetAsync(id, token);
            if (workflow == null) return NotFound();
            var usage = await _service.GetUsageAsync([id], token);
            var siblings = await _service.ListAsync(workflow.ApplicationType, token);
            var response = ToResponse(workflow, usage[id], siblings);
            response.ApproverNames = await ApproverNamesAsync(workflow, token);
            return Ok(response);
        }

        private Task<Dictionary<Guid, string>> ApproverNamesAsync(ApprovalWorkflow workflow, CancellationToken token) =>
            _employeeService.GetDisplayNamesAsync(
                workflow.Steps
                    .Where(s => s.ApproverEmployeeId != null).Select(s => s.ApproverEmployeeId!.Value)
                    .Concat(workflow.Steps.SelectMany(s => s.NamedApprovers.Select(a => a.EmployeeId))),
                token);

        [HttpPost]
        [RequirePermission("Approval Workflows:Edit")]
        [ProducesResponseType(typeof(ResponseModel<ApprovalWorkflowResponse>), 200)]
        public async Task<IActionResult> Post([FromBody] ApprovalWorkflowRequest payload, CancellationToken token)
        {
            await EnsureStepsAllowedAsync(payload.Steps.Count, token);
            var workflow = FromRequest(payload);
            // New workflows start inactive (draft) -- Activate is the only path to IsActive =
            // true, so the "only one active per (ApplicationType, ScopeDepartmentId)" rule (see
            // ApprovalWorkflowService.ActivateAsync) is never at risk of a race with Create.
            workflow.IsActive = false;

            var created = await _service.CreateWorkflowAsync(workflow, token);
            return Ok(ToResponse(created, WorkflowUsage.None, []));
        }

        [HttpPut("{id}")]
        [RequirePermission("Approval Workflows:Edit")]
        [ProducesResponseType(typeof(ResponseModel<ApprovalWorkflowResponse>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] ApprovalWorkflowRequest payload, CancellationToken token)
        {
            await EnsureStepsAllowedAsync(payload.Steps.Count, token);
            var workflow = FromRequest(payload);
            var updated = await _service.UpdateWorkflowAsync(id, workflow, token);
            var usage = await _service.GetUsageAsync([id], token);
            var response = ToResponse(updated, usage[id], []);
            response.ApproverNames = await ApproverNamesAsync(updated, token);
            return Ok(response);
        }

        /// <summary>
        /// Activate when the current one drains: this version takes over from the workflow now
        /// active for its type/scope once that one has no requests in progress.
        /// </summary>
        [HttpPost("{id}/schedule-activation")]
        [RequirePermission("Approval Workflows:Edit")]
        [ProducesResponseType(typeof(ResponseModel<ScheduleActivationResult>), 200)]
        public async Task<IActionResult> ScheduleActivation(Guid id, CancellationToken token)
        {
            await EnsureStepsAllowedAsync(id, token);
            var activated = await _service.ScheduleActivationAsync(id, token);
            return Ok(new ScheduleActivationResult(activated));
        }

        [HttpDelete("{id}/schedule-activation")]
        [RequirePermission("Approval Workflows:Edit")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> CancelScheduledActivation(Guid id, CancellationToken token)
        {
            await _service.CancelScheduledActivationAsync(id, token);
            return Ok();
        }

        [HttpPost("{id}/activate")]
        [RequirePermission("Approval Workflows:Edit")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Activate(Guid id, CancellationToken token)
        {
            await EnsureStepsAllowedAsync(id, token);
            await _service.ActivateAsync(id, token);
            return Ok();
        }

        [HttpPost("{id}/deactivate")]
        [RequirePermission("Approval Workflows:Edit")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Deactivate(Guid id, CancellationToken token)
        {
            await _service.DeactivateAsync(id, token);
            return Ok();
        }

        [HttpDelete("{id}")]
        [RequirePermission("Approval Workflows:Edit")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.DeleteAsync(id, token);
            return Ok();
        }

        private static ApprovalWorkflow FromRequest(ApprovalWorkflowRequest payload) => new()
        {
            ApplicationType = payload.ApplicationType,
            Name = payload.Name,
            ScopeDepartmentId = payload.ScopeDepartmentId,
            Steps = payload.Steps.Select(s => new ApprovalWorkflowStep
            {
                StepNumber = s.StepNumber,
                ApproverType = s.ApproverType,
                ApproverEmployeeId = s.ApproverEmployeeId,
                ApproverDepartmentId = s.ApproverDepartmentId,
                ApproverPositionId = s.ApproverPositionId,
                MinApprovals = s.MinApprovals,
                NoteRequirement = s.NoteRequirement,
                NamedApprovers = s.NamedApproverEmployeeIds
                    .Select(empId => new ApprovalWorkflowStepApprover { EmployeeId = empId })
                    .ToList(),
            }).ToList(),
        };

        // siblings: the other workflows of the same type, to name the one this replaces / is replaced by.
        private static ApprovalWorkflowResponse ToResponse(
            ApprovalWorkflow workflow, WorkflowUsage usage, IReadOnlyCollection<ApprovalWorkflow> siblings) => new()
        {
            Id = workflow.Id,
            ApplicationType = workflow.ApplicationType,
            Name = workflow.Name,
            IsActive = workflow.IsActive,
            ScopeDepartmentId = workflow.ScopeDepartmentId,
            ScopeDepartmentName = workflow.ScopeDepartment?.Name,
            IsEditable = usage.CanEdit,
            InProgressCount = usage.InProgress,
            HasBeenUsed = usage.HasBeenUsed,
            CanEdit = usage.CanEdit,
            CanDelete = usage.CanDelete,
            ReplacesWorkflowId = workflow.ReplacesWorkflowId,
            ReplacesWorkflowName = siblings.FirstOrDefault(s => s.Id == workflow.ReplacesWorkflowId)?.Name,
            ReplacedByWorkflowName = siblings.FirstOrDefault(s => s.ReplacesWorkflowId == workflow.Id && !s.IsActive)?.Name,
            Steps = workflow.Steps.OrderBy(s => s.StepNumber).Select(s => new ApprovalWorkflowStepResponse
            {
                Id = s.Id,
                StepNumber = s.StepNumber,
                ApproverType = s.ApproverType,
                ApproverEmployeeId = s.ApproverEmployeeId,
                ApproverDepartmentId = s.ApproverDepartmentId,
                ApproverPositionId = s.ApproverPositionId,
                MinApprovals = s.MinApprovals,
                NoteRequirement = s.NoteRequirement,
                NamedApproverEmployeeIds = s.NamedApprovers.Select(a => a.EmployeeId).ToList(),
            }).ToList(),
        };
    }
}
