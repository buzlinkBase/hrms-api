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
    public class UserRolesController : ControllerBase
    {
        private readonly UserRoleService _service;
        private readonly IMapper _mapper;

        public UserRolesController(UserRoleService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        [HttpGet("{userId}")]
        [ProducesResponseType(typeof(ResponseModel<List<RoleModel>>), 200)]
        public async Task<IActionResult> Get(Guid userId, CancellationToken token)
        {
            var data = await _service.GetRolesForUserAsync(userId, token);
            return Ok(_mapper.Map<List<RoleModel>>(data));
        }

        [HttpPut("{userId}")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Put(Guid userId, [FromBody] ReplaceUserRoles payload, CancellationToken token)
        {
            await _service.ReplaceRolesAsync(userId, payload.RoleIds, token);
            return Ok();
        }
    }
}
