using Onepunch.Common.Lib.Subscriptions;
using Asp.Versioning;
using Hrms.Api.Filters;
using Hrms.Api.RateLimiting;
using Hrms.Core.Services.Storage;
using Hrms.Domain.Entities;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
    [RequireFeature(EntitlementKeys.ClientsAgency, WritesOnly = true)]
    public class ClientsController : ControllerBase
    {
        private readonly ClientService _service;
        private readonly ClientBillingInfoService _billingService;
        private readonly IMapper _mapper;
        private readonly OrganizationImageService _images;
        private readonly ImageUrlResolver _imageUrls;
        public ClientsController(ClientService service, ClientBillingInfoService billingService, IMapper mapper,
            OrganizationImageService images, ImageUrlResolver imageUrls)
        {
            _service = service;
            _billingService = billingService;
            _mapper = mapper;
            _images = images;
            _imageUrls = imageUrls;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResponseModel<List<ClientModel>>), 200)]
        public async Task<IActionResult> Get(CancellationToken token)
        {
            var data = await _service.FindAllAsync(token);
            var models = new List<ClientModel>();
            foreach (var client in data)
            {
                var model = _mapper.Map<ClientModel>(client);
                model.LogoUrl = await _imageUrls.DisplayUrlAsync(client.LogoKey, token);
                models.Add(model);
            }
            return Ok(models);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ResponseModel<ClientModel>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            var model = _mapper.Map<ClientModel>(data);
            if (model != null) model.LogoUrl = await _imageUrls.DisplayUrlAsync(data?.LogoKey, token);
            return Ok(model);
        }

        /// <summary>Uploads (or replaces) the client's logo: PNG, JPG or WebP up to 2 MB.</summary>
        [HttpPost("{id:guid}/logo")]
        // Create too: the New Client form uploads the logo right after creating the record.
        [RequirePermission("Workforce Setup:Edit", "Workforce Setup:Create")]
        [EnableRateLimiting(RateLimitPolicies.FileUpload)]
        [RequestSizeLimit(4 * 1024 * 1024)]
        [ProducesResponseType(typeof(ResponseModel<ImageUploadResult>), 200)]
        public async Task<IActionResult> UploadLogo(Guid id, IFormFile file, CancellationToken token)
        {
            await using var upload = file.OpenReadStream();
            var url = await _images.SetClientLogoAsync(id, upload, file.Length, file.FileName, token);
            return Ok(new ImageUploadResult(url));
        }

        [HttpDelete("{id:guid}/logo")]
        [RequirePermission("Workforce Setup:Edit")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> RemoveLogo(Guid id, CancellationToken token)
        {
            await _images.RemoveClientLogoAsync(id, token);
            return Ok();
        }

        [HttpPost]
        [RequirePermission("Workforce Setup:Create")]
        [ProducesResponseType(typeof(ResponseModel<ClientModel>), 200)]
        public async Task<IActionResult> Post([FromBody] CreateClient payload, CancellationToken token)
        {
            var data = _mapper.Map<Client>(payload);
            await _service.AddAsync(data, token);
            var respModel = _mapper.Map<ClientModel>(data);
            return Ok(respModel);
        }

        [HttpPut("{id}")]
        [RequirePermission("Workforce Setup:Edit")]
        [ProducesResponseType(typeof(ResponseModel<ClientModel>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateClient payload, CancellationToken token)
        {
            payload.Id = payload.Id == Guid.Empty ? id : payload.Id;
            await _service.UpdateAsync(payload, token);
            return Ok(_mapper.Map<ClientModel>(payload));
        }

        [HttpDelete("{id}")]
        [RequirePermission("Workforce Setup:Delete")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.DeleteAsync(id, token);
            return Ok();
        }

        [HttpGet("{clientId:guid}/billing-info")]
        [ProducesResponseType(typeof(ResponseModel<ClientBillingInfoModel>), 200)]
        public async Task<IActionResult> GetBillingInfo(Guid clientId, CancellationToken token)
        {
            var data = await _billingService.FindByClientIdAsync(clientId, token);
            return Ok(data == null ? new ClientBillingInfoModel() : _mapper.Map<ClientBillingInfoModel>(data));
        }

        [HttpPut("{clientId:guid}/billing-info")]
        [RequirePermission("Workforce Setup:Edit")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> UpdateBillingInfo(Guid clientId, [FromBody] UpdateClientBillingInfo payload, CancellationToken token)
        {
            await _billingService.SaveAsync(clientId, payload, token);
            return Ok("success");
        }
    }
}
