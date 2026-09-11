using Asp.Versioning;
using Hrms.Api.Documents;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;

namespace Hrms.Api.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
    public class PayrollReportsController : ControllerBase
    {
        private readonly SSSContributionService _sssService;
        private readonly PHICContributionService _phicService;
        private readonly HDMFContributionService _hdmfService;
        private readonly TaxContributionService _taxService;
        private readonly PayrollReportService _reportService;
        private readonly CompanyService _companyService;
        private readonly IWebHostEnvironment _environment;

        public PayrollReportsController(
            SSSContributionService sssService,
            PHICContributionService phicService,
            HDMFContributionService hdmfService,
            TaxContributionService taxService,
            PayrollReportService reportService,
            CompanyService companyService,
            IWebHostEnvironment environment)
        {
            _sssService = sssService;
            _phicService = phicService;
            _hdmfService = hdmfService;
            _taxService = taxService;
            _reportService = reportService;
            _companyService = companyService;
            _environment = environment;
        }

        [HttpGet("sss-remittance")]
        public async Task<IActionResult> SssRemittance([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken token)
        {
            var data = await _sssService.GetRemittanceReportAsync(DateOnly.FromDateTime(from), DateOnly.FromDateTime(to), token);
            return Ok(new { data, total = data.Count });
        }

        [HttpGet("philhealth-remittance")]
        public async Task<IActionResult> PhilHealthRemittance([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken token)
        {
            var data = await _phicService.GetRemittanceReportAsync(DateOnly.FromDateTime(from), DateOnly.FromDateTime(to), token);
            return Ok(new { data, total = data.Count });
        }

        [HttpGet("pagibig-remittance")]
        public async Task<IActionResult> PagIbigRemittance([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken token)
        {
            var data = await _hdmfService.GetRemittanceReportAsync(DateOnly.FromDateTime(from), DateOnly.FromDateTime(to), token);
            return Ok(new { data, total = data.Count });
        }

        [HttpGet("wtax-remittance")]
        public async Task<IActionResult> WTaxRemittance([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken token)
        {
            var data = await _taxService.GetRemittanceReportAsync(DateOnly.FromDateTime(from), DateOnly.FromDateTime(to), token);
            return Ok(new { data, total = data.Count });
        }

        [HttpGet("bank-disbursement")]
        public async Task<IActionResult> BankDisbursement([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken token)
        {
            var data = await _reportService.GetBankDisbursementAsync(DateOnly.FromDateTime(from), DateOnly.FromDateTime(to), token);
            return Ok(new { data, total = data.Count });
        }

        [HttpGet("loan-ledger")]
        public async Task<IActionResult> LoanLedger([FromQuery] DateTime? asOf, CancellationToken token)
        {
            var asOfDate = asOf.HasValue ? DateOnly.FromDateTime(asOf.Value) : DateOnly.FromDateTime(DateTime.UtcNow);
            var data = await _reportService.GetLoanLedgerAsync(asOfDate, token);
            return Ok(new { data, total = data.Count });
        }

        [HttpGet("leave-ledger")]
        public async Task<IActionResult> LeaveLedger([FromQuery] int year, CancellationToken token)
        {
            var data = await _reportService.GetLeaveLedgerAsync(year, token);
            return Ok(new { data, total = data.Count });
        }

        [HttpGet("reimbursement-list")]
        public async Task<IActionResult> ReimbursementList([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken token)
        {
            var data = await _reportService.GetReimbursementListAsync(DateOnly.FromDateTime(from), DateOnly.FromDateTime(to), token);
            return Ok(new { data, total = data.Count });
        }

        [HttpGet("cost-summary")]
        public async Task<IActionResult> CostSummary(
            [FromQuery] DateTime from,
            [FromQuery] DateTime to,
            [FromQuery] string groupBy = "department",
            CancellationToken token = default)
        {
            var data = await _reportService.GetCostSummaryAsync(DateOnly.FromDateTime(from), DateOnly.FromDateTime(to), groupBy, token);
            return Ok(new { data, total = data.Count });
        }

        [HttpGet("ytd-summary")]
        public async Task<IActionResult> YtdSummary([FromQuery] int year, [FromQuery] Guid? employeeId, CancellationToken token)
        {
            var data = await _reportService.GetYtdSummaryAsync(year, employeeId, token);
            return Ok(new { data, total = data.Count });
        }

        [HttpGet("13th-month-pay")]
        public async Task<IActionResult> ThirteenthMonthPay([FromQuery] int year, CancellationToken token)
        {
            var data = await _reportService.GetThirteenthMonthAsync(year, token);
            return Ok(new { data, total = data.Count });
        }

        [HttpGet("13th-month-pay/print")]
        public async Task<IActionResult> ThirteenthMonthPayPrint([FromQuery] int year, CancellationToken token)
        {
            var data = await _reportService.GetThirteenthMonthAsync(year, token);
            var company = await _companyService.FineOneAsync(token);
            var document = new ThirteenthMonthPayListDocument(data, year, company);
            var bytes = document.GeneratePdf();
            return File(bytes, "application/pdf", $"13th-month-pay-{year}.pdf");
        }

        // ── BIR 1601-C ────────────────────────────────────────────────────────────────

        [HttpGet("1601c")]
        public async Task<IActionResult> MonthlyRemittanceReturn(
            [FromQuery] DateTime from, [FromQuery] DateTime to,
            [FromQuery] bool amendedReturn, CancellationToken token)
        {
            var (summary, employees) = await _reportService.GetMonthlyRemittanceReturnAsync(
                DateOnly.FromDateTime(from), DateOnly.FromDateTime(to), amendedReturn, token);
            return Ok(new { data = employees, summary, total = employees.Count });
        }

        [HttpGet("1601c/print")]
        public async Task<IActionResult> MonthlyRemittanceReturnPrint(
            [FromQuery] DateTime from, [FromQuery] DateTime to,
            [FromQuery] bool amendedReturn,
            [FromQuery] bool debug,
            CancellationToken token)
        {
            var fromDate = DateOnly.FromDateTime(from);
            var toDate = DateOnly.FromDateTime(to);
            var (summary, _) = await _reportService.GetMonthlyRemittanceReturnAsync(fromDate, toDate, amendedReturn, token);
            var company = await _companyService.FineOneAsync(token);
            var bytes = new MonthlyRemittanceReturnOverlayDocument(summary, company, _environment.WebRootPath)
                .Generate(debug && _environment.IsDevelopment());
            return File(bytes, "application/pdf", $"bir-1601c-{fromDate:yyyyMM}.pdf");
        }

        // ── BIR Alphalist ─────────────────────────────────────────────────────────────

        [HttpGet("alphalist")]
        public async Task<IActionResult> Alphalist([FromQuery] int year, CancellationToken token)
        {
            var data = await _reportService.GetAlphalistAsync(year, token);
            return Ok(new { data, total = data.Count });
        }

        [HttpGet("alphalist/print")]
        public async Task<IActionResult> AlphalistPrint([FromQuery] int year, CancellationToken token)
        {
            var data = await _reportService.GetAlphalistAsync(year, token);
            var company = await _companyService.FineOneAsync(token);
            var bytes = new AlphalistPreviewDocument(data, year, company).GeneratePdf();
            return File(bytes, "application/pdf", $"bir-alphalist-{year}.pdf");
        }

        [HttpGet("alphalist/export")]
        public async Task<IActionResult> AlphalistExport([FromQuery] int year, CancellationToken token)
        {
            var data = await _reportService.GetAlphalistAsync(year, token);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("TIN,Employee Name,Employee No.,Gross Compensation,Non-Taxable,Taxable,13th Month Pay,SSS,PhilHealth,Pag-IBIG,Tax Withheld");
            foreach (var r in data)
            {
                sb.AppendLine(string.Join(",",
                    r.TIN, r.FullName, r.EmployeeNo,
                    r.GrossCompensation.ToString("F2"), r.NonTaxableCompensation.ToString("F2"),
                    r.TaxableCompensation.ToString("F2"), r.ThirteenthMonthPay.ToString("F2"),
                    r.TotalSSS.ToString("F2"), r.TotalPhilHealth.ToString("F2"), r.TotalPagIbig.ToString("F2"),
                    r.TotalTaxWithheld.ToString("F2")));
            }
            return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"bir-alphalist-{year}.csv");
        }

        // ── BIR 2316 ──────────────────────────────────────────────────────────────────

        [HttpGet("2316")]
        public async Task<IActionResult> Bir2316([FromQuery] Guid employeeId, [FromQuery] int year, CancellationToken token)
        {
            // No posted payroll for this employee/year is a normal, expected result here
            // (not every employee has a 2316 for every year) — matches every other report
            // action in this controller returning 200 with an empty array rather than a 404,
            // so the frontend's generic error-notification interceptor doesn't fire for it.
            var data = await _reportService.Get2316DataAsync(employeeId, year, token);
            var results = data == null ? Array.Empty<Bir2316Model>() : new[] { data };
            return Ok(new { data = results, total = results.Length });
        }

        [HttpGet("2316/print")]
        public async Task<IActionResult> Bir2316Print(
            [FromQuery] Guid employeeId, [FromQuery] int year, [FromQuery] bool debug, CancellationToken token)
        {
            var data = await _reportService.Get2316DataAsync(employeeId, year, token);
            if (data == null) return NotFound();
            var company = await _companyService.FineOneAsync(token);
            var bytes = new Bir2316OverlayDocument(data, company, _environment.WebRootPath)
                .Generate(debug && _environment.IsDevelopment());
            return File(bytes, "application/pdf", $"bir-2316-{data.EmployeeNo}-{year}.pdf");
        }

        // ── SSS R3 ────────────────────────────────────────────────────────────────────

        [HttpGet("sss-r3/print")]
        public async Task<IActionResult> SssR3Print([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken token)
        {
            var fromDate = DateOnly.FromDateTime(from);
            var toDate = DateOnly.FromDateTime(to);
            var data = await _sssService.GetRemittanceReportAsync(fromDate, toDate, token);
            var company = await _companyService.FineOneAsync(token);
            var bytes = new ContributionFilePreviewDocument(data, "SSS Contribution Collection List (R3)", "SS Number", fromDate, toDate, company).GeneratePdf();
            return File(bytes, "application/pdf", $"sss-r3-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.pdf");
        }

        [HttpGet("sss-r3/export")]
        public async Task<IActionResult> SssR3Export([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken token)
        {
            var fromDate = DateOnly.FromDateTime(from);
            var toDate = DateOnly.FromDateTime(to);
            var bytes = await _sssService.GenerateR3FileAsync(fromDate, toDate, token);
            return File(bytes, "text/csv", $"sss-r3-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.csv");
        }

        // ── PhilHealth EPRS ───────────────────────────────────────────────────────────

        [HttpGet("philhealth-eprs/print")]
        public async Task<IActionResult> PhilHealthEprsPrint([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken token)
        {
            var fromDate = DateOnly.FromDateTime(from);
            var toDate = DateOnly.FromDateTime(to);
            var data = await _phicService.GetRemittanceReportAsync(fromDate, toDate, token);
            var company = await _companyService.FineOneAsync(token);
            var bytes = new ContributionFilePreviewDocument(data, "PhilHealth Electronic Premium Remittance (EPRS)", "PhilHealth No.", fromDate, toDate, company).GeneratePdf();
            return File(bytes, "application/pdf", $"philhealth-eprs-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.pdf");
        }

        [HttpGet("philhealth-eprs/export")]
        public async Task<IActionResult> PhilHealthEprsExport([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken token)
        {
            var fromDate = DateOnly.FromDateTime(from);
            var toDate = DateOnly.FromDateTime(to);
            var bytes = await _phicService.GenerateEprsFileAsync(fromDate, toDate, token);
            return File(bytes, "text/csv", $"philhealth-eprs-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.csv");
        }

        // ── Pag-IBIG MCRF ─────────────────────────────────────────────────────────────

        [HttpGet("pagibig-mcrf/print")]
        public async Task<IActionResult> PagIbigMcrfPrint([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken token)
        {
            var fromDate = DateOnly.FromDateTime(from);
            var toDate = DateOnly.FromDateTime(to);
            var data = await _hdmfService.GetRemittanceReportAsync(fromDate, toDate, token);
            var company = await _companyService.FineOneAsync(token);
            var bytes = new ContributionFilePreviewDocument(data, "Pag-IBIG Member's Contribution Remittance Form (MCRF)", "Pag-IBIG MID", fromDate, toDate, company).GeneratePdf();
            return File(bytes, "application/pdf", $"pagibig-mcrf-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.pdf");
        }

        [HttpGet("pagibig-mcrf/export")]
        public async Task<IActionResult> PagIbigMcrfExport([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken token)
        {
            var fromDate = DateOnly.FromDateTime(from);
            var toDate = DateOnly.FromDateTime(to);
            var bytes = await _hdmfService.GenerateMcrfFileAsync(fromDate, toDate, token);
            return File(bytes, "text/csv", $"pagibig-mcrf-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.csv");
        }
    }
}
