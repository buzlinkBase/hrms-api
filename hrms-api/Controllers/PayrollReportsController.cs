using Onepunch.Common.Lib.Subscriptions;
using Hrms.Api.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Asp.Versioning;
using Hrms.Api.Documents;
using Hrms.Api.Filters;
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

        [RequireFeature(EntitlementKeys.ReportsStatutory)]
        [CompensationData]
        [HttpGet("sss-remittance")]
        [RequirePermission("Government Statutory Reports:View")]
        public async Task<IActionResult> SssRemittance([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken token)
        {
            var data = await _sssService.GetRemittanceReportAsync(DateOnly.FromDateTime(from), DateOnly.FromDateTime(to), token);
            return Ok(new { data, total = data.Count });
        }

        [RequireFeature(EntitlementKeys.ReportsStatutory)]
        [CompensationData]
        [HttpGet("philhealth-remittance")]
        [RequirePermission("Government Statutory Reports:View")]
        public async Task<IActionResult> PhilHealthRemittance([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken token)
        {
            var data = await _phicService.GetRemittanceReportAsync(DateOnly.FromDateTime(from), DateOnly.FromDateTime(to), token);
            return Ok(new { data, total = data.Count });
        }

        [RequireFeature(EntitlementKeys.ReportsStatutory)]
        [CompensationData]
        [HttpGet("pagibig-remittance")]
        [RequirePermission("Government Statutory Reports:View")]
        public async Task<IActionResult> PagIbigRemittance([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken token)
        {
            var data = await _hdmfService.GetRemittanceReportAsync(DateOnly.FromDateTime(from), DateOnly.FromDateTime(to), token);
            return Ok(new { data, total = data.Count });
        }

        [RequireFeature(EntitlementKeys.ReportsStatutory)]
        [CompensationData]
        [HttpGet("wtax-remittance")]
        [RequirePermission("Government Statutory Reports:View")]
        public async Task<IActionResult> WTaxRemittance([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken token)
        {
            var data = await _taxService.GetRemittanceReportAsync(DateOnly.FromDateTime(from), DateOnly.FromDateTime(to), token);
            return Ok(new { data, total = data.Count });
        }

        [RequireFeature(EntitlementKeys.PayrollCore)]
        [CompensationData]
        [HttpGet("bank-disbursement")]
        [RequirePermission("Payroll Reports:View")]
        public async Task<IActionResult> BankDisbursement([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken token)
        {
            var data = await _reportService.GetBankDisbursementAsync(DateOnly.FromDateTime(from), DateOnly.FromDateTime(to), token);
            return Ok(new { data, total = data.Count });
        }

        [RequireFeature(EntitlementKeys.PayrollLoansIncome)]
        [EntriesData]
        [HttpGet("deduction-ledger")]
        [RequirePermission("Payroll Reports:View")]
        public async Task<IActionResult> DeductionLedger([FromQuery] DateTime? asOf, CancellationToken token)
        {
            var asOfDate = asOf.HasValue ? DateOnly.FromDateTime(asOf.Value) : DateOnly.FromDateTime(DateTime.UtcNow);
            var data = await _reportService.GetDeductionLedgerAsync(asOfDate, token);
            return Ok(new { data, total = data.Count });
        }

        [RequireFeature(EntitlementKeys.PayrollLoansIncome)]
        [EntriesData]
        [HttpGet("cash-bond")]
        [RequirePermission("Payroll Reports:View")]
        public async Task<IActionResult> CashBond([FromQuery] DateTime? asOf, CancellationToken token)
        {
            var asOfDate = asOf.HasValue ? DateOnly.FromDateTime(asOf.Value) : DateOnly.FromDateTime(DateTime.UtcNow);
            var data = await _reportService.GetCashBondReportAsync(asOfDate, token);
            return Ok(new { data, total = data.Count });
        }

        [RequireFeature(EntitlementKeys.PayrollLoansIncome)]
        [EntriesData]
        [HttpGet("cash-bond/{employeeId:guid}/entries")]
        [RequirePermission("Payroll Reports:View")]
        public async Task<IActionResult> CashBondEntries(Guid employeeId, [FromQuery] DateTime? asOf, CancellationToken token)
        {
            var data = await _reportService.GetCashBondEntriesAsync(employeeId, AsOfOrToday(asOf), token);
            return Ok(new { data, total = data.Count });
        }

        [RequireFeature(EntitlementKeys.ApplicationsCore)]
        [OperationsData]
        [HttpGet("leave-ledger")]
        [RequirePermission("Payroll Reports:View")]
        public async Task<IActionResult> LeaveLedger(
            [FromQuery] int year, [FromQuery] EmployeeScopeFilter scope, CancellationToken token)
        {
            var data = await _reportService.GetLeaveLedgerAsync(year, scope, token);
            return Ok(new { data, total = data.Count });
        }

        [RequireFeature(EntitlementKeys.ApplicationsCore)]
        [OperationsData]
        [HttpGet("leave-ledger/{employeeId:guid}/{leaveId:guid}/entries")]
        [RequirePermission("Payroll Reports:View")]
        public async Task<IActionResult> LeaveEntries(Guid employeeId, Guid leaveId, [FromQuery] int year, CancellationToken token)
        {
            var data = await _reportService.GetLeaveEntriesAsync(employeeId, leaveId, year, token);
            return Ok(new { data, total = data.Count });
        }

        [RequireFeature(EntitlementKeys.PayrollLoansIncome)]
        [CompensationData]
        [HttpGet("retirement-ledger")]
        [RequirePermission("Payroll Reports:View")]
        public async Task<IActionResult> RetirementLedger([FromQuery] DateTime? asOf, CancellationToken token)
        {
            var data = await _reportService.GetRetirementSummaryAsync(AsOfOrToday(asOf), token);
            return Ok(new { data, total = data.Count });
        }

        [RequireFeature(EntitlementKeys.PayrollLoansIncome)]
        [CompensationData]
        [HttpGet("retirement-ledger/{employeeId:guid}/entries")]
        [RequirePermission("Payroll Reports:View")]
        public async Task<IActionResult> RetirementEntries(Guid employeeId, [FromQuery] DateTime? asOf, CancellationToken token)
        {
            var data = await _reportService.GetRetirementEntriesAsync(employeeId, AsOfOrToday(asOf), token);
            return Ok(new { data, total = data.Count });
        }

        [RequireFeature(EntitlementKeys.PayrollLoansIncome)]
        [EntriesData]
        [HttpGet("uniform-allowance-ledger")]
        [RequirePermission("Payroll Reports:View")]
        public async Task<IActionResult> UniformAllowanceLedger(
            [FromQuery] DateTime? asOf, [FromQuery] Guid? clientId, [FromQuery] bool separatedWithBalance, CancellationToken token)
        {
            var data = await _reportService.GetUniformAllowanceSummaryAsync(AsOfOrToday(asOf), token, clientId, separatedWithBalance);
            return Ok(new { data, total = data.Count });
        }

        [RequireFeature(EntitlementKeys.PayrollLoansIncome)]
        [EntriesData]
        [HttpGet("uniform-allowance-ledger/{employeeId:guid}/entries")]
        [RequirePermission("Payroll Reports:View")]
        public async Task<IActionResult> UniformAllowanceEntries(Guid employeeId, [FromQuery] DateTime? asOf, CancellationToken token)
        {
            var data = await _reportService.GetUniformAllowanceEntriesAsync(employeeId, AsOfOrToday(asOf), token);
            return Ok(new { data, total = data.Count });
        }

        private static DateOnly AsOfOrToday(DateTime? asOf) =>
            asOf.HasValue ? DateOnly.FromDateTime(asOf.Value) : DateOnly.FromDateTime(DateTime.UtcNow);

        [RequireFeature(EntitlementKeys.PayrollLoansIncome)]
        [CompensationData]
        [HttpGet("reimbursement-list")]
        [RequirePermission("Payroll Reports:View")]
        public async Task<IActionResult> ReimbursementList([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken token)
        {
            var data = await _reportService.GetReimbursementListAsync(DateOnly.FromDateTime(from), DateOnly.FromDateTime(to), token);
            return Ok(new { data, total = data.Count });
        }

        [RequireFeature(EntitlementKeys.ClientsAgency)]
        [CompensationData]
        [HttpGet("cost-summary")]
        [RequirePermission("Payroll Reports:View")]
        public async Task<IActionResult> CostSummary(
            [FromQuery] DateTime from,
            [FromQuery] DateTime to,
            [FromQuery] string groupBy = "department",
            CancellationToken token = default)
        {
            var data = await _reportService.GetCostSummaryAsync(DateOnly.FromDateTime(from), DateOnly.FromDateTime(to), groupBy, token);
            return Ok(new { data, total = data.Count });
        }

        [RequireFeature(EntitlementKeys.PayrollCore)]
        [CompensationData]
        [HttpGet("ytd-summary")]
        [RequirePermission("Payroll Reports:View")]
        public async Task<IActionResult> YtdSummary([FromQuery] int year, [FromQuery] Guid? employeeId, CancellationToken token)
        {
            var data = await _reportService.GetYtdSummaryAsync(year, employeeId, token);
            return Ok(new { data, total = data.Count });
        }

        [RequireFeature(EntitlementKeys.PayrollCore)]
        [CompensationData]
        [HttpGet("13th-month-pay")]
        [RequirePermission("Payroll Reports:View")]
        public async Task<IActionResult> ThirteenthMonthPay([FromQuery] int year, CancellationToken token)
        {
            var data = await _reportService.GetThirteenthMonthAsync(year, token);
            return Ok(new { data, total = data.Count });
        }

        [RequireFeature(EntitlementKeys.PayrollCore)]
        [CompensationData]
        [HttpGet("13th-month-pay/print")]
        [EnableRateLimiting(RateLimitPolicies.DocumentRender)]
        [RequirePermission("Payroll Reports:Export")]
        public async Task<IActionResult> ThirteenthMonthPayPrint([FromQuery] int year, CancellationToken token)
        {
            var data = await _reportService.GetThirteenthMonthAsync(year, token);
            var company = await _companyService.FineOneAsync(token);
            var document = new ThirteenthMonthPayListDocument(data, year, company);
            var bytes = document.GeneratePdf();
            return File(bytes, "application/pdf", $"13th-month-pay-{year}.pdf");
        }

        // ── BIR 1601-C ────────────────────────────────────────────────────────────────

        [RequireFeature(EntitlementKeys.ReportsStatutory)]
        [CompensationData]
        [HttpGet("1601c")]
        [RequirePermission("BIR Reports:View")]
        public async Task<IActionResult> MonthlyRemittanceReturn(
            [FromQuery] DateTime from, [FromQuery] DateTime to,
            [FromQuery] bool amendedReturn, CancellationToken token)
        {
            var (summary, employees) = await _reportService.GetMonthlyRemittanceReturnAsync(
                DateOnly.FromDateTime(from), DateOnly.FromDateTime(to), amendedReturn, token);
            return Ok(new { data = employees, summary, total = employees.Count });
        }

        [RequireFeature(EntitlementKeys.ReportsStatutory)]
        [CompensationData]
        [HttpGet("1601c/print")]
        [EnableRateLimiting(RateLimitPolicies.DocumentRender)]
        [RequirePermission("BIR Reports:Export")]
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

        [RequireFeature(EntitlementKeys.ReportsStatutory)]
        [CompensationData]
        [HttpGet("alphalist")]
        [RequirePermission("BIR Reports:View")]
        public async Task<IActionResult> Alphalist([FromQuery] int year, CancellationToken token)
        {
            var data = await _reportService.GetAlphalistAsync(year, token);
            return Ok(new { data, total = data.Count });
        }

        [RequireFeature(EntitlementKeys.ReportsStatutory)]
        [CompensationData]
        [HttpGet("alphalist/print")]
        [EnableRateLimiting(RateLimitPolicies.DocumentRender)]
        [RequirePermission("BIR Reports:Export")]
        public async Task<IActionResult> AlphalistPrint([FromQuery] int year, CancellationToken token)
        {
            var data = await _reportService.GetAlphalistAsync(year, token);
            var company = await _companyService.FineOneAsync(token);
            var bytes = new AlphalistPreviewDocument(data, year, company).GeneratePdf();
            return File(bytes, "application/pdf", $"bir-alphalist-{year}.pdf");
        }

        [RequireFeature(EntitlementKeys.ReportsStatutory)]
        [CompensationData]
        [HttpGet("alphalist/export")]
        [EnableRateLimiting(RateLimitPolicies.DocumentRender)]
        [RequirePermission("BIR Reports:Export")]
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

        [RequireFeature(EntitlementKeys.ReportsStatutory)]
        [CompensationData]
        [HttpGet("2316")]
        [RequirePermission("BIR Reports:View")]
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

        [RequireFeature(EntitlementKeys.ReportsStatutory)]
        [CompensationData]
        [HttpGet("2316/print")]
        [EnableRateLimiting(RateLimitPolicies.DocumentRender)]
        [RequirePermission("BIR Reports:Export")]
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

        [RequireFeature(EntitlementKeys.ReportsStatutory)]
        [HttpGet("sss-r3/print")]
        [EnableRateLimiting(RateLimitPolicies.DocumentRender)]
        [RequirePermission("BIR Reports:Export")]
        public async Task<IActionResult> SssR3Print([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken token)
        {
            var fromDate = DateOnly.FromDateTime(from);
            var toDate = DateOnly.FromDateTime(to);
            var data = await _sssService.GetRemittanceReportAsync(fromDate, toDate, token);
            var company = await _companyService.FineOneAsync(token);
            var bytes = new ContributionFilePreviewDocument(data, "SSS Contribution Collection List (R3)", "SS Number", fromDate, toDate, company).GeneratePdf();
            return File(bytes, "application/pdf", $"sss-r3-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.pdf");
        }

        [RequireFeature(EntitlementKeys.ReportsStatutory)]
        [HttpGet("sss-r3/export")]
        [EnableRateLimiting(RateLimitPolicies.DocumentRender)]
        [RequirePermission("BIR Reports:Export")]
        public async Task<IActionResult> SssR3Export([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken token)
        {
            var fromDate = DateOnly.FromDateTime(from);
            var toDate = DateOnly.FromDateTime(to);
            var bytes = await _sssService.GenerateR3FileAsync(fromDate, toDate, token);
            return File(bytes, "text/csv", $"sss-r3-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.csv");
        }

        // ── PhilHealth EPRS ───────────────────────────────────────────────────────────

        [RequireFeature(EntitlementKeys.ReportsStatutory)]
        [HttpGet("philhealth-eprs/print")]
        [EnableRateLimiting(RateLimitPolicies.DocumentRender)]
        [RequirePermission("BIR Reports:Export")]
        public async Task<IActionResult> PhilHealthEprsPrint([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken token)
        {
            var fromDate = DateOnly.FromDateTime(from);
            var toDate = DateOnly.FromDateTime(to);
            var data = await _phicService.GetRemittanceReportAsync(fromDate, toDate, token);
            var company = await _companyService.FineOneAsync(token);
            var bytes = new ContributionFilePreviewDocument(data, "PhilHealth Electronic Premium Remittance (EPRS)", "PhilHealth No.", fromDate, toDate, company).GeneratePdf();
            return File(bytes, "application/pdf", $"philhealth-eprs-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.pdf");
        }

        [RequireFeature(EntitlementKeys.ReportsStatutory)]
        [HttpGet("philhealth-eprs/export")]
        [EnableRateLimiting(RateLimitPolicies.DocumentRender)]
        [RequirePermission("BIR Reports:Export")]
        public async Task<IActionResult> PhilHealthEprsExport([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken token)
        {
            var fromDate = DateOnly.FromDateTime(from);
            var toDate = DateOnly.FromDateTime(to);
            var bytes = await _phicService.GenerateEprsFileAsync(fromDate, toDate, token);
            return File(bytes, "text/csv", $"philhealth-eprs-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.csv");
        }

        // ── Pag-IBIG MCRF ─────────────────────────────────────────────────────────────

        [RequireFeature(EntitlementKeys.ReportsStatutory)]
        [HttpGet("pagibig-mcrf/print")]
        [EnableRateLimiting(RateLimitPolicies.DocumentRender)]
        [RequirePermission("BIR Reports:Export")]
        public async Task<IActionResult> PagIbigMcrfPrint([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken token)
        {
            var fromDate = DateOnly.FromDateTime(from);
            var toDate = DateOnly.FromDateTime(to);
            var data = await _hdmfService.GetRemittanceReportAsync(fromDate, toDate, token);
            var company = await _companyService.FineOneAsync(token);
            var bytes = new ContributionFilePreviewDocument(data, "Pag-IBIG Member's Contribution Remittance Form (MCRF)", "Pag-IBIG MID", fromDate, toDate, company).GeneratePdf();
            return File(bytes, "application/pdf", $"pagibig-mcrf-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.pdf");
        }

        [RequireFeature(EntitlementKeys.ReportsStatutory)]
        [HttpGet("pagibig-mcrf/export")]
        [EnableRateLimiting(RateLimitPolicies.DocumentRender)]
        [RequirePermission("BIR Reports:Export")]
        public async Task<IActionResult> PagIbigMcrfExport([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken token)
        {
            var fromDate = DateOnly.FromDateTime(from);
            var toDate = DateOnly.FromDateTime(to);
            var bytes = await _hdmfService.GenerateMcrfFileAsync(fromDate, toDate, token);
            return File(bytes, "text/csv", $"pagibig-mcrf-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.csv");
        }
    }
}
