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
    private readonly SalaryAdjustmentService _salaryAdjService;

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
        PayrollService payrollService,
        SalaryAdjustmentService salaryAdjService
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
        _salaryAdjService = salaryAdjService;
    }
    public async Task<CalculatorPayload?> ComposeAsync(
   DateRangePayload dtrPayload,
   List<EmployeeModelPayrollRun> employees,
   CancellationToken token)
    {
        // 1. Validate inputs early to avoid unnecessary DB calls
        if (employees == null || !employees.Any()) return null;
        try
        {
            var hasEmpIds = employees.Select(x => x.Id).ToHashSet();
            var empIds = hasEmpIds.ToList();
            // 2. Start all tasks in parallel (I/O Bound)
            var ratesTask = await _rateTableService.FindAllAsync(token);
            var leavesTask = await _leaveService.FindByDateRangeAsync(dtrPayload.FromDate, dtrPayload.ToDate, hasEmpIds, token);
            var leaveCreditsTask = await _leaveLedgerService.LoadCreditsAsync(empIds, token);
            var otherIncomeTask = await _otherIncomeService.LoadAsync(empIds, dtrPayload.FromDate, dtrPayload.ToDate, token);
            var deductionsTask = await _deductionService.LoadAsync(empIds, dtrPayload.FromDate, dtrPayload.ToDate, token);
            var salaryAdjTask = await _salaryAdjService.LoadAsync(empIds, dtrPayload.FromDate, dtrPayload.ToDate, token);
            var companyTask = await _companyService.FineOneAsync(token);
            // Contributions
            var payrollsTask = await _payrollService.LoadPostedPayrollAsync(dtrPayload.FromDate, dtrPayload.ToDate, token);
            var sssContriTask = await _ssscontriService.LoadContributionsAsync(dtrPayload.FromDate, dtrPayload.ToDate, token);
            var phicContriTask = await _phiccontriService.LoadContributionsAsync(dtrPayload.FromDate, dtrPayload.ToDate, token);
            var hdmfContriTask = await _hdmfcontriService.LoadContributionsAsync(dtrPayload.FromDate, dtrPayload.ToDate, token);
            var taxContriTask = await _taxcontriService.LoadContributionsAsync(dtrPayload.FromDate, dtrPayload.ToDate, token);

            // Gov Tables & Holidays
            var sssTableTask = await _govSSSService.LoadForPayrollrunAsync(dtrPayload.ToDate, token);
            var phicTableTask = await _govPHICService.LoadForPayrollrunAsync(dtrPayload.ToDate, token);
            var hdmfTableTask = await _govHDMFService.LoadForPayrollrunAsync(dtrPayload.ToDate, token);
            var taxTableTask = await _govTaxService.LoadForPayrollrunAsync(dtrPayload.ToDate, token);
            var holidaysTask = await _holidayService.GetAllHolidays(dtrPayload.FromDate, dtrPayload.ToDate, token);

            // 3. Wait for all to complete
            //await Task.WhenAll(
            //    ratesTask, leavesTask, leaveCreditsTask, otherIncomeTask, deductionsTask, companyTask,
            //    payrollsTask, sssContriTask, phicContriTask, hdmfContriTask, taxContriTask,
            //    sssTableTask, phicTableTask, hdmfTableTask, taxTableTask, holidaysTask,
            //    salaryAdjTask
            //);
            // 4. Extract results synchronously (No more 'await' needed here)
            //var companyInfo = companyTask;

            return new CalculatorPayload
            {
                FromDate = dtrPayload.FromDate,
                ToDate = dtrPayload.ToDate,
                PremiumRates = ratesTask.GroupBy(x => x.Type).ToDictionary(g => g.Key, g => g.FirstOrDefault()?.Rate ?? 0m),
                PostedPriorPayrolls = payrollsTask,
                Leaves = leavesTask,
                LeaveCredits = leaveCreditsTask,
                Deductions = deductionsTask,
                Incomes = otherIncomeTask,
                SalaryAdjustments = salaryAdjTask,
                SSSTableModel = sssTableTask,
                PHICTableModel = phicTableTask,
                HDMFTableModel = hdmfTableTask,
                TaxTableModel = taxTableTask,
                SSSContribution = sssContriTask,
                PHICContribution = phicContriTask,
                HDMFContribution = hdmfContriTask,
                TaxContribution = taxContriTask,
                // Initialization
                ProratedAllowance = new List<ProratedAllowanceForSSS>(),
                CompanyPolicy = new CompanyPolicyRule
                {
                    RequiredTakehomePercentage = companyTask?.TakehomePercentage ?? 10,
                    RequiredWorkingDays = companyTask?.TotalWorkingDays ?? 26,
                    ApplyStatutoryOnActualMonth = companyTask?.ApplyStatutoryOnActualMonth ?? true,
                }
            };
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (AggregateException ae)
        {
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
