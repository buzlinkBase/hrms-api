using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
    public class RolesController : ControllerBase
    {
        private readonly RoleService _service;
        private readonly IMapper _mapper;

        public RolesController(RoleService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResponseModel<List<RoleModel>>), 200)]
        public async Task<IActionResult> Get(CancellationToken token)
        {
            var data = await _service.FindAllAsync(token);
            return Ok(_mapper.Map<List<RoleModel>>(data));
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ResponseModel<RoleModel>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FindOneWithPermissionsAsync(id, token);
            if (data == null) return NotFound();
            return Ok(_mapper.Map<RoleModel>(data));
        }

        [HttpPost]
        [ProducesResponseType(typeof(ResponseModel<RoleModel>), 200)]
        public async Task<IActionResult> Post([FromBody] CreateRole payload, CancellationToken token)
        {
            var data = await _service.AddAsync(payload, token);
            return Ok(_mapper.Map<RoleModel>(data));
        }

        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ResponseModel<RoleModel>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateRole payload, CancellationToken token)
        {
            payload.Id = id;
            var data = await _service.UpdateAsync(payload, token);
            return Ok(_mapper.Map<RoleModel>(data));
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.DeleteAsync(id, token);
            return Ok();
        }

        [HttpPut("{id}/permissions")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> SetPermissions(Guid id, [FromBody] SetRolePermissions payload, CancellationToken token)
        {
            await _service.SetPermissionsAsync(id, payload.PermissionIds, token);
            return Ok();
        }
    }
}
