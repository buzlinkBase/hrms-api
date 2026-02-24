using Hrms.Domain.Entities;

namespace Hrms.Core.Calculators.Payloads;

public class PayrollRangeContextComposerService
{
    private readonly RateTableService _rateTableService;
    private readonly LeaveApplicationService _leaveService;
    private readonly LeaveLedgerService _leaveLedgerService;
    private readonly IncomeAplDtlService _otherIncomeService;
    private readonly DeductionAplDtlService _deductionService;
    private readonly SSSService _govSSSService;
    private readonly PHICService _govPHICService;
    private readonly HDMFService _govHDMFService;
    private readonly TaxService _govTaxService;
    private readonly SSSContributionService _ssscontriService;
    private readonly PHICContributionService _phiccontriService;
    private readonly HDMFContributionService _hdmfcontriService;
    private readonly TaxContributionService _taxcontriService;
    private readonly CompanyService _companyService;
    private readonly HolidayService _holidayService;
    private readonly PayrollService _payrollService;

    public PayrollRangeContextComposerService(RateTableService rateTableService,
        LeaveApplicationService leaveService,
        LeaveLedgerService leaveLedgerService,
        IncomeAplDtlService otherIncomeService,
        DeductionAplDtlService deductionService,
        SSSService govSSSService,
        PHICService govPHICService,
        HDMFService govHDMFService,
        TaxService govTaxService,
        SSSContributionService ssscontriService,
        PHICContributionService phiccontriService,
        HDMFContributionService hdmfcontriService,
        TaxContributionService taxcontriService,
        CompanyService companyService,
        HolidayService holidayService,
        PayrollService payrollService
        )
    {
        _rateTableService = rateTableService;
        _leaveService = leaveService;
        _leaveLedgerService = leaveLedgerService;
        _otherIncomeService = otherIncomeService;
        _deductionService = deductionService;
        _govSSSService = govSSSService;
        _govPHICService = govPHICService;
        _govHDMFService = govHDMFService;
        _govTaxService = govTaxService;
        _ssscontriService = ssscontriService;
        _phiccontriService = phiccontriService;
        _hdmfcontriService = hdmfcontriService;
        _taxcontriService = taxcontriService;
        _companyService = companyService;
        _holidayService = holidayService;
        _payrollService = payrollService;
    }
    public async Task<CalculatorPayload?> ComposeAsync(
   PayrollCalcPayload dtrPayload,
   List<EmployeeModelPayrollRun> employees,
   CancellationToken token)
    {
        // 1. Validate inputs early to avoid unnecessary DB calls
        if (employees == null || !employees.Any()) return null;

        try
        {
            var empIds = employees.Select(x => x.Id).ToList();

            // 2. Start all tasks in parallel (I/O Bound)
            var ratesTask = _rateTableService.FindAllAsync(token);
            var leavesTask = _leaveService.FindLeaveDetailsAsync(dtrPayload.FromDate, dtrPayload.ToDate, token);
            var leaveCreditsTask = _leaveLedgerService.LoadCreditsAsync(empIds, token);
            var otherIncomeTask = _otherIncomeService.LoadAsync(empIds, dtrPayload.FromDate, dtrPayload.ToDate, token);
            var deductionsTask = _deductionService.LoadAsync(empIds, dtrPayload.FromDate, dtrPayload.ToDate, token);
            var companyTask = _companyService.FineOneAsync(token);

            // Contributions
            var payrollsTask = _payrollService.LoadPostedPayrollAsync(dtrPayload.FromDate, dtrPayload.ToDate, token);
            var sssContriTask = _ssscontriService.LoadContributionsAsync(dtrPayload.FromDate, dtrPayload.ToDate, token);
            var phicContriTask = _phiccontriService.LoadContributionsAsync(dtrPayload.FromDate, dtrPayload.ToDate, token);
            var hdmfContriTask = _hdmfcontriService.LoadContributionsAsync(dtrPayload.FromDate, dtrPayload.ToDate, token);
            var taxContriTask = _taxcontriService.LoadContributionsAsync(dtrPayload.FromDate, dtrPayload.ToDate, token);

            // Gov Tables & Holidays
            var sssTableTask = _govSSSService.LoadForPayrollrunAsync(dtrPayload.ToDate, token);
            var phicTableTask = _govPHICService.LoadForPayrollrunAsync(dtrPayload.ToDate, token);
            var hdmfTableTask = _govHDMFService.LoadForPayrollrunAsync(dtrPayload.ToDate, token);
            var taxTableTask = _govTaxService.LoadForPayrollrunAsync(dtrPayload.ToDate, token);
            var holidaysTask = _holidayService.GetAllHolidays(dtrPayload.FromDate, dtrPayload.ToDate, token);

            // 3. Wait for all to complete
            await Task.WhenAll(
                ratesTask, leavesTask, leaveCreditsTask, otherIncomeTask, deductionsTask, companyTask,
                payrollsTask, sssContriTask, phicContriTask, hdmfContriTask, taxContriTask,
                sssTableTask, phicTableTask, hdmfTableTask, taxTableTask, holidaysTask
            );

            // 4. Extract results synchronously (No more 'await' needed here)
            var companyInfo = companyTask.Result;

            return new CalculatorPayload
            {
                FromDate = dtrPayload.FromDate,
                ToDate = dtrPayload.ToDate,

                // Transform data using synchronous access
                PremiumRates = ratesTask.Result
                    .GroupBy(x => x.Type)
                    .ToDictionary(g => g.Key, g => g.FirstOrDefault()?.Rate ?? 0),

                Payrolls = payrollsTask.Result,
                Leaves = leavesTask.Result,
                LeaveCredits = leaveCreditsTask.Result,
                Deductions = deductionsTask.Result,
                Incomes = otherIncomeTask.Result,
                SSSTableModel = sssTableTask.Result,
                PHICTableModel = phicTableTask.Result,
                HDMFTableModel = hdmfTableTask.Result,
                TaxTableModel = taxTableTask.Result,
                SSSContribution = sssContriTask.Result,
                PHICContribution = phicContriTask.Result,
                HDMFContribution = hdmfContriTask.Result,
                TaxContribution = taxContriTask.Result,

                // Initialization
                ProratedAllowance = new List<ProratedAllowanceForSSS>(),
                CompanyPolicy = new CompanyPolicyRule
                {
                    RequiredTakehomePercentage = companyInfo?.TakehomePercentage ?? 10,
                    RequiredWorkingDays = companyInfo?.TotalWorkingDays ?? 26,
                    ApplyStatutoryOnActualMonth = companyInfo?.ApplyStatutoryOnActualMonth ?? true,
                }
            };
        }
        catch (OperationCanceledException)
        {
            // Handle cancellation gracefully (e.g., user navigated away)
            return null;
        }
        catch (AggregateException ae)
        {
            // Task.WhenAll wraps exceptions in an AggregateException
            foreach (var ex in ae.Flatten().InnerExceptions)
            {
                // Log each specific DB failure here
                // _logger.LogError(ex, "One of the payroll data fetches failed");
            }
            return null;
        }
        catch (Exception ex)
        {
            // Fallback for unexpected errors
            return null;
        }
    }
}
