using Asp.Versioning;
using AutoMapper;
using Hrms.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    public class LeaveApplicationsController : ControllerBase
    {
        private readonly LeaveApplicationService _service;
        private readonly IMapper _mapper;

        public LeaveApplicationsController(LeaveApplicationService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        [HttpGet("range")]
        public async Task<IActionResult> Range([FromQuery] DateEmployeeRequestPayload payload, CancellationToken token)
        {
            var data = await _service.FindAllAsync(token);
            return Ok(data);
        }

        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken token)
        {
            var data = await _service.FindAllAsync(token);
            return Ok(data);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(data);
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CreateLeaveApplication payload, CancellationToken token)
        {
            return Ok(await _service.AddAsync(payload, token));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateLeaveApplication payload, CancellationToken token)
        {
            var data = _mapper.Map<LeaveApplication>(payload);
            data.Id = id;
            //data.PayType = payload.IsPaid ? PayType.WithPay : PayType.WithoutPay;
            await _service.UpdateAsync(data, token);
            return Ok(_mapper.Map<LeaveApplicationModel>(data));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.Delete(id, token);
            await _service.CommitChangesAsync(token);
            return Ok();
        }
    }
}
