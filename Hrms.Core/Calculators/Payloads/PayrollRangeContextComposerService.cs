using DTR.Core;
using Hrms.Core.Policies.DeductionPolicies;
using Hrms.Domain.Entities;

namespace Hrms.Core.Calculators.Payloads;

public class PayrollRangeContextComposerService
{
    private readonly RateTableService _rateTableService;
    private readonly ClientRateTableService _clientRateTableService;
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
    private readonly PayrollService _payrollService;
    private readonly SalaryAdjustmentService _salaryAdjService;
    private readonly GeneralSettingService _generalSettingService;
    private readonly ClientService _clientService;

    public PayrollRangeContextComposerService(RateTableService rateTableService,
        ClientRateTableService clientRateTableService,
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
        PayrollService payrollService,
        SalaryAdjustmentService salaryAdjService,
        GeneralSettingService generalSettingService,
        ClientService clientService
        )
    {
        _rateTableService = rateTableService;
        _clientRateTableService = clientRateTableService;
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
        _payrollService = payrollService;
        _salaryAdjService = salaryAdjService;
        _generalSettingService = generalSettingService;
        _clientService = clientService;
    }
    public async Task<CalculatorPayload?> ComposePayload(
    DateRangePayload dtrPayload,
    List<EmployeeModelPayrollRun> employees,
    CancellationToken token,
    DateOnly? payDate = null)
    {
        // 1. Validate inputs early to avoid unnecessary DB calls
        if (employees == null || !employees.Any()) return null;
        try
        {
            var hasEmpIds = employees.Select(x => x.Id).ToHashSet();
            var empIds = hasEmpIds.ToList();
            // 2. Start all tasks in parallel (I/O Bound)
            var ratesTask = await _rateTableService.FindAllAsync(token);
            var clientIds = employees.Where(e => e.ClientId.HasValue).Select(e => e.ClientId!.Value).ToHashSet();
            var clientRatesTask = await _clientRateTableService.FindByClientsAsync(clientIds, token);
            var clientSettingsTask = await _generalSettingService.GetSettingsAsync(
                "Client", clientIds.Select(id => id.ToString()).ToHashSet());
            var clientRetirementDaysPerYearTask = await _clientService.FindRetirementDaysPerYearAsync(clientIds, token);
            var leavesTask = await _leaveService.FindByDateRangeAsync(dtrPayload.FromDate, dtrPayload.ToDate, hasEmpIds, token);
            var leaveCreditsTask = await _leaveLedgerService.LoadCreditsAsync(empIds, token);
            var otherIncomeTask = await _otherIncomeService.LoadAsync(empIds, dtrPayload.FromDate, dtrPayload.ToDate, token);
            var deductionsTask = await _deductionService.LoadAsync(empIds, dtrPayload.FromDate, dtrPayload.ToDate, token);
            var salaryAdjTask = await _salaryAdjService.LoadAsync(empIds, dtrPayload.FromDate, dtrPayload.ToDate, token);
            var oneTimeLeavePayoutsTask = await _leaveService.LoadOneTimePayoutsAsync(empIds, dtrPayload.FromDate, dtrPayload.ToDate, token);
            var companyTask = await _companyService.FineOneAsync(token);
            var companySettings = await _generalSettingService.GetSettingsAsync("Company");
            var crossMonthCreditPolicy = companySettings.TryGetValue(SettingKey.CrossMonthStatutoryCreditPolicy.ToString(), out var creditPolicySetting)
                ? GeneralSettingsUtil.ParseEnum(creditPolicySetting.Value, CrossMonthStatutoryCreditPolicy.CutoffStartMonth)
                : CrossMonthStatutoryCreditPolicy.CutoffStartMonth;
            // The same date the ledger was WRITTEN against (PayrollProcessorService.InitializePayrollLine's
            // StatutoryCreditDate) must be used here to read it back, or a cross-month cutoff's prior
            // withholding silently falls out of the balance-netting query.
            var creditDate = StatutoryCreditDateResolver.Resolve(dtrPayload.FromDate, dtrPayload.ToDate, crossMonthCreditPolicy, payDate);

            // WTax defaults to (and is independently configurable from) the SSS/PhilHealth/
            // Pag-IBIG policy above — BIR Form 1601-C reports withholding tax against the
            // payout month, not the period earned, so the default diverges (CutoffEndMonth).
            var wtaxCreditPolicy = companySettings.TryGetValue(SettingKey.WTaxCrossMonthCreditPolicy.ToString(), out var wtaxCreditPolicySetting)
                ? GeneralSettingsUtil.ParseEnum(wtaxCreditPolicySetting.Value, CrossMonthStatutoryCreditPolicy.CutoffEndMonth)
                : CrossMonthStatutoryCreditPolicy.CutoffEndMonth;
            var wtaxCreditDate = StatutoryCreditDateResolver.Resolve(dtrPayload.FromDate, dtrPayload.ToDate, wtaxCreditPolicy, payDate);

            // Setup > Company Policy > Minimum Take-Home Pay — see DeductionValidator.CanApply.
            var requiredTakehomePercentage = companySettings.TryGetValue(SettingKey.RequiredTakehomePercentage.ToString(), out var takehomeSetting)
                ? GeneralSettingsUtil.ParseDouble(takehomeSetting.Value, 10)
                : 10;

            // Setup > Client > Settings > Statutory Capping — only added when a client actually
            // has a positive cap set for that type; an absent key means uncapped (see
            // StatutoryCapHelper.ApplyClientCap).
            var clientStatutoryCaps = new Dictionary<ClientStatutoryCapKey, decimal>();
            foreach (var (groupKey, settings) in clientSettingsTask)
            {
                void AddCap(SettingKey key, StatutoryCapType type)
                {
                    var cap = settings.TryGetValue(key.ToString(), out var setting)
                        ? GeneralSettingsUtil.ParsePositiveDecimalOrNull(setting.Value)
                        : null;
                    if (cap.HasValue) clientStatutoryCaps[new ClientStatutoryCapKey(groupKey.IdentityId, type)] = cap.Value;
                }
                AddCap(SettingKey.MaxSSSCapping, StatutoryCapType.SSS);
                AddCap(SettingKey.MaxPhilHealthCapping, StatutoryCapType.PhilHealth);
                AddCap(SettingKey.MaxPagIbigCapping, StatutoryCapType.PagIbig);
            }

            // Contributions
            var payrollsTask = await _payrollService.LoadPostedPayrollAsync(dtrPayload.FromDate, dtrPayload.ToDate, token);
            var sssContriTask = await _ssscontriService.LoadContributionsAsync(creditDate, dtrPayload.ToDate, token);
            var phicContriTask = await _phiccontriService.LoadContributionsAsync(creditDate, dtrPayload.ToDate, token);
            var hdmfContriTask = await _hdmfcontriService.LoadContributionsAsync(creditDate, dtrPayload.ToDate, token);
            //var taxContriTask = await _taxcontriService.LoadContributionsAsync(wtaxCreditDate, dtrPayload.ToDate, token);

            // Gov Tables & Holidays
            var sssTableTask = await _govSSSService.LoadForPayrollrunAsync(token);
            var phicTableTask = await _govPHICService.LoadForPayrollrunAsync(token);
            var hdmfTableTask = await _govHDMFService.LoadForPayrollrunAsync(token);
            var taxTableTask = await _govTaxService.LoadForPayrollrunAsync(token);

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
                ClientPremiumRates = clientRatesTask.Values
                    .SelectMany(rows => rows)
                    .GroupBy(x => new ClientRateKey(x.ClientId, x.Type))
                    .ToDictionary(g => g.Key, g => g.First().Rate),
                ClientStatutoryCaps = clientStatutoryCaps,
                ClientRetirementDaysPerYear = clientRetirementDaysPerYearTask,
                PostedPriorPayrolls = payrollsTask,
                Leaves = leavesTask,
                LeaveCredits = leaveCreditsTask,
                Deductions = deductionsTask,
                Incomes = otherIncomeTask,
                SalaryAdjustments = salaryAdjTask,
                OneTimeLeavePayouts = oneTimeLeavePayoutsTask,
                SSSTableModel = sssTableTask,
                PHICTableModel = phicTableTask,
                HDMFTableModel = hdmfTableTask,
                TaxTableModel = taxTableTask,
                SSSContribution = sssContriTask,
                PHICContribution = phicContriTask,
                HDMFContribution = hdmfContriTask,
                //TaxContribution = taxContriTask,
                // Initialization
                //ProratedAllowance = new List<ProratedAllowanceForSSS>(),
                CompanyPolicy = new CompanyPolicyRule
                {
                    ApplyStatutoryOnActualMonth = companyTask?.ApplyStatutoryOnActualMonth ?? true,
                    CrossMonthStatutoryCreditPolicy = crossMonthCreditPolicy,
                    WTaxCrossMonthCreditPolicy = wtaxCreditPolicy,
                    RequiredTakehomePercentage = (decimal)requiredTakehomePercentage,
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
