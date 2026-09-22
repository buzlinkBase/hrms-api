using DTR.Core;
using Hrms.Core.Policies.DeductionPolicies;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Core.Services;

// Year-End Tax Annualization (BIR TRAIN law, RR 11-2018 §2.79.4): recomputes each employee's
// TRUE annual income tax due — against AnnualTaxTable's annual brackets, not the monthly/
// semi-monthly WTax table regular payroll uses — consolidating this employer's own posted
// payroll (PayrollReportService.GetAnnualTaxAnnualizationInputsAsync) with any prior employer's
// BIR 2316 YTD figures (PriorEmployerTaxRecord, for a job-changer hired mid-year), floors the
// resulting taxable income at 0, and nets the annual tax due against tax already withheld from
// both sources — producing a refund (over-withheld) or additional collection (under-withheld).
// Minimum Wage Earners are excluded entirely (zero adjustment — see MinimumWageEarnerResolver).
// A collection larger than a configurable multiple of the employee's average monthly net pay is
// flagged with a warning (informational only — never blocks Generate). Reuses
// PayrollSummaryLine -> Payroll, the same PayrollBatch/Post/Delete lifecycle as regular payroll,
// so Post/Delete/payslip/report infrastructure works unchanged (PayrollType.YearEndAdjustment).
// Sibling to ThirteenthMonthPayrollService/LastPayrollService; PayrollProcessorService (a thin
// Facade) delegates here.
public class TaxAnnualizationService
{
    private readonly PayrollService _payrollService;
    private readonly IMapper _mapper;
    private readonly PayrollBatchService _payrollBatchService;
    private readonly PayrollReportService _payrollReportService;
    private readonly AnnualTaxService _annualTaxService;
    private readonly EmployeePriorEmployerTaxRecordService _priorEmployerTaxRecordService;
    private readonly GeneralSettingService _generalSettingService;
    private readonly YearLockService _yearLockService;
    private readonly StatutoryContributionLedgerService _statutoryLedgerService;

    public TaxAnnualizationService(
        PayrollService payrollService,
        IMapper mapper,
        PayrollBatchService payrollBatchService,
        PayrollReportService payrollReportService,
        AnnualTaxService annualTaxService,
        EmployeePriorEmployerTaxRecordService priorEmployerTaxRecordService,
        GeneralSettingService generalSettingService,
        YearLockService yearLockService,
        StatutoryContributionLedgerService statutoryLedgerService)
    {
        _payrollService = payrollService;
        _mapper = mapper;
        _payrollBatchService = payrollBatchService;
        _payrollReportService = payrollReportService;
        _annualTaxService = annualTaxService;
        _priorEmployerTaxRecordService = priorEmployerTaxRecordService;
        _generalSettingService = generalSettingService;
        _yearLockService = yearLockService;
        _statutoryLedgerService = statutoryLedgerService;
    }

    public Task<List<TaxAnnualizationPreviewModel>> PreviewAsync(TaxAnnualizationRunPayload payload, CancellationToken token)
    {
        return ComputeAsync(payload, token);
    }

    public async Task<List<PayrollSummaryLine>> GenerateAsync(TaxAnnualizationRunPayload payload, CancellationToken token)
    {
        if (await _yearLockService.IsYearLockedAsync(payload.Year, token))
        {
            throw new ValidationException(
                $"Payroll for {payload.Year} is locked — the Year-End Tax Adjustment has already been posted " +
                "for this year. Reopen the year first if changes are required.");
        }

        var previews = await ComputeAsync(payload, token);

        var pending = previews
            .Where(x => !x.IsMinimumWageEarner && !x.AlreadyGenerated && x.AdjustmentAmount != 0)
            .ToList();
        if (pending.Count == 0)
        {
            throw new ValidationException(
                $"Year-End Tax Adjustment for {payload.Year} has already been generated for every eligible " +
                "employee in scope, or no eligible employee has a non-zero adjustment. Delete the existing " +
                "run first if you need to regenerate it.");
        }

        var effectiveDate = payload.PayDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var periodStart = new DateOnly(payload.Year, 1, 1);
        var periodEnd = new DateOnly(payload.Year, 12, 31);
        var batchId = Guid.CreateVersion7();

        var lines = new List<PayrollSummaryLine>();
        foreach (var preview in pending)
        {
            // Direct annual bracket lookup, not a per-period deduction chain — no
            // DeductionPipeline/WTaxCalculatorFactory involved, so the pipe data here exists
            // solely to reuse PayrollProcessorUtil.GetNetPay's GrossIncome - RunningTotal
            // formula, matching every other Payroll row's NetPay computation exactly.
            var deductionResult = new DeductionPipeData { RunningTotal = preview.AdjustmentAmount };

            var line = new PayrollSummaryLine
            {
                PayrollPeriod = $"Year-End Tax Adjustment {payload.Year}",
                PayPeriodStart = periodStart,
                PayPeriodEnd = periodEnd,
                PayrollDate = effectiveDate,
                StatutoryCreditDate = effectiveDate,
                PostingPeriod = effectiveDate,
                PayDate = payload.PayDate,
                PayrollBatchId = batchId,
                PayrollType = PayrollType.YearEndAdjustment,
                Remarks = payload.Remarks,
                EmployeeId = preview.EmployeeId,
                FullName = preview.FullName,
                SalaryType = preview.SalaryType,
                BasicPay = 0,
                GrossIncome = 0,
                // Positive = additional tax collected this run; negative = refund. See
                // StatutoryContributionLedgerService.SaveAsync's WithholdingTax > 0 filter —
                // a refund correctly produces no WTaxContribution remittance-ledger row.
                WithholdingTax = preview.AdjustmentAmount,
                PayrollGroupId = preview.PayrollGroupId,
                AreaId = preview.AreaId,
                ClientId = preview.ClientId,
            };
            line.NetPay = PayrollProcessorUtil.GetNetPay(line, deductionResult);
            lines.Add(line);
        }

        await _payrollBatchService.AddAsync(new PayrollBatch
        {
            Id = batchId,
            PayPeriodStart = periodStart,
            PayPeriodEnd = periodEnd,
            PayDate = payload.PayDate,
            PayrollType = PayrollType.YearEndAdjustment,
            Remarks = payload.Remarks,
        }, token, commit: false);

        var payrolls = _mapper.Map<List<Payroll>>(lines);
        foreach (var payroll in payrolls)
        {
            payroll.BatchCode = batchId.ToString();
        }
        await _payrollService.SavePayrollsAsync(payrolls, token, commit: false);
        // Atomic commit point — see PayrollBatchService.AddAsync's doc comment.
        await _payrollService.CommitChangesAsync(token);
        // SSS/PHIC/HDMF are never set on these lines, so SaveAsync's > 0 filters naturally
        // produce nothing for those three; only the WTaxContribution row (needed for BIR
        // remittance reporting, and only when this is a collection not a refund) gets written.
        await _statutoryLedgerService.SaveAsync(lines, token);

        return lines;
    }

    // Shared by Preview and Generate so both compute the adjustment identically — Generate
    // simply re-derives the same preview right before persisting, so it's always safe against
    // data that changed between a Preview call and the later Generate call.
    private async Task<List<TaxAnnualizationPreviewModel>> ComputeAsync(
        TaxAnnualizationRunPayload payload, CancellationToken token)
    {
        var inputs = await _payrollReportService.GetAnnualTaxAnnualizationInputsAsync(payload.Year, token);

        if (payload.EmployeeIds is { Count: > 0 })
            inputs = inputs.Where(x => payload.EmployeeIds.Contains(x.EmployeeId)).ToList();
        if (payload.PayrollGroupIds is { Count: > 0 })
            inputs = inputs.Where(x => x.PayrollGroupId.HasValue && payload.PayrollGroupIds.Contains(x.PayrollGroupId.Value)).ToList();

        var brackets = await _annualTaxService.FindAllAsync(token);
        var alreadyGenerated = await _payrollService.GetYearEndAdjustmentGeneratedEmployeeIdsAsync(payload.Year, token);
        var priorRecords = (await _priorEmployerTaxRecordService.FindAllByYearAsync(payload.Year, token))
            .ToDictionary(x => x.EmployeeId);

        var settings = await _generalSettingService.GetSettingsAsync(Hrms.Domain.ValueObjects.PayrollSettingsIdentity.IdentityType);
        var warningMultiplier = settings.TryGetValue(Hrms.Domain.ValueObjects.PayrollSettingsIdentity.KeyLargeTaxCollectionWarningMultiplier, out var multiplierSetting)
                      && multiplierSetting.Value != null
            ? (decimal)GeneralSettingsUtil.ParseDouble(multiplierSetting.Value, 1.0)
            : 1.0m;

        var previews = inputs.Select(input =>
        {
            priorRecords.TryGetValue(input.EmployeeId, out PriorEmployerTaxRecord? prior);
            var hasPrior = prior != null;
            var priorGross = prior?.GrossIncomeYtd ?? 0m;
            var priorNonTaxable = prior?.NonTaxableYtd ?? 0m;
            var priorStatutory = prior?.StatutoryDeductionsYtd ?? 0m;
            var priorWithheld = prior?.TaxWithheldYtd ?? 0m;

            // Consolidate current + prior employer, THEN floor at 0, THEN bracket-lookup —
            // mirrors the required BIR 2316-transfer consolidation order for a job-changer.
            var combinedGross = input.CurrentGrossIncome + priorGross;
            var rawTaxableIncome = (input.CurrentGrossIncome - input.CurrentNonTaxableBenefits - input.CurrentStatutoryDeductions)
                                  + (priorGross - priorNonTaxable - priorStatutory);
            var combinedTaxableIncome = Math.Round(Math.Max(0m, rawTaxableIncome), 2, MidpointRounding.AwayFromZero);
            var combinedWithheld = input.CurrentWithholdingTaxYTD + priorWithheld;

            // MWE exclusion stays gated on current-employer wage classification only — outside/
            // prior income never affects MWE status for this job.
            var annualTaxDue = input.IsMinimumWageEarner
                ? 0m
                : Math.Round(AnnualTaxCalculator.GetAnnualTaxDue(brackets, combinedTaxableIncome), 2, MidpointRounding.AwayFromZero);
            var adjustment = input.IsMinimumWageEarner
                ? 0m
                : Math.Round(annualTaxDue - combinedWithheld, 2, MidpointRounding.AwayFromZero);

            var exceedsWarning = adjustment > 0 && adjustment > (input.CurrentAverageMonthlyNetPay * warningMultiplier);

            return new TaxAnnualizationPreviewModel
            {
                EmployeeId = input.EmployeeId,
                EmployeeNo = input.EmployeeNo,
                FullName = input.FullName,
                Year = input.Year,
                PayrollGroupId = input.PayrollGroupId,
                AreaId = input.AreaId,
                ClientId = input.ClientId,
                SalaryType = input.SalaryType,
                CurrentGrossIncome = input.CurrentGrossIncome,
                CurrentNonTaxableBenefits = input.CurrentNonTaxableBenefits,
                CurrentStatutoryDeductions = input.CurrentStatutoryDeductions,
                CurrentWithholdingTaxYTD = input.CurrentWithholdingTaxYTD,
                CurrentAverageMonthlyNetPay = input.CurrentAverageMonthlyNetPay,
                IsMinimumWageEarner = input.IsMinimumWageEarner,
                IsUnclassified = input.IsUnclassified,
                HasPriorEmployerData = hasPrior,
                PriorEmployerGrossIncome = priorGross,
                PriorEmployerTaxWithheld = priorWithheld,
                AnnualGrossIncome = combinedGross,
                AnnualTaxableIncome = combinedTaxableIncome,
                AnnualWithholdingTaxYTD = combinedWithheld,
                AnnualTaxDue = annualTaxDue,
                AdjustmentAmount = adjustment,
                IsRefund = adjustment < 0,
                ExceedsLargeCollectionWarning = exceedsWarning,
                AlreadyGenerated = alreadyGenerated.Contains(input.EmployeeId),
            };
        }).OrderBy(x => x.FullName).ToList();

        return previews;
    }
}
