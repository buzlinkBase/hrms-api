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
    public class DeductionTypesController : ControllerBase
    {
        private readonly DeductionTypeService _service;
        private readonly IMapper _mapper;
        public DeductionTypesController(DeductionTypeService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResponseModel<List<DeductionTypeModel>>), 200)]
        public async Task<IActionResult> Get(CancellationToken token)
        {
            var data = await _service.FindAllAsync(token);
            return Ok(_mapper.Map<List<DeductionTypeModel>>(data));
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ResponseModel<DeductionTypeModel>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<DeductionTypeModel>(data));
        }

        [HttpPost]
        [ProducesResponseType(typeof(ResponseModel<DeductionTypeModel>), 200)]
        public async Task<IActionResult> Post([FromBody] CreateDeductionType payload, CancellationToken token)
        {
            var data = _mapper.Map<DeductionType>(payload);
            await _service.AddAsync(data, token);
            var respModel = _mapper.Map<DeductionTypeModel>(data);
            return Ok(respModel);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ResponseModel<DeductionTypeModel>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateDeductionType payload, CancellationToken token)
        {
            var data = _mapper.Map<DeductionType>(payload);
            data.Id = id;
            await _service.UpdateAsync(data, token);
            return Ok(_mapper.Map<DeductionTypeModel>(data));
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.DeleteAsync(id, token);
            return Ok();
        }
    }
}
