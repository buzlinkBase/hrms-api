using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    // Read-only — the feature/action catalog is system-seeded (see PermissionCatalogSeederService),
    // never admin-typed free text, so there are deliberately no create/update/delete actions here.
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
    public class PermissionsController : ControllerBase
    {
        private readonly PermissionService _service;
        private readonly IMapper _mapper;

        public PermissionsController(PermissionService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResponseModel<List<PermissionModel>>), 200)]
        public async Task<IActionResult> Get(CancellationToken token)
        {
            var data = await _service.FindAllAsync(token);
            return Ok(_mapper.Map<List<PermissionModel>>(data));
        }
    }
}
