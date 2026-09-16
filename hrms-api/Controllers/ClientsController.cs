using Asp.Versioning;
using Hrms.Api.Filters;
using Hrms.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
    public class ClientsController : ControllerBase
    {
        private readonly ClientService _service;
        private readonly ClientBillingInfoService _billingService;
        private readonly IMapper _mapper;
        public ClientsController(ClientService service, ClientBillingInfoService billingService, IMapper mapper)
        {
            _service = service;
            _billingService = billingService;
            _mapper = mapper;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResponseModel<List<ClientModel>>), 200)]
        public async Task<IActionResult> Get(CancellationToken token)
        {
            var data = await _service.FindAllAsync(token);
            return Ok(_mapper.Map<List<ClientModel>>(data));
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ResponseModel<ClientModel>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<ClientModel>(data));
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
