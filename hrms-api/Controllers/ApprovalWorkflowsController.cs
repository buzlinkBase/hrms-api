using Asp.Versioning;
using Hrms.Api.Filters;
using Hrms.Core.Services.Approvals;
using Hrms.Domain;
using Hrms.Domain.Entities.Approvals;
using Hrms.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

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

        public ApprovalWorkflowsController(ApprovalWorkflowService service)
        {
            _service = service;
        }

        [HttpGet]
        [RequirePermission("Approval Workflows:View")]
        [ProducesResponseType(typeof(ResponseModel<List<ApprovalWorkflowResponse>>), 200)]
        public async Task<IActionResult> Get([FromQuery] ApprovalApplicationType applicationType, CancellationToken token)
        {
            var workflows = await _service.ListAsync(applicationType, token);
            var hasInstances = await Task.WhenAll(workflows.Select(w => _service.HasInstancesAsync(w.Id, token)));
            var result = workflows.Zip(hasInstances, (w, used) => ToResponse(w, !used));
            return Ok(result);
        }

        [HttpGet("{id}")]
        [RequirePermission("Approval Workflows:View")]
        [ProducesResponseType(typeof(ResponseModel<ApprovalWorkflowResponse>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var workflow = await _service.GetAsync(id, token);
            if (workflow == null) return NotFound();
            var isEditable = !await _service.HasInstancesAsync(id, token);
            return Ok(ToResponse(workflow, isEditable));
        }

        [HttpPost]
        [RequirePermission("Approval Workflows:Edit")]
        [ProducesResponseType(typeof(ResponseModel<ApprovalWorkflowResponse>), 200)]
        public async Task<IActionResult> Post([FromBody] ApprovalWorkflowRequest payload, CancellationToken token)
        {
            var workflow = FromRequest(payload);
            // New workflows start inactive (draft) -- Activate is the only path to IsActive =
            // true, so the "only one active per (ApplicationType, ScopeDepartmentId)" rule (see
            // ApprovalWorkflowService.ActivateAsync) is never at risk of a race with Create.
            workflow.IsActive = false;

            var created = await _service.CreateWorkflowAsync(workflow, token);
            return Ok(ToResponse(created, isEditable: true));
        }

        [HttpPut("{id}")]
        [RequirePermission("Approval Workflows:Edit")]
        [ProducesResponseType(typeof(ResponseModel<ApprovalWorkflowResponse>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] ApprovalWorkflowRequest payload, CancellationToken token)
        {
            var workflow = FromRequest(payload);
            var updated = await _service.UpdateWorkflowAsync(id, workflow, token);
            return Ok(ToResponse(updated, isEditable: true));
        }

        [HttpPost("{id}/activate")]
        [RequirePermission("Approval Workflows:Edit")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Activate(Guid id, CancellationToken token)
        {
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

        private static ApprovalWorkflowResponse ToResponse(ApprovalWorkflow workflow, bool isEditable) => new()
        {
            Id = workflow.Id,
            ApplicationType = workflow.ApplicationType,
            Name = workflow.Name,
            IsActive = workflow.IsActive,
            ScopeDepartmentId = workflow.ScopeDepartmentId,
            ScopeDepartmentName = workflow.ScopeDepartment?.Name,
            IsEditable = isEditable,
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
