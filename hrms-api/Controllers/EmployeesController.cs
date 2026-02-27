using Asp.Versioning;
using AutoMapper;
using ClosedXML.Excel;
using Hrms.Domain.Entities.EmployeeEntities;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    public class EmployeesController : ControllerBase
    {
        private readonly EmployeeService _service;
        private readonly EmployeeImportService _employeeImportService;
        private readonly IMapper _mapper;

        public EmployeesController(EmployeeService service,
            EmployeeImportService employeeImportService,
            IMapper mapper)
        {
            _service = service;
            _employeeImportService = employeeImportService;
            _mapper = mapper;
        }

        [HttpGet("all")]
        public async Task<IActionResult> GetAll(CancellationToken token)
        {
            var data = await _service.GetAll(token);
            return Ok(data);
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] PaginationPayload payload, CancellationToken token)
        {
            var data = await _service.LoadAll(payload, token);
            return Ok(data);
        }

        [HttpGet("full")]
        public async Task<IActionResult> GetFull([FromQuery] PaginationPayload payload, CancellationToken token)
        {
            var data = await _service.LoadAllFullAsync(payload, token);
            return Ok(data);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var employee = await _service.FineOneAsync(id, token);
            var model = _mapper.Map<EmployeeModel>(employee);
            return Ok(model == null ? new EmployeeModel() : model);
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CreateEmployee payload, CancellationToken token)
        {
            var employee = _mapper.Map<Employee>(payload);
            await _service.AddAsync(employee, token);
            await _service.CommitChangesAsync(token);
            return Ok(employee);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateEmployee payload, CancellationToken token)
        {
            var employee = _mapper.Map<Employee>(payload);
            if (payload.Id == Guid.Empty) employee.Id = id;
            await _service.UpdateAsync(employee, token);
            return Ok(_mapper.Map<EmployeeModel>(employee));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.Delete(id, token);
            return Ok();
        }

        [HttpPost("upload-employees")]
        public async Task<IActionResult> Upload(IFormFile excelFile, CancellationToken token)
        {
            if (excelFile == null || excelFile.Length == 0)
                return BadRequest("Please select a file.");

            // IMPORTANT: Open the stream for the service to read
            using (var stream = excelFile.OpenReadStream())
            {
                await _employeeImportService.Upload(stream, token);
            }

            return Ok("success");
        }
    }
}

