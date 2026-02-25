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
        private readonly IMapper _mapper;

        public EmployeesController(EmployeeService service, IMapper mapper)
        {
            _service = service;
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
            var respModel = _mapper.Map<EmployeeModel>(employee);
            return Ok(respModel);
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
            await _service.Delete(id,token);
            return Ok();
        }

        [HttpPost("upload-employees")]
        public async Task<IActionResult> Upload(IFormFile excelFile, CancellationToken token)
        {
            if (excelFile == null || excelFile.Length == 0)
                return BadRequest("Please select a file.");


            var list = new List<UploadEmployeeDto>();
            using (var stream = new MemoryStream())
            {
                await excelFile.CopyToAsync(stream, token);
                using (var workbook = new XLWorkbook(stream))
                {
                    var worksheet = workbook.Worksheet(1);
                    // Project rows to your model, skipping the header (row 1)
                    list = worksheet.RangeUsed().RowsUsed()
                        .Skip(1)
                        .Select(row => new  UploadEmployeeDto 
                        {
                            FirstName = row.Cell(1).GetValue<string>(),
                            Email = row.Cell(2).GetValue<string>()
                        })
                        .ToList();
                }
            }

            return Ok(list);
        }
    }
}
public record struct ShiftKey(string ShiftName, TimeSpan am, TimeSpan pm, TimeSpan? l1, TimeSpan? l2);

