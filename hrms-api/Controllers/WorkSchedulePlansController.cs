using Asp.Versioning;
using Hrms.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers;

[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[ApiController]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
public class WorkSchedulePlansController : ControllerBase
{
    private readonly WorkSchedulePlanService _service;
    private readonly IMapper _mapper;
    public WorkSchedulePlansController(WorkSchedulePlanService service, IMapper mapper)
    {
        _service = service;
        _mapper = mapper;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ResponseModel<List<WorkSchedulePlanModel>>), 200)]
    public async Task<IActionResult> Get([FromQuery] WorkRotationPlanFilter filter, CancellationToken token)
    {
        var data = await _service.FindAllAsync(filter, token);
        return Ok(data);
    }

    //[HttpGet("range")]
    //[ProducesResponseType(typeof(ResponseModel<object>), 200)]
    //public async Task<IActionResult> Get([FromQuery] DateRequestPayload payload, CancellationToken token)
    //{
    //    var data = await _service.FindRange(payload, token);
    //    return Ok(data);
    //}
    //[HttpGet("{id}")]
    //[ProducesResponseType(typeof(ResponseModel<WorkSchedulePlanModel>), 200)]
    //public async Task<IActionResult> Get(Guid id, CancellationToken token)
    //{
    //    var data = await _service.FineOneAsync(id, token);
    //    return Ok(_mapper.Map<WorkSchedulePlanModel>(data));
    //}
    //[HttpPost]
    //[ProducesResponseType(typeof(ResponseModel<WorkSchedulePlanModel>), 200)]
    //public async Task<IActionResult> Post([FromBody] CreateWorkRotationPlan payload, CancellationToken token)
    //{
    //    var data = _mapper.Map<WorkSchedulePlan>(payload);
    //    await _service.AddAsync(data, token);
    //    var respModel = _mapper.Map<WorkSchedulePlanModel>(data);
    //    return Ok(respModel);
    //}

    [HttpPost("batch")]
    [ProducesResponseType(typeof(ResponseModel<List<WorkSchedulePlanModel>>), 200)]
    public async Task<IActionResult> PostBatch([FromBody] CreateWorkRotationPlanBatch payload, CancellationToken token)
    {
        var data = new List<WorkSchedulePlan>();
        foreach (var employeeId in payload.EmployeeIds)
        {
            foreach (var payrollDate in payload.PayrollDates)
            {
                data.Add(new WorkSchedulePlan
                {
                    EmployeeId = employeeId,
                    PayrollDate = payrollDate,
                    TimeShiftId = payload.TimeShiftId,
                });
            }
        }
        await _service.AddRange(data, token);
        var respModel = _mapper.Map<List<WorkSchedulePlanModel>>(data);
        return Ok(respModel);
    }

    //[HttpPut("{id}")]
    //[ProducesResponseType(typeof(ResponseModel<WorkSchedulePlanModel>), 200)]
    //public async Task<IActionResult> Put(Guid id, [FromBody] UpdateWorkSchedulePlan payload, CancellationToken token)
    //{
    //    payload.Id = payload.Id == Guid.Empty ? id : payload.Id;
    //    await _service.UpdateAsync(payload, token);
    //    return Ok(_mapper.Map<WorkSchedulePlanModel>(payload));
    //}

    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ResponseModel<string>), 200)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken token)
    {
        await _service.DeleteAsync(id, token);
        return Ok("Success");
    }

    [HttpDelete("batch")]
    [ProducesResponseType(typeof(ResponseModel<string>), 200)]
    public async Task<IActionResult> DeleteBatch([FromQuery] string batchCode, CancellationToken token)
    {
        await _service.DeleteBatchAsync(batchCode, token);
        return Ok("Success");
    }
}
