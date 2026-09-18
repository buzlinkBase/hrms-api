using Asp.Versioning;
using Hrms.Api.Documents;
using Hrms.Api.Filters;
using Hrms.Domain.Entities.EmployeeEntities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;

namespace Hrms.Api.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    [Authorize]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
    public class EmployeesController : ControllerBase
    {
        private readonly EmployeeService _service;
        private readonly EmployeeImportService _employeeImportService;
        private readonly EmployeeSeederService _seederService;
        private readonly TemplateDownloaderService _templateService;
        private readonly CompanyService _companyService;
        private readonly IWebHostEnvironment _hostEnvironment;
        private readonly IMapper _mapper;

        public EmployeesController(EmployeeService service,
            EmployeeImportService employeeImportService,
            EmployeeSeederService seederService,
            TemplateDownloaderService templateService,
            CompanyService companyService,
            IWebHostEnvironment hostEnvironment,
            IMapper mapper)
        {
            _service = service;
            _employeeImportService = employeeImportService;
            _seederService = seederService;
            _templateService = templateService;
            _companyService = companyService;
            _hostEnvironment = hostEnvironment;
            _mapper = mapper;
        }

        // Dev/test-only bulk fake-data seeding -- gated like any other Create/Delete action
        // rather than left open, even though it's not really a "Setup" screen.
        [HttpPost("seed/{count:int}")]
        [RequirePermission("Workforce Setup:Create")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Seed(int count, CancellationToken token)
        {
            var seeded = await _seederService.SeedAsync(count, token);
            return Ok(new { seeded });
        }

        [HttpDelete("seed")]
        [RequirePermission("Workforce Setup:Delete")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> RemoveSeeded(CancellationToken token)
        {
            var removed = await _seederService.RemoveSeededAsync(token);
            return Ok(new { removed });
        }

        [HttpGet("all")]
        [ProducesResponseType(typeof(ResponseModel<List<EmployeeModel>>), 200)]
        public async Task<IActionResult> GetAll([FromQuery] string? keyword, CancellationToken token)
        {
            var data = await _service.GetAll(keyword, token);
            return Ok(data);
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResponseModel<List<EmployeeModel>>), 200)]
        public async Task<IActionResult> Get([FromQuery] PaginationPayload payload, CancellationToken token)
        {
            var data = await _service.LoadAll(payload, token);
            return Ok(data);
        }

        [HttpGet("full")]
        [ProducesResponseType(typeof(ResponseModel<List<EmployeeFullModel>>), 200)]
        public async Task<IActionResult> GetFull([FromQuery] PaginationPayload payload, CancellationToken token)
        {
            var data = await _service.LoadAllFullAsync(payload, token);
            return Ok(data);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ResponseModel<EmployeeModel>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var employee = await _service.FineOneAsync(id, token);
            var model = _mapper.Map<EmployeeModel>(employee);
            return Ok(model == null ? new EmployeeModel() : model);
        }

        [HttpPost]
        [RequirePermission("Workforce Setup:Create")]
        [ProducesResponseType(typeof(ResponseModel<EmployeeModel>), 200)]
        public async Task<IActionResult> Post([FromBody] CreateEmployee payload, CancellationToken token)
        {
            var employee = _mapper.Map<Employee>(payload);
            await _service.AddAsync(employee, token);
            await _service.CommitChangesAsync(token);
            return Ok(_mapper.Map<EmployeeModel>(employee));
        }

        [HttpPut("{id}")]
        [RequirePermission("Workforce Setup:Edit")]
        [ProducesResponseType(typeof(ResponseModel<EmployeeModel>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateEmployee payload, CancellationToken token)
        {
            payload.Id = payload.Id == Guid.Empty ? id : payload.Id;
            await _service.UpdateAsync(payload, token);
            return Ok(_mapper.Map<EmployeeModel>(payload));
        }

        [HttpDelete("{id}")]
        [RequirePermission("Workforce Setup:Delete")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.Delete(id, token);
            return Ok();
        }

        [HttpPost("upload-employees")]
        [RequirePermission("Workforce Setup:Create")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> Upload(IFormFile excelFile, CancellationToken token)
        {
            if (excelFile == null || excelFile.Length == 0)
                return BadRequest("Please select a file.");
            using (var stream = excelFile.OpenReadStream())
            {
                await _employeeImportService.Upload(stream, token);
            }
            return Ok("success");
        }

        [HttpPost("upload-employees-preview")]
        [RequirePermission("Workforce Setup:Create")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(ResponseModel<List<EmployeeImportPreviewRow>>), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> PreviewUpload(IFormFile excelFile, CancellationToken token)
        {
            if (excelFile == null || excelFile.Length == 0)
                return BadRequest("Please select a file.");
            using var stream = excelFile.OpenReadStream();
            var preview = await _employeeImportService.PreviewAsync(stream, token);
            return Ok(preview);
        }

        [HttpPost("upload-employees-errors-export")]
        [RequirePermission("Workforce Setup:Create")]
        [ProducesResponseType(200)]
        public async Task<IActionResult> ExportErrorRows([FromBody] List<EmployeeImportPreviewRow> rows, CancellationToken token)
        {
            var dataStream = await _templateService.GetEmployeeTemplateWithErrors(rows, token);
            return File(
                dataStream,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "employee_import_corrections.xlsx"
            );
        }

        [HttpGet("export-template")]
        [ProducesResponseType(200)]
        public async Task<IActionResult> DownloadTemplate(CancellationToken token)
        {
            var dataStream = await _templateService.GetEmployeeTemplate(token);
            return File(
                dataStream,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "employees.xlsx"
            );
        }

        [HttpGet("{id}/full")]
        [ProducesResponseType(typeof(ResponseModel<EmployeeFullModel>), 200)]
        public async Task<IActionResult> GetFullById(Guid id, CancellationToken token)
        {
            var data = await _service.GetFullByIdAsync(id, token);
            return Ok(data);
        }

        [HttpGet("{id}/print-201")]
        public async Task<IActionResult> Print201(Guid id, CancellationToken token)
        {
            var employee = await _service.GetFullByIdAsync(id, token);
            if (employee == null) return NotFound();
            var company = await _companyService.FineOneAsync(token);
            var document = new Employee201Document(employee, company);
            var bytes = document.GeneratePdf();
            return File(bytes, "application/pdf", $"201-{employee.EmployeeNo}.pdf");
        }

        [HttpGet("filter")]
        [ProducesResponseType(typeof(ResponseModel<List<EmployeeFilterResponseModel>>), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> GetManualEntry([FromQuery] EmployeeFilter filter, CancellationToken ct)
        {
            var result = await _service.Filter(filter, ct);
            return Ok(result);
        } 
    }
}
