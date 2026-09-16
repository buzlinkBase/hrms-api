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
    public class BranchesController : ControllerBase
    {
        private readonly BranchService _service;
        private readonly IMapper _mapper;

        public BranchesController(BranchService service,
            BranchService branchService,
            IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        [HttpPost]
        [RequirePermission("Organization Setup:Create")]
        [ProducesResponseType(typeof(ResponseModel<BranchModel>), 200)]
        public async Task<IActionResult> Post([FromBody] CreateBranch payload, CancellationToken token)
        {
            var data = _mapper.Map<Branch>(payload);
            await _service.AddAsync(data, token);
            var respModel = _mapper.Map<DepartmentModel>(data);
            return Ok(respModel);
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResponseModel<List<BranchModel>>), 200)]
        public async Task<IActionResult> Get(CancellationToken token)
        {
            var data = await _service.FindAllAsync(token);
            return Ok(_mapper.Map<List<BranchModel>>(data));
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ResponseModel<BranchModel>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<BranchModel>(data));
        }

        [HttpPut("{id}")]
        [RequirePermission("Organization Setup:Edit")]
        [ProducesResponseType(typeof(ResponseModel<BranchModel>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateBranch payload, CancellationToken token)
        {
            payload.Id = payload.Id == Guid.Empty ? id : payload.Id;
            await _service.UpdateAsync(payload, token);
            return Ok(_mapper.Map<BranchModel>(payload));
        }

        [HttpDelete("{id}")]
        [RequirePermission("Organization Setup:Delete")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.Delete(id, token);
            return Ok();
        }
    }
}
