using Asp.Versioning;
using Hrms.Domain.Entities;
using Microsoft.AspNetCore.Mvc;



namespace Hrms.Api.Controllers;

[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[ApiController]
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
    public async Task<IActionResult> Get(CancellationToken token)
    {
        var data = await _service.FindAllAsync(token);
        return Ok(data);
    }

    [HttpGet("range")]
    public async Task<IActionResult> Get([FromQuery] DateRequestPayload payload, CancellationToken token)
    {
        var data = await _service.FindRange(payload, token);
        return Ok(data);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken token)
    {
        var data = await _service.FineOneAsync(id, token);
        return Ok(_mapper.Map<WorkSchedulePlanModel>(data));
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] CreateWorkRotationPlan payload, CancellationToken token)
    {
        var data = _mapper.Map<WorkSchedulePlan>(payload);
        await _service.AddAsync(data, token);
        var respModel = _mapper.Map<WorkSchedulePlanModel>(data);
        return Ok(respModel);
    }
    [HttpPost("batch")]
    public async Task<IActionResult> PostBatch([FromBody] List<CreateWorkRotationPlan> payload, CancellationToken token)
    {
        var data = _mapper.Map<List<WorkSchedulePlan>>(payload);
        await _service.AddRange(data, token);
        var respModel = _mapper.Map<List<WorkSchedulePlanModel>>(data);
        return Ok(respModel);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Put(Guid id, [FromBody] UpdateWorkSchedulePlan payload, CancellationToken token)
    {
        var data = _mapper.Map<WorkSchedulePlan>(payload);
        data.Id = id;
        await _service.UpdateAsync(data, token);
        return Ok(_mapper.Map<WorkSchedulePlanModel>(data));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken token)
    {
        await _service.DeleteAsync(id, token);
        return Ok();
    }
}
