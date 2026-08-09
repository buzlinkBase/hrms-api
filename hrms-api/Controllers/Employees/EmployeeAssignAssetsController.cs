using Asp.Versioning;
using Hrms.Domain.Entities.EmployeeEntities;
using Microsoft.AspNetCore.Mvc;
using Onepunch.Common.Lib.Cache;

namespace Hrms.Api.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
    public class EmployeeAssignAssetsController : ControllerBase
    {
        private readonly EmployeeAssignAssetService _service;
        private readonly IMapper _mapper;
        private readonly ICacheService _cache;

        public EmployeeAssignAssetsController(EmployeeAssignAssetService service,
            IMapper mapper,
            ICacheService cache)
        {
            _service = service;
            _mapper = mapper;
            _cache = cache;
        }

        [HttpGet("employee")]
        [ProducesResponseType(typeof(ResponseModel<List<AssignAssetModel>>), 200)]
        public async Task<IActionResult> GetAll([FromQuery] Guid emp_id, CancellationToken token)
        {
            var key = emp_id.ToString();
            var cache = await _cache.GetAsync<List<AssignAssetModel>>(key);
            if (cache != null) return Ok(cache);

            var data = await _service.FindAllAsync(emp_id, token);
            await _cache.SetAsync(key, data, TimeSpan.FromSeconds(30));

            return Ok(_mapper.Map<List<AssignAssetModel>>(data));
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ResponseModel<AssignAssetModel>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var key = id.ToString();
            var cache = await _cache.GetAsync<AssignAssetModel>(key);
            if (cache != null) return Ok(cache);
            var data = await _service.FineOneAsync(id, token);
            await _cache.SetAsync(key, data, TimeSpan.FromSeconds(30));
            return Ok(_mapper.Map<AssignAssetModel>(data));
        }

        [HttpPost]
        [ProducesResponseType(typeof(ResponseModel<AssignAssetModel>), 200)]
        public async Task<IActionResult> Post([FromBody] CreateAssignAsset payload, CancellationToken token)
        {
            var data = _mapper.Map<AssignAsset>(payload);
            await _service.AddAsync(data, token);
            var respModel = _mapper.Map<AssignAssetModel>(data);
            return Ok(respModel);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ResponseModel<AssignAssetModel>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateAssignAsset payload, CancellationToken token)
        {
            payload.Id = payload.Id == Guid.Empty ? id : payload.Id;
            await _service.UpdateAsync(payload, token);
            return Ok(_mapper.Map<AssignAssetModel>(payload));
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
