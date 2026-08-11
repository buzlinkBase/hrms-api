using Asp.Versioning;
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
    public class TravelOrderApplicationsController : ControllerBase
    {
        private readonly TravelOrderApplicationService _service;
        private readonly IMapper _mapper;

        public TravelOrderApplicationsController(TravelOrderApplicationService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResponseModel<List<TravelOrderApplicationModel>>), 200)]
        public async Task<IActionResult> Get([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken token)
        {
            var data = await _service.FindAllAsync(token, from, to);
            return Ok(_mapper.Map<List<TravelOrderApplicationModel>>(data));
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ResponseModel<TravelOrderApplicationModel>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<TravelOrderApplicationModel>(data));
        }

        [HttpPost]
        [ProducesResponseType(typeof(ResponseModel<TravelOrderApplicationModel>), 200)]
        public async Task<IActionResult> Post([FromBody] CreateTravelOrderApplication payload, CancellationToken token)
        {
            var data = _mapper.Map<TravelOrderApplication>(payload);
            await _service.AddAsync(data, token);
            return Ok(_mapper.Map<TravelOrderApplicationModel>(data));
        }

        [HttpPost("batch")]
        [ProducesResponseType(typeof(ResponseModel<List<TravelOrderApplicationModel>>), 200)]
        public async Task<IActionResult> PostBatch([FromBody] List<CreateTravelOrderApplication> payload, CancellationToken token)
        {
            var results = new List<TravelOrderApplicationModel>();
            foreach (var item in payload)
            {
                var data = _mapper.Map<TravelOrderApplication>(item);
                data.ApprovalStatus = ApprovalStatus.Approved;
                await _service.AddAsync(data, token);
                results.Add(_mapper.Map<TravelOrderApplicationModel>(data));
            }
            return Ok(results);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ResponseModel<TravelOrderApplicationModel>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateTravelOrderApplication payload, CancellationToken token)
        {
            payload.Id = payload.Id == Guid.Empty ? id : payload.Id;
            await _service.UpdateAsync(payload, token);
            return Ok(_mapper.Map<TravelOrderApplicationModel>(payload));
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.Delete(id, token);
            return Ok();
        }
    }
}
