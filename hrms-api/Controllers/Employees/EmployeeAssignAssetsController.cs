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
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

        private readonly EmployeeAssignAssetService _service;
        private readonly IMapper _mapper;
        private readonly ICacheService _cache;
        private readonly ITenantProvider _tenantProvider;

        public EmployeeAssignAssetsController(EmployeeAssignAssetService service,
            IMapper mapper,
            ICacheService cache,
            ITenantProvider tenantProvider)
        {
            _service = service;
            _mapper = mapper;
            _cache = cache;
            _tenantProvider = tenantProvider;
        }

        // Tenant-scoped keys: a bare id key let one company's cached row be served to another
        // (the cache read skips the tenant query filter).
        private string EmployeeKey(Guid employeeId) => $"assign-assets:{_tenantProvider.TenantId}:employee:{employeeId}";
        private string ItemKey(Guid id) => $"assign-assets:{_tenantProvider.TenantId}:item:{id}";

        [HttpGet("employee")]
        [ProducesResponseType(typeof(ResponseModel<List<AssignAssetModel>>), 200)]
        public async Task<IActionResult> GetAll([FromQuery] Guid emp_id, CancellationToken token)
        {
            var key = EmployeeKey(emp_id);
            var cache = await _cache.GetAsync<List<AssignAssetModel>>(key);
            if (cache != null) return Ok(cache);

            // Cache the mapped model, never the entities -- serializing lazy-loading proxies walks
            // (and loads) their whole navigation graph.
            var models = _mapper.Map<List<AssignAssetModel>>(await _service.FindAllAsync(emp_id, token));
            await _cache.SetAsync(key, models, CacheTtl);
            return Ok(models);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ResponseModel<AssignAssetModel>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var key = ItemKey(id);
            var cache = await _cache.GetAsync<AssignAssetModel>(key);
            if (cache != null) return Ok(cache);

            var model = _mapper.Map<AssignAssetModel>(await _service.FineOneAsync(id, token));
            if (model != null) await _cache.SetAsync(key, model, CacheTtl);
            return Ok(model);
        }

        [HttpPost]
        [ProducesResponseType(typeof(ResponseModel<AssignAssetModel>), 200)]
        public async Task<IActionResult> Post([FromBody] CreateAssignAsset payload, CancellationToken token)
        {
            var data = _mapper.Map<AssignAsset>(payload);
            await _service.AddAsync(data, token);
            await _cache.RemoveAsync(EmployeeKey(payload.EmployeeId));
            var respModel = _mapper.Map<AssignAssetModel>(data);
            return Ok(respModel);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ResponseModel<AssignAssetModel>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateAssignAsset payload, CancellationToken token)
        {
            payload.Id = payload.Id == Guid.Empty ? id : payload.Id;
            await _service.UpdateAsync(payload, token);
            await _cache.RemoveAsync(ItemKey(payload.Id));
            await _cache.RemoveAsync(EmployeeKey(payload.EmployeeId));
            return Ok(_mapper.Map<AssignAssetModel>(payload));
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.DeleteAsync(id, token);
            // The employee list key isn't known here; its 30s TTL covers it.
            await _cache.RemoveAsync(ItemKey(id));
            return Ok();
        }
    }
}