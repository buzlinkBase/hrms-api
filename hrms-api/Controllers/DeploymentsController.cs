using Asp.Versioning;
using Hrms.Api.Filters;
using Hrms.Core.Services.ClientPortal;
using Hrms.Core.Services.Deployments;
using Hrms.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;
using Onepunch.Common.Lib.Subscriptions;

namespace Hrms.Api.Controllers;

/// <summary>
/// Client sites, their posts and contracted manning, and who is deployed where (with history). Reads follow the clients screen; changes
/// need Workforce Setup edit rights.
/// </summary>
[Route("api/v{version:apiVersion}/deployments")]
[ApiVersion("1.0")]
[ApiController]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
[RequireFeature(EntitlementKeys.ClientsAgency, WritesOnly = true)]
public class DeploymentsController(ClientSiteService sites, ClientPostService posts, DeploymentService deployments, ClientPortalLiveService live) : ControllerBase
{
    private const string Edit = "Workforce Setup:Edit";

    // ---- sites ----

    [HttpGet("clients/{clientId:guid}/sites")]
    [ProducesResponseType(typeof(ResponseModel<List<ClientSiteModel>>), 200)]
    public async Task<IActionResult> Sites(Guid clientId, CancellationToken token) =>
        Ok(await sites.ListAsync(clientId, DeploymentClock.Today(), token));

    [HttpPost("clients/{clientId:guid}/sites")]
    [RequirePermission(Edit)]
    public async Task<IActionResult> AddSite(Guid clientId, [FromBody] ClientSiteRequest request, CancellationToken token) =>
        Ok((await sites.SaveAsync(clientId, null, request, token)).Id);

    [HttpPut("clients/{clientId:guid}/sites/{id:guid}")]
    [RequirePermission(Edit)]
    public async Task<IActionResult> UpdateSite(Guid clientId, Guid id, [FromBody] ClientSiteRequest request, CancellationToken token) =>
        Ok((await sites.SaveAsync(clientId, id, request, token)).Id);

    [HttpDelete("sites/{id:guid}")]
    [RequirePermission(Edit)]
    public async Task<IActionResult> DeleteSite(Guid id, CancellationToken token)
    {
        await sites.DeleteAsync(id, DeploymentClock.Today(), token);
        return Ok("success");
    }

    // ---- posts and manning ----

    [HttpGet("sites/{siteId:guid}/posts")]
    [ProducesResponseType(typeof(ResponseModel<List<ClientPostModel>>), 200)]
    public async Task<IActionResult> Posts(Guid siteId, CancellationToken token) =>
        Ok(await posts.ListAsync(siteId, DeploymentClock.Today(), token));

    [HttpPost("sites/{siteId:guid}/posts")]
    [RequirePermission(Edit)]
    public async Task<IActionResult> AddPost(Guid siteId, [FromBody] ClientPostRequest request, CancellationToken token) =>
        Ok((await posts.SaveAsync(siteId, null, request, token)).Id);

    [HttpPut("sites/{siteId:guid}/posts/{id:guid}")]
    [RequirePermission(Edit)]
    public async Task<IActionResult> UpdatePost(Guid siteId, Guid id, [FromBody] ClientPostRequest request, CancellationToken token) =>
        Ok((await posts.SaveAsync(siteId, id, request, token)).Id);

    [HttpDelete("posts/{id:guid}")]
    [RequirePermission(Edit)]
    public async Task<IActionResult> DeletePost(Guid id, CancellationToken token)
    {
        await posts.DeleteAsync(id, DeploymentClock.Today(), token);
        return Ok("success");
    }

    [HttpPost("posts/{postId:guid}/requirements")]
    [RequirePermission(Edit)]
    [ProducesResponseType(typeof(ResponseModel<PostRequirementModel>), 200)]
    public async Task<IActionResult> AddRequirement(Guid postId, [FromBody] PostRequirementRequest request, CancellationToken token) =>
        Ok(await posts.AddRequirementAsync(postId, request, token));

    [HttpPost("requirements/{id:guid}/end")]
    [RequirePermission(Edit)]
    public async Task<IActionResult> EndRequirement(Guid id, [FromBody] EndDeploymentRequest request, CancellationToken token)
    {
        await posts.EndRequirementAsync(id, request.EndDate, token);
        return Ok("success");
    }

    // ---- deployments ----

    [HttpGet]
    [ProducesResponseType(typeof(ResponseModel<List<DeploymentModel>>), 200)]
    public async Task<IActionResult> List([FromQuery] Guid? clientId, [FromQuery] Guid? siteId, [FromQuery] Guid? postId, [FromQuery] Guid? employeeId,
        [FromQuery] DateOnly? activeOn, CancellationToken token) =>
        Ok(await deployments.ListAsync(new DeploymentQuery(clientId, siteId, postId, employeeId, activeOn), DeploymentClock.Today(), token));

    [HttpPost]
    [RequirePermission(Edit)]
    [ProducesResponseType(typeof(ResponseModel<DeploymentModel>), 200)]
    public async Task<IActionResult> Assign([FromBody] AssignDeploymentRequest request, CancellationToken token) =>
        Ok(await deployments.AssignAsync(request, DeploymentClock.Today(), token));

    [HttpPost("{id:guid}/end")]
    [RequirePermission(Edit)]
    [ProducesResponseType(typeof(ResponseModel<DeploymentModel>), 200)]
    public async Task<IActionResult> End(Guid id, [FromBody] EndDeploymentRequest request, CancellationToken token) =>
        Ok(await deployments.EndAsync(id, request, DeploymentClock.Today(), token));

    [HttpGet("clients/{clientId:guid}/coverage")]
    [ProducesResponseType(typeof(ResponseModel<CoverageReport>), 200)]
    public async Task<IActionResult> Coverage(Guid clientId, [FromQuery] DateOnly? date, CancellationToken token) =>
        Ok(await deployments.CoverageAsync(clientId, date ?? DeploymentClock.Today(), token));

    /// <summary>
    /// Sends the coming week's manning (shifts needed and who is deployed) to the Client Portal now, for one client or all, instead of
    /// waiting for the nightly send. Returns how many clients were sent.
    /// </summary>
    [HttpPost("portal-manning")]
    [RequirePermission(Edit)]
    [ProducesResponseType(typeof(ResponseModel<int>), 200)]
    public async Task<IActionResult> SendManning([FromQuery] Guid? clientId, CancellationToken token) =>
        Ok(await live.PublishManningAsync(clientId, token));
}
