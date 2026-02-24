//using Asp.Versioning;
//using AutoMapper;
//using Hrms.Domain.Entities.HR;
//using Microsoft.AspNetCore.Mvc;

//namespace Hrms.Api.Controllers
//{
//    [Route("api/v{version:apiVersion}/[controller]")]
//    [ApiVersion("1.0")]
//    [ApiController]
//    public class BranchesController : ControllerBase
//    {
//        private readonly BranchService _service;
//        private readonly IMapper _mapper;
//        public BranchesController(BranchService service, IMapper mapper)
//        {
//            _service = service;
//            _mapper = mapper;
//        }

//        [HttpGet]
//        public async Task<IActionResult> Get(CancellationToken token)
//        {
//            var data = await _service.FindAllAsync(token);
//            return Ok(_mapper.Map<List<BranchModel>>(data));
//        }

//        [HttpGet("{id}")]
//        public async Task<IActionResult> Get(Guid id, CancellationToken token)
//        {
//            var data = await _service.FineOneAsync(id, token);
//            return Ok(_mapper.Map<BranchModel>(data));
//        }

//        [HttpPost]
//        public async Task<IActionResult> Post([FromBody] CreateBranch payload, CancellationToken token)
//        {
//            var data = _mapper.Map<Branch>(payload);
//            await _service.AddAsync(data, token);
//            var respModel = _mapper.Map<BranchModel>(data);
//            return Ok(respModel);
//        }

//        [HttpPut("{id}")]
//        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateBranch payload, CancellationToken token)
//        {
//            var data = _mapper.Map<Branch>(payload);
//            data.Id = id;
//            await _service.UpdateAsync(data, token);
//            return Ok(_mapper.Map<BranchModel>(data));
//        }

//        [HttpDelete("{id}")]
//        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
//        {
//            await _service.DeleteAsync(id, token);
//            return Ok();
//        }
//    }
//}
//using Asp.Versioning;
//using AutoMapper;
//using Hrms.Domain.Entities.HR;
//using Microsoft.AspNetCore.Mvc;

//namespace Hrms.Api.Controllers
//{
//    [Route("api/v{version:apiVersion}/[controller]")]
//    [ApiVersion("1.0")]
//    [ApiController]
//    public class BranchesController : ControllerBase
//    {
//        private readonly BranchService _service;
//        private readonly IMapper _mapper;
//        public BranchesController(BranchService service, IMapper mapper)
//        {
//            _service = service;
//            _mapper = mapper;
//        }

//        [HttpGet]
//        public async Task<IActionResult> Get(CancellationToken token)
//        {
//            var data = await _service.FindAllAsync(token);
//            return Ok(_mapper.Map<List<BranchModel>>(data));
//        }

//        [HttpGet("{id}")]
//        public async Task<IActionResult> Get(Guid id, CancellationToken token)
//        {
//            var data = await _service.FineOneAsync(id, token);
//            return Ok(_mapper.Map<BranchModel>(data));
//        }

//        [HttpPost]
//        public async Task<IActionResult> Post([FromBody] CreateBranch payload, CancellationToken token)
//        {
//            var data = _mapper.Map<Branch>(payload);
//            await _service.AddAsync(data, token);
//            var respModel = _mapper.Map<BranchModel>(data);
//            return Ok(respModel);
//        }

//        [HttpPut("{id}")]
//        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateBranch payload, CancellationToken token)
//        {
//            var data = _mapper.Map<Branch>(payload);
//            data.Id = id;
//            await _service.UpdateAsync(data, token);
//            return Ok(_mapper.Map<BranchModel>(data));
//        }

//        [HttpDelete("{id}")]
//        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
//        {
//            await _service.DeleteAsync(id, token);
//            return Ok();
//        }
//    }
//}
