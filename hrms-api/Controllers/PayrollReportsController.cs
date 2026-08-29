using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

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

        public PayrollReportsController(
            SSSContributionService sssService,
            PHICContributionService phicService,
            HDMFContributionService hdmfService,
            TaxContributionService taxService,
            PayrollReportService reportService)
        {
            _sssService = sssService;
            _phicService = phicService;
            _hdmfService = hdmfService;
            _taxService = taxService;
            _reportService = reportService;
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
    }
}
