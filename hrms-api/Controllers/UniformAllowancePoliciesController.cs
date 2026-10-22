using Onepunch.Common.Lib.Subscriptions;
using Asp.Versioning;
using Hrms.Api.Extensions;
using Hrms.Api.Filters;
using Hrms.Core.Services.UniformAllowance;
using Hrms.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    /// <summary>
    /// Client Settings > Uniform Allowance: a client's policy versions. A change is a new version
    /// starting on a future day; versions already used by a credit run can't be changed.
    /// </summary>
    [Route("api/v{version:apiVersion}/clients/{clientId:guid}/uniform-allowance-policies")]
    [ApiVersion("1.0")]
    [ApiController]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
    [RequireFeature(EntitlementKeys.ClientsAgency, WritesOnly = true)]
    public class UniformAllowancePoliciesController : ControllerBase
    {
        private readonly UniformAllowancePolicyService _policies;
        private readonly UniformAllowanceScheduleService _schedule;
        private readonly EmployeeService _employeeService;

        public UniformAllowancePoliciesController(
            UniformAllowancePolicyService policies, UniformAllowanceScheduleService schedule, EmployeeService employeeService)
        {
            _policies = policies;
            _schedule = schedule;
            _employeeService = employeeService;
        }

        [HttpGet]
        [RequirePermission("Workforce Setup:View")]
        [ProducesResponseType(typeof(ResponseModel<UniformAllowancePolicyListModel>), 200)]
        public async Task<IActionResult> List(Guid clientId, CancellationToken token) =>
            Ok(await _policies.ListAsync(clientId, token));

        /// <summary>The next credit dates from asOf (default today), with what each depends on.</summary>
        [HttpGet("preview")]
        [RequirePermission("Workforce Setup:View")]
        [ProducesResponseType(typeof(ResponseModel<List<UniformCreditPreviewModel>>), 200)]
        public async Task<IActionResult> Preview(Guid clientId, [FromQuery] DateOnly? asOf, [FromQuery] int count, CancellationToken token) =>
            Ok(await _schedule.PreviewAsync(clientId, asOf ?? PhTime.Today(DateTime.UtcNow), count == 0 ? 3 : count, token));

        [HttpPost]
        [RequirePermission("Workforce Setup:Edit")]
        [ProducesResponseType(typeof(ResponseModel<UniformAllowancePolicyModel>), 200)]
        public async Task<IActionResult> Create(Guid clientId, [FromBody] UpsertUniformAllowancePolicy payload, CancellationToken token)
        {
            var (userId, name) = await ResolveActorAsync(token);
            return Ok(await _policies.CreateAsync(clientId, payload, userId, name, token));
        }

        [HttpPut("{policyId:guid}")]
        [RequirePermission("Workforce Setup:Edit")]
        [ProducesResponseType(typeof(ResponseModel<UniformAllowancePolicyModel>), 200)]
        public async Task<IActionResult> Update(Guid clientId, Guid policyId, [FromBody] UpsertUniformAllowancePolicy payload, CancellationToken token) =>
            Ok(await _policies.UpdateAsync(clientId, policyId, payload, token));

        [HttpDelete("{policyId:guid}")]
        [RequirePermission("Workforce Setup:Edit")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Delete(Guid clientId, Guid policyId, CancellationToken token)
        {
            await _policies.DeleteAsync(clientId, policyId, token);
            return Ok();
        }

        // The caller's employee name when they have one, else their login email.
        private async Task<(Guid UserId, string Name)> ResolveActorAsync(CancellationToken token)
        {
            var userId = User.GetRequiredUserId();
            var email = User.GetUserClaim("email");
            var employeeId = await _employeeService.ResolveEmployeeIdAsync(userId, email, token);
            var name = employeeId is { } id
                ? (await _employeeService.GetDisplayNamesAsync([id], token)).GetValueOrDefault(id)
                : null;
            return (userId, string.IsNullOrWhiteSpace(name) ? email ?? "Unknown" : name);
        }
    }
}
