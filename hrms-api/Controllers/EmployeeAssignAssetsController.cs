using Asp.Versioning;
using AutoMapper;
using Hrms.Domain.Entities.EmployeeEntities;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
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
        public async Task<IActionResult> Post([FromBody] CreateAssignAsset payload, CancellationToken token)
        {
            var data = _mapper.Map<AssignAsset>(payload);
            await _service.AddAsync(data, token);
            var respModel = _mapper.Map<AssignAssetModel>(data);
            return Ok(respModel);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateAssignAsset payload, CancellationToken token)
        {
            var data = _mapper.Map<AssignAsset>(payload);
            data.Id = id;
            await _service.UpdateAsync(data, token);
            return Ok(_mapper.Map<AssignAssetModel>(data));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.DeleteAsync(id, token);
            return Ok();
        }
    }
}
