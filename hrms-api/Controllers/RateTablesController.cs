using Asp.Versioning;
using Hrms.Api.Extensions;
using Hrms.Core.Services.Compliance;
using Hrms.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
    public class RateTablesController : ControllerBase
    {
        private readonly RateTableService _service;
        private readonly ClientRateTableService _clientRateService;
        private readonly AccountInitService _accountInitService;
        private readonly ComplianceAcknowledgmentService _compliance;
        private readonly IMapper _mapper;

        public RateTablesController(RateTableService service,
            ClientRateTableService clientRateService,
            AccountInitService accountInitService,
            ComplianceAcknowledgmentService compliance,
            IMapper mapper)
        {
            _service = service;
            _clientRateService = clientRateService;
            _accountInitService = accountInitService;
            _compliance = compliance;
            _mapper = mapper;
        }

        // Rates below the DOLE minimum premiums are logged (who and when).
        private Task RecordRateDeviationsAsync(string scope, IEnumerable<(RateType Type, decimal Rate)> rates, CancellationToken token)
        {
            var byType = rates.GroupBy(r => r.Type).ToDictionary(g => g.Key, g => g.Last().Rate);
            return _compliance.RecordDeviationsAsync(scope, StatutoryDeviationRules.Evaluate(new RateSubject(byType)),
                HttpContext.ToComplianceActor(), token);
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResponseModel<List<RateTableModel>>), 200)]
        public async Task<IActionResult> Get(CancellationToken token)
        {
            var data = await _service.FindAllAsync(token);
            return Ok(_mapper.Map<List<RateTableModel>>(data));
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ResponseModel<RateTableModel>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<RateTableModel>(data));
        }

        [HttpPost]
        [RequirePermission("Statutory Tables:Create")]
        [ProducesResponseType(typeof(ResponseModel<RateTableModel>), 200)]
        public async Task<IActionResult> Post([FromBody] CreateRateTable payload, CancellationToken token)
        {
            await RecordRateDeviationsAsync(ComplianceAcknowledgmentService.CompanyScope,
                [(payload.Type, payload.Rate)], token);
            var data = _mapper.Map<RateTable>(payload);
            data.Description = payload.Type.ToString();
            await _service.AddAsync(data, token);
            var respModel = _mapper.Map<RateTableModel>(data);
            return Ok(respModel);
        }

        [HttpPut("{id}")]
        [RequirePermission("Statutory Tables:Edit")]
        [ProducesResponseType(typeof(ResponseModel<RateTableModel>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateRateTable payload, CancellationToken token)
        {
            payload.Id = payload.Id == Guid.Empty ? id : payload.Id;
            await RecordRateDeviationsAsync(ComplianceAcknowledgmentService.CompanyScope,
                [(payload.Type, payload.Rate)], token);
            await _service.UpdateAsync(payload, token);
            return Ok(_mapper.Map<RateTableModel>(payload));
        }

        [HttpDelete("{id}")]
        [RequirePermission("Statutory Tables:Delete")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.Delete(id, token);
            return Ok();
        }

        [HttpPost("bulk")]
        [RequirePermission("Statutory Tables:Edit", "Organization Setup:Edit")]
        [ProducesResponseType(204)]
        public async Task<IActionResult> BulkReplace([FromBody] List<CreateRateTable> payload, CancellationToken token)
        {
            await RecordRateDeviationsAsync(ComplianceAcknowledgmentService.CompanyScope,
                payload.Select(p => (p.Type, p.Rate)), token);
            var entities = _mapper.Map<List<RateTable>>(payload);
            await _service.BulkReplaceAsync(entities, token);
            return NoContent();
        }

        [HttpDelete]
        [RequirePermission("Statutory Tables:Delete")]
        [ProducesResponseType(204)]
        public async Task<IActionResult> ClearAll(CancellationToken token)
        {
            await _service.ClearAllAsync(token);
            return NoContent();
        }

        [HttpGet("client/{clientId:guid}")]
        [ProducesResponseType(typeof(ResponseModel<List<ClientRateTableModel>>), 200)]
        public async Task<IActionResult> GetClientRates(Guid clientId, CancellationToken token)
        {
            var data = await _clientRateService.FindByClientAsync(clientId, token);
            return Ok(_mapper.Map<List<ClientRateTableModel>>(data));
        }

        [HttpPost("client/{clientId:guid}/bulk")]
        [RequirePermission("Workforce Setup:Edit")]
        [ProducesResponseType(204)]
        public async Task<IActionResult> BulkReplaceClientRates(Guid clientId, [FromBody] List<ClientRateEntry> payload, CancellationToken token)
        {
            await RecordRateDeviationsAsync(ComplianceAcknowledgmentService.ClientScope(clientId),
                payload.Select(p => (p.Type, p.Rate)), token);
            var entities = payload.Select(p => new ClientRateTable { ClientId = clientId, Type = p.Type, Rate = p.Rate }).ToList();
            await _clientRateService.BulkReplaceForClientAsync(clientId, entities, token);
            return NoContent();
        }
    }
}
