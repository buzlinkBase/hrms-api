using DTR.Core;
using Hrms.Core.Policies.DeductionPolicies;
using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

public class PayrollProcessorService
{
    private readonly DailyRecordService _dtrServie;
    private readonly PayrollRangeContextComposerService _payloadComposer;
    private readonly PayrollService _payrollService;
    private readonly IMapper _mapper;
    private readonly ICalculator<DTRPayModel, PayrollContext> _basicPayrollCalculator;
    private readonly ICalculator<AllowancePipeData, PayrollContext> _allowancesCalculator;
    private readonly ICalculator<DeductionPipeData, DeductionPayloadContext> _deductionCalculator;
    private readonly IDailyRateResolver _dailyRateResolver;
    private readonly EmployeePayrollInclusionResolver _inclusionResolver;
    private readonly SSSContributionService _sssContributionService;
    private readonly PHICContributionService _phicContributionService;
    private readonly HDMFContributionService _hdmfContributionService;
    private readonly TaxContributionService _taxContributionService;
    private readonly LeaveService _leaveService;
    private readonly PayrollBatchService _payrollBatchService;
    private readonly PayrollReportService _payrollReportService;
    private readonly GeneralSettingService _generalSettingService;
    private readonly EmployeeService _employeeService;
    private readonly TaxService _taxService;

    public PayrollProcessorService(
        PayrollRangeContextComposerService payloadComposer,
        DailyRecordService dtrServie,
        PayrollService payrollService,
        IMapper mapper,
        ICalculator<DTRPayModel, PayrollContext> basicPayrollCalculator,
        ICalculator<AllowancePipeData, PayrollContext> allowancesCalculator,
        ICalculator<DeductionPipeData, DeductionPayloadContext> deductionCalculator,
        IDailyRateResolver dailyRateResolver,
        EmployeePayrollInclusionResolver inclusionResolver,
        SSSContributionService sssContributionService,
        PHICContributionService phicContributionService,
        HDMFContributionService hdmfContributionService,
        TaxContributionService taxContributionService,
        LeaveService leaveService,
        PayrollBatchService payrollBatchService,
        PayrollReportService payrollReportService,
        GeneralSettingService generalSettingService,
        EmployeeService employeeService,
        TaxService taxService)
    {
        _dtrServie = dtrServie;
        _payloadComposer = payloadComposer;
        _payrollService = payrollService;
        _mapper = mapper;
        _basicPayrollCalculator = basicPayrollCalculator;
        _allowancesCalculator = allowancesCalculator;
        _deductionCalculator = deductionCalculator;
        _dailyRateResolver = dailyRateResolver;
        _inclusionResolver = inclusionResolver;
        _sssContributionService = sssContributionService;
        _phicContributionService = phicContributionService;
        _hdmfContributionService = hdmfContributionService;
        _taxContributionService = taxContributionService;
        _leaveService = leaveService;
        _payrollBatchService = payrollBatchService;
        _payrollReportService = payrollReportService;
        _generalSettingService = generalSettingService;
        _employeeService = employeeService;
        _taxService = taxService;
    }

    public async Task<List<PayrollSummaryLine>> GenerateAsync(PayrollRunPayload payload, CancellationToken token)
    {
        var usedBatchCodes = await _payrollBatchService.GetUsedDtrBatchCodesAsync(token);
        var alreadyPosted = payload.BatchCodes.Where(usedBatchCodes.Contains).ToList();
        if (alreadyPosted.Count > 0)
        {
            throw new ValidationException(
                $"Payroll has already been generated for DTR batch(es): {string.Join(", ", alreadyPosted)}. " +
                "Delete the existing payroll run first if you need to regenerate it.");
        }

        var lines = await CalculateAsync(payload, token);
        if (lines.Count() == 0) return lines;
        var savingBatch = Guid.CreateVersion7().ToString();

        // CalculateAsync already stamped every line's PayrollBatchId with the id it wants to
        // travel with (see InitializePayrollLine) — the PayrollBatch header row must be
        // inserted under that same id, otherwise Payroll.PayrollBatchId (what the frontend
        // groups by and posts/deletes against) never matches any real PayrollBatch.Id.
        await _payrollBatchService.AddAsync(new PayrollBatch
        {
            Id = lines.First().PayrollBatchId,
            PayPeriodStart = lines.MinBy(x => x.PayPeriodStart)!.PayPeriodStart,
            PayPeriodEnd = lines.MaxBy(x => x.PayPeriodEnd)!.PayPeriodEnd,
            PayDate = payload.PayDate,
            DtrBatchCodes = string.Join(",", payload.BatchCodes),
            Remarks = payload.Remarks,
        }, token);

        // Generating no longer marks payroll as posted — a run stays an editable/deletable
        // draft (PayrollBatch.IsPosted defaults to false) until explicitly posted via
        // PostBatchAsync. Saving is not posting.
        var payrolls = _mapper.Map<List<Payroll>>(lines);
        foreach (var payroll in payrolls)
        {
            payroll.BatchCode = savingBatch;
        }
        await _payrollService.SavePayrollsAsync(payrolls, token);
        await SaveStatutoryContributionsAsync(lines, token);

        // Saving a draft locks in the DTR it was built from — posts every DTR batch this run
        // used (idempotent/no-op on an already-posted batch) so it can't be edited or reused
        // by another Generate run while this draft exists. See DeleteBatchAsync for the
        // inverse: deleting a draft unposts these same DTR batches again.
        foreach (var batchCode in payload.BatchCodes)
        {
            await _dtrServie.PostAsync(batchCode, token);
        }

        return lines;
    }

    // 13th month pay (PD 851): total BasicPay earned in the calendar year / 12 — a lump-sum,
    // non-attendance-based payout, so unlike GenerateAsync there's no DTR batch to build
    // from. Not subject to SSS/PhilHealth/Pag-IBIG (never invokes those calculators); the
    // portion over ThirteenthMonthExemptionCeiling is run through the same WTax table
    // calculator regular pay uses (not full BIR annualization — see plan notes). Reuses the
    // GenerateAsync draft shape (PayrollSummaryLine -> Payroll, same PayrollBatch/Post/Delete
    // lifecycle) so Post/Delete/payslip/report infrastructure works unchanged.
    public async Task<List<PayrollSummaryLine>> GenerateThirteenthMonthAsync(ThirteenthMonthRunPayload payload, CancellationToken token)
    {
        var figures = await _payrollReportService.GetThirteenthMonthAsync(payload.Year, token);
        if (payload.EmployeeIds is { Count: > 0 })
            figures = figures.Where(x => payload.EmployeeIds.Contains(x.EmployeeId)).ToList();

        var employees = await _employeeService.GetForThirteenthMonthRunAsync(payload.PayrollGroupIds, payload.EmployeeIds, token);
        var employeeMap = employees.ToDictionary(x => x.Id);
        // Only employees eligible for 13th month (per GetForThirteenthMonthRunAsync's filter)
        // AND scoped by PayrollGroupIds, if provided.
        figures = figures.Where(x => employeeMap.ContainsKey(x.EmployeeId)).ToList();
        if (figures.Count == 0) return new List<PayrollSummaryLine>();

        var alreadyPaid = await _payrollService.GetThirteenthMonthPaidEmployeeIdsAsync(payload.Year, token);
        var pending = figures.Where(x => !alreadyPaid.Contains(x.EmployeeId)).ToList();
        if (pending.Count == 0)
        {
            throw new ValidationException(
                $"13th Month Pay for {payload.Year} has already been generated for every eligible employee in scope. " +
                "Delete the existing run first if you need to regenerate it.");
        }

        var settings = await _generalSettingService.GetSettingsAsync(PayrollSettingsIdentity.IdentityType);
        var ceiling = settings.TryGetValue(PayrollSettingsIdentity.KeyThirteenthMonthExemptionCeiling, out var ceilingSetting)
                      && ceilingSetting.Value != null
            ? GeneralSettingsUtil.ParseDouble(ceilingSetting.Value, 90_000)
            : 90_000;

        var effectiveDate = payload.PayDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var taxTable = await _taxService.LoadForPayrollrunAsync(effectiveDate, token);
        var periodStart = new DateOnly(payload.Year, 1, 1);
        var periodEnd = new DateOnly(payload.Year, 12, 31);
        var batchId = Guid.CreateVersion7();

        var lines = new List<PayrollSummaryLine>();
        foreach (var figure in pending)
        {
            var employee = employeeMap[figure.EmployeeId];
            var gross = figure.ThirteenthMonthPay;
            // Per Payroll Settings' documented rule, the exemption ceiling covers 13th month
            // pay COMBINED with Special Bonuses already paid this year — whatever room those
            // bonuses already consumed isn't available to this payout. Does not retroactively
            // touch the WTax already withheld on those bonus payroll runs, only this payout's
            // own split.
            var remainingCeiling = ComputeRemainingThirteenthMonthCeiling((decimal)ceiling, figure.TotalSpecialBonusesForYear);
            var (nonTaxable, taxable) = ComputeThirteenthMonthTaxSplit(gross, remainingCeiling);

            var wtaxResult = new DeductionPipeData { RemainingGrossBalance = taxable };
            if (taxable > 0 && employee.TaxRate != null)
            {
                // A lump-sum annual payout doesn't fit any Daily/Weekly/Semi-Monthly cutoff
                // table — Monthly-scale thresholds are the closest fit among the existing
                // tables. Mutating PayrollFrequency here is safe: this employee object was
                // just loaded for this run only and isn't reused elsewhere.
                employee.PayrollFrequency = PayrollFrequency.MONTHLY;
                var wtaxContext = new DeductionPayloadContext
                {
                    Employee = employee,
                    Payload = new CalculatorPayload
                    {
                        FromDate = periodStart,
                        ToDate = periodEnd,
                        TaxTableModel = taxTable,
                        CompanyPolicy = new CompanyPolicyRule(),
                    },
                    PayrollLine = new PayrollSummaryLine { GrossIncome = taxable },
                };
                wtaxResult = WTaxCalculatorFactory.Create(wtaxContext).Calculate(wtaxContext, wtaxResult);
            }

            var line = new PayrollSummaryLine
            {
                PayrollPeriod = $"13th Month Pay {payload.Year}",
                PayPeriodStart = periodStart,
                PayPeriodEnd = periodEnd,
                PayrollDate = effectiveDate,
                StatutoryCreditDate = effectiveDate,
                PostingPeriod = effectiveDate,
                PayDate = payload.PayDate,
                PayrollBatchId = batchId,
                PayrollType = PayrollType.ThirteenthMonth,
                Remarks = payload.Remarks,
                EmployeeId = figure.EmployeeId,
                FullName = figure.FullName,
                SalaryType = employee.SalaryType,
                BasicPay = 0, // never counted toward a future year's 13th month/Alphalist figure
                GrossIncome = gross,
                NonTaxableBenefits = nonTaxable,
                TaxableBenefits = taxable,
                WithholdingTax = wtaxResult.TaxInfo?.TaxDue ?? 0,
                PayrollGroupId = employee.PayrollGroupId,
                AreaId = employee.AreaId,
                ClientId = employee.ClientId,
            };
            line.NetPay = PayrollProcessorUtil.GetNetPay(line, wtaxResult);
            lines.Add(line);
        }

        await _payrollBatchService.AddAsync(new PayrollBatch
        {
            Id = batchId,
            PayPeriodStart = periodStart,
            PayPeriodEnd = periodEnd,
            PayDate = payload.PayDate,
            PayrollType = PayrollType.ThirteenthMonth,
            Remarks = payload.Remarks,
        }, token);

        var payrolls = _mapper.Map<List<Payroll>>(lines);
        foreach (var payroll in payrolls)
        {
            payroll.BatchCode = batchId.ToString();
        }
        await _payrollService.SavePayrollsAsync(payrolls, token);
        // SSS/PHIC/HDMF are never set on these lines, so SaveStatutoryContributionsAsync's
        // > 0 filters naturally produce nothing for those three; only the WTaxContribution
        // row (needed for BIR remittance reporting) gets written.
        await SaveStatutoryContributionsAsync(lines, token);

        return lines;
    }

    // Splits a 13th month gross into the non-taxable portion (up to the exemption ceiling)
    // and the taxable excess above it, per TRAIN law. `internal` so it's directly unit
    // testable without a database — see GenerateThirteenthMonthAsync.
    internal static (decimal NonTaxable, decimal Taxable) ComputeThirteenthMonthTaxSplit(decimal gross, decimal ceiling)
    {
        var nonTaxable = Math.Min(gross, ceiling);
        var taxable = Math.Max(0, gross - ceiling);
        return (nonTaxable, taxable);
    }

    // Per Payroll Settings: the exemption ceiling covers 13th month pay + Special Bonuses
    // combined, not 13th month pay alone — whatever ceiling room Special Bonuses already paid
    // this year consumed isn't available to the 13th month payout. `internal` so it's directly
    // unit testable without a database — see GenerateThirteenthMonthAsync.
    internal static decimal ComputeRemainingThirteenthMonthCeiling(decimal ceiling, decimal priorSpecialBonuses) =>
        Math.Max(0, ceiling - priorSpecialBonuses);

    // Locks a whole Generate run in as final — an employee's payroll is never posted on its
    // own, since it was never generated on its own either. Updates the canonical
    // PayrollBatch.IsPosted plus each child Payroll row's denormalized copy (see
    // Payroll.PayrollBatchId doc comment) so existing per-row report filters keep working
    // unchanged.
    public async Task PostBatchAsync(Guid payrollBatchId, CancellationToken token)
    {
        await _payrollBatchService.PostAsync(payrollBatchId, token);
        await _payrollService.PostBatchAsync(payrollBatchId, token);
    }

    // Deletes every row from one Generate run (same PayrollBatchId) in a single action, plus
    // the PayrollBatch header row itself. Post and Delete are run-level transactions, not
    // per-employee ones — an employee's payroll was never generated on its own, so it isn't
    // posted or deleted on its own either. Regenerating is blocked while ANY row from the
    // run's DTR batch(es) still exists (see GenerateAsync/GetUsedDtrBatchCodesAsync), so a
    // partial per-row cleanup wouldn't actually unblock a re-run anyway. Also removes the
    // SSS/PHIC/HDMF/WTax ledger rows SaveStatutoryContributionsAsync wrote for this batch,
    // since those are written unconditionally regardless of IsPosted and the remittance
    // reports read them directly rather than filtering by Payroll — leaving them behind
    // would show contributions for a payroll run that no longer exists. Each cascade delete
    // is a single ExecuteDeleteAsync keyed on PayrollBatchId (not a per-employee loop —
    // these 4 tables can reach millions of rows, so this matters), matching how
    // PayrollService.DeleteByBatchIdAsync already deletes the Payroll rows themselves. If
    // the batch has already been posted, it's left alone — a posted run is final.
    public async Task DeleteBatchAsync(Guid payrollBatchId, CancellationToken token)
    {
        var batch = await _payrollBatchService.FineOneAsync(payrollBatchId, token);
        if (batch == null) throw new ValidationException("Payroll batch not found.");
        if (batch.IsPosted)
            throw new ValidationException("This payroll run has already been posted and can no longer be deleted.");

        await _sssContributionService.DeleteByBatchIdAsync(payrollBatchId, token);
        await _phicContributionService.DeleteByBatchIdAsync(payrollBatchId, token);
        await _hdmfContributionService.DeleteByBatchIdAsync(payrollBatchId, token);
        await _taxContributionService.DeleteByBatchIdAsync(payrollBatchId, token);
        await _payrollService.DeleteByBatchIdAsync(payrollBatchId, token);
        await _payrollService.CommitChangesAsync(token);

        // Deleting a draft releases the DTR it was built from — unposts every DTR batch this
        // run used (idempotent/no-op on an already-unposted batch), the inverse of
        // GenerateAsync's post-on-save. Only ever reached for a draft (IsPosted == false was
        // already checked above), matching "unpost DTR if the draft is deleted."
        var dtrBatchCodes = (batch.DtrBatchCodes ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (var batchCode in dtrBatchCodes)
        {
            await _dtrServie.UnpostAsync(batchCode, token);
        }

        await _payrollBatchService.DeleteAsync(payrollBatchId, token);
        await _payrollBatchService.CommitChangesAsync(token);
    }

    // Persists one SSS/PHIC/HDMF ledger row per employee for this cutoff, so the next
    // cutoff's balance-netting (SSSHelper/PHICHelper/HDMFHelper.GetBalance) can see what's
    // already been withheld this month instead of always treating the target as untouched.
    private async Task SaveStatutoryContributionsAsync(List<PayrollSummaryLine> lines, CancellationToken token)
    {
        var sssRows = lines
            .Where(l => l.SSSContribution > 0 || l.EmployerSSSContribution > 0)
            .Select(l => new SSSContribution
            {
                PayrollBatchId = l.PayrollBatchId,
                EmployeeId = l.EmployeeId,
                PayrollFrom = l.PayPeriodStart,
                PayrollTo = l.PayPeriodEnd,
                PayrollDate = l.StatutoryCreditDate,
                EE = l.SSSContribution,
                ER = l.EmployerSSSContribution - l.EmployerECContribution,
                EC = l.EmployerECContribution,
                TotalContibution = l.SSSContribution + l.EmployerSSSContribution,
            })
            .ToList();

        var phicRows = lines
            .Where(l => l.PhilHealthContribution > 0 || l.EmployerPhilHealthContribution > 0)
            .Select(l => new PHICContribution
            {
                PayrollBatchId = l.PayrollBatchId,
                EmployeeId = l.EmployeeId,
                PayrollFrom = l.PayPeriodStart,
                PayrollTo = l.PayPeriodEnd,
                PayrollDate = l.StatutoryCreditDate,
                EmployeeShare = l.PhilHealthContribution,
                EmployerShare = l.EmployerPhilHealthContribution,
                TotalContribution = l.PhilHealthContribution + l.EmployerPhilHealthContribution,
            })
            .ToList();

        var hdmfRows = lines
            .Where(l => l.PagIbigContribution > 0 || l.EmployerPagIbigContribution > 0)
            .Select(l => new HDMFContribution
            {
                PayrollBatchId = l.PayrollBatchId,
                EmployeeId = l.EmployeeId,
                PayrollFrom = l.PayPeriodStart,
                PayrollTo = l.PayPeriodEnd,
                PayrollDate = l.StatutoryCreditDate,
                EmployeeShare = l.PagIbigContribution,
                EmployerShare = l.EmployerPagIbigContribution,
                TotalContribution = l.PagIbigContribution + l.EmployerPagIbigContribution,
            })
            .ToList();

        var taxRows = lines
            .Where(l => l.WithholdingTax > 0)
            .Select(l => new WTaxContribution
            {
                PayrollBatchId = l.PayrollBatchId,
                EmployeeId = l.EmployeeId,
                PayrollFrom = l.PayPeriodStart,
                PayrollTo = l.PayPeriodEnd,
                PayrollDate = l.PostingPeriod,
                Date = l.PayPeriodEnd,
                Amount = l.WithholdingTax,
            })
            .ToList();

        if (sssRows.Count > 0) await _sssContributionService.AddRangeAsync(sssRows, token);
        if (phicRows.Count > 0) await _phicContributionService.AddRangeAsync(phicRows, token);
        if (hdmfRows.Count > 0) await _hdmfContributionService.AddRangeAsync(hdmfRows, token);
        if (taxRows.Count > 0) await _taxContributionService.AddRangeAsync(taxRows, token);
    }

    public async Task<List<PayrollSummaryLine>> CalculateAsync(PayrollRunPayload payload, CancellationToken token)
    {
        var payrollLines = new List<PayrollSummaryLine>();
        var dtrRecords = await _dtrServie.LoadForPayrollRunAsync(payload.BatchCodes, token);
        if (dtrRecords.Records == null || !dtrRecords.Records.Any()) return payrollLines;
        var leaveInfoByEmployee = await _dtrServie.LoadLeaveInfoForPayrollRunAsync(payload.BatchCodes, token);
        var dateRange = new DateRangePayload(dtrRecords.FromDate, dtrRecords.ToDate);
        var period = BuildPayrollPeriod(dateRange);
        var batch = Guid.CreateVersion7();

        var employees = dtrRecords.Records.Values
            .SelectMany(x => x.Select(y => y.Employee))
            .DistinctBy(x => x.Id)
            .ToList();

        if (employees == null || employees.Count() == 0)
        {
            return payrollLines;
        }

        // Resolve tenant-vs-employee Fixed-salary inclusion settings once, upstream —
        // every downstream DTR pay policy keeps reading employee.IsXxxIncluded unchanged.
        await _inclusionResolver.ApplyAsync(employees!, token);
        var rangePayload = await _payloadComposer.ComposePayload(dateRange, employees, token, payload.PayDate)
                          ?? throw new Exception("Unable to load range payload");

        // PayDate is user-supplied input, not a config fallback — silently defaulting to
        // ToDate here would quietly violate the company's chosen posting policy, so this
        // fails loudly instead (StatutoryCreditDateResolver's own fallback is defense in
        // depth only).
        var needsPayDate = rangePayload.CompanyPolicy.CrossMonthStatutoryCreditPolicy == CrossMonthStatutoryCreditPolicy.PayDate
            || rangePayload.CompanyPolicy.WTaxCrossMonthCreditPolicy == CrossMonthStatutoryCreditPolicy.PayDate;
        if (needsPayDate && payload.PayDate == null)
            throw new ValidationException("A Pay/Release Date is required to generate this payroll under the configured statutory posting policy.");

        // Small master table — load once per run rather than per employee. Used to split
        // PaidLeaves by funding source (see ComputeBasicSalary/ComputeAllowances).
        var leavePaySourceMap = (await _leaveService.FindAllAsync(token))
            .ToDictionary(x => x.Id, x => x.PaySource);

        foreach (var employee in employees)
        {
            if (employee == null) continue;
            if (!dtrRecords.Records.TryGetValue(new EmployeeKey(employee.Id), out var empDtr))
            {
                continue;
            }
            employee.DailyRate = _dailyRateResolver.Resolve(employee, dateRange.FromDate);
            var payrollLine = InitializePayrollLine(
                dateRange, employee, batch, period,
                rangePayload.CompanyPolicy.CrossMonthStatutoryCreditPolicy,
                rangePayload.CompanyPolicy.WTaxCrossMonthCreditPolicy,
                payload.PayDate, payload.Remarks);

            ComputeHoursBreakdown(empDtr, payrollLine);
            leaveInfoByEmployee.TryGetValue(new EmployeeKey(employee.Id), out var empLeaveInfo);
            payrollLine.PaidLeaveBreakdown = BuildPaidLeaveBreakdown(empLeaveInfo);
            ComputeBasicSalary(dateRange, empDtr, employee, rangePayload, payrollLine, leavePaySourceMap);
            ComputeNonCompanyPaidLeaves(empLeaveInfo, leavePaySourceMap, payrollLine);
            ComputeAllowances(dateRange, rangePayload, employee, payrollLine);
            ApplyOneTimeLeavePayoutsToGross(rangePayload, employee, payrollLine);
            payrollLine.GrossIncome = PayrollProcessorUtil.GetGross(payrollLine);
            // Deliberately before ComputeDeductions (unlike ApplySalaryAdjustments, which runs
            // after) — the Company-funded portion must already be part of GrossIncome so the
            // SSS/PHIC/HDMF/WTax calculators below see it via StatutoryHelper.Get*GrossBaseRate.
            ComputeDeductions(rangePayload, employee, payrollLine);
            // ComputeDeductions just freshly recomputed NetPay from GrossIncome (not an
            // increment), so the Government-funded portion — deliberately kept out of
            // GrossIncome/statutory bases above — can only be added to NetPay here, after.
            payrollLine.NetPay += payrollLine.GovernmentFundedLeavePay;
            ApplySalaryAdjustments(rangePayload, employee, payrollLine);
            payrollLines.Add(payrollLine);
        }
        return payrollLines;
    }

    // Straight per-employee sum of DailyRecord's own per-category hour fields across every
    // day in the period — the DTR posting pipeline already computed these (see
    // DailyRecordRunModel/DailyRecord), this just carries them forward instead of letting
    // them get dropped once the DTR rows are rolled up into pay amounts. No payroll math of
    // its own; OvertimeHours is the one derived figure — the sum of every pure *OTHours
    // category (excluding the ND-OT combo hours, which are their own columns) — matching
    // what the Payroll Summary "OT Total Hr" figure means.
    internal static void ComputeHoursBreakdown(List<DailyRecordRunModel> dtrs, PayrollSummaryLine line)
    {
        decimal Sum(Func<DailyRecordRunModel, double> selector) => (decimal)dtrs.Sum(selector);

        line.RegularNetHours = Sum(x => x.RegularNetHours);
        line.RegularOTHours = Sum(x => x.RegularOTHours);
        line.RegularNDHours = Sum(x => x.RegularNDHours);
        line.RegularNDOTHours = Sum(x => x.RegularNDOTHours);

        line.RestDayHours = Sum(x => x.RestDayHours);
        line.RestDayOTHours = Sum(x => x.RestDayOTHours);
        line.RestDayNDHours = Sum(x => x.RestDayNDHours);
        line.RestDayNDOTHours = Sum(x => x.RestDayNDOTHours);

        line.LegalHolHours = Sum(x => x.LegalHolHours);
        line.LegalHolOTHours = Sum(x => x.LegalHolOTHours);
        line.LegalHolNightDiffHours = Sum(x => x.LegalHolNightDiffHours);
        line.LegalHolNightDiffOTHours = Sum(x => x.LegalHolNightDiffOTHours);

        line.SpecialHolHours = Sum(x => x.SpecialHolHours);
        line.SpecialHolOTHours = Sum(x => x.SpecialHolOTHours);
        line.SpecialHolNightDiffHours = Sum(x => x.SpecialHolNightDiffHours);
        line.SpecialHolNightDiffOTHours = Sum(x => x.SpecialHolNightDiffOTHours);

        line.RestLegalDayHours = Sum(x => x.RestLegalDayHours);
        line.RestLegalDayOTHours = Sum(x => x.RestLegalDayOTHours);
        line.RestLegalDayNDHours = Sum(x => x.RestLegalDayNDHours);
        line.RestLegalDayNDOTHours = Sum(x => x.RestLegalDayNDOTHours);

        line.RestSpecialDayHours = Sum(x => x.RestSpecialDayHours);
        line.RestSpecialDayOTHours = Sum(x => x.RestSpecialDayOTHours);
        line.RestSpecialDayNDHours = Sum(x => x.RestSpecialDayNDHours);
        line.RestSpecialDayNDOTHours = Sum(x => x.RestSpecialDayNDOTHours);

        line.DoubleLegalHours = Sum(x => x.DoubleLegalHours);
        line.DoubleLegalOTHours = Sum(x => x.DoubleLegalOTHours);
        line.DoubleLegalNDHours = Sum(x => x.DoubleLegalNDHours);
        line.DoubleLegalNDOTHours = Sum(x => x.DoubleLegalNDOTHours);

        line.RestDoubleLegalHours = Sum(x => x.RestDoubleLegalHours);
        line.RestDoubleLegalOTHours = Sum(x => x.RestDoubleLegalOTHours);
        line.RestDoubleLegalNDHours = Sum(x => x.RestDoubleLegalNDHours);
        line.RestDoubleLegalNDOTHours = Sum(x => x.RestDoubleLegalNDOTHours);

        line.OBHours = (decimal)dtrs.Sum(x => x.OBHours);
        line.PaidLeaveHours = (decimal)dtrs.Sum(x => x.PaidLeaveHours);
        line.UnpaidLeaveHours = (decimal)dtrs.Sum(x => x.UnpaidLeaveHours);

        line.OvertimeHours = line.RegularOTHours + line.RestDayOTHours +
            line.LegalHolOTHours + line.SpecialHolOTHours +
            line.RestLegalDayOTHours + line.RestSpecialDayOTHours +
            line.DoubleLegalOTHours + line.RestDoubleLegalOTHours;
    }

    // Which leave type(s) made up this run's PaidLeaves/UnpaidLeaves totals, and how many
    // hours each — grouped by leave + pay type since the same leave can appear as both Paid
    // and Unpaid across different days in one run (e.g. balance ran out mid-period). Purely
    // a display aid; does not change PaidLeaves/UnpaidLeaves/GrossIncome, which are computed
    // elsewhere from the same DTR days.
    internal static string? BuildPaidLeaveBreakdown(List<LeaveMetaDataModel>? leaveInfo)
    {
        if (leaveInfo == null || leaveInfo.Count == 0) return null;
        var parts = leaveInfo
            .GroupBy(x => new { x.Name, x.PayType })
            .Select(g => $"{g.Key.Name}: {g.Sum(x => x.Hours):0.##}h ({(g.Key.PayType == PayType.WithoutPay ? "Unpaid" : "Paid")})")
            .ToList();
        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    private static string BuildPayrollPeriod(DateRangePayload payload)
    {

        return string.Concat(
               payload.FromDate.ToString("MMM-dd-yyyy"), " ",
               payload.ToDate.ToString("MMM-dd-yyyy"),
               string.Empty);
    }

    private static PayrollSummaryLine InitializePayrollLine(
        DateRangePayload payload,
        EmployeeModelPayrollRun employee,
        Guid payrollBatchId,
        string period,
        CrossMonthStatutoryCreditPolicy creditPolicy,
        CrossMonthStatutoryCreditPolicy wtaxCreditPolicy,
        DateOnly? payDate,
        string? remarks) =>
        new PayrollSummaryLine
        {
            PayrollPeriod = period,
            PayPeriodStart = payload.FromDate,
            PayPeriodEnd = payload.ToDate,
            EmployeeId = employee.Id,
            FullName = employee.FullName,
            PayrollBatchId = payrollBatchId,
            Remarks = remarks,
            PayrollDate = payload.ToDate,
            StatutoryCreditDate = StatutoryCreditDateResolver.Resolve(payload.FromDate, payload.ToDate, creditPolicy, payDate),
            PostingPeriod = StatutoryCreditDateResolver.Resolve(payload.FromDate, payload.ToDate, wtaxCreditPolicy, payDate),
            PayDate = payDate,
            PayrollGroupId = employee.PayrollGroupId,
            AreaId = employee.AreaId,
            ClientId = employee.ClientId
        };

    private void ComputeBasicSalary(
        DateRangePayload payload,
        List<DailyRecordRunModel> dtrs,
        EmployeeModelPayrollRun employee,
        CalculatorPayload rangePayload,
        PayrollSummaryLine payrollLine,
        Dictionary<Guid, PaySource> leavePaySourceMap)
    {
        var employeeBasicCalc = CalculateDTRTimePay(payload, dtrs, employee, rangePayload);
        payrollLine.TimeHourPayResults = employeeBasicCalc;
        payrollLine.SalaryType = employee.SalaryType;
        payrollLine.DailyRate = employee.DailyRate;
        GetBasicPay(payrollLine, employeeBasicCalc, employee);
        payrollLine.GrossIncome += payrollLine.BasicPay + employeeBasicCalc.Sum(x => x.TotalExcludingBasic);
        payrollLine.LateAmount = employeeBasicCalc.Sum(x => x.LateAmount);
        payrollLine.OvertimePay = employeeBasicCalc.Sum(x => x.TotalOT);
        payrollLine.UnderTimeAmount = employeeBasicCalc.Sum(x => x.UTAmount);
        payrollLine.NightDifferentialPay = employeeBasicCalc.Sum(x => x.TotalND);
        payrollLine.NightDifferentialOTPay = employeeBasicCalc.Sum(x => x.TotalNDOT);
        payrollLine.OTPremiumPay = employeeBasicCalc.Sum(x => x.OTPremiumPay);
        payrollLine.NDPremiumPay = employeeBasicCalc.Sum(x => x.NDPremiumPay);
        payrollLine.AbsencesAmount = employeeBasicCalc.Sum(x => x.AbsentAmount);

        payrollLine.RegularDayPay = employeeBasicCalc.Sum(x => x.RegularDayPay);
        payrollLine.RegularOTPay = employeeBasicCalc.Sum(x => x.RegularOTPay);
        payrollLine.RegularNDPay = employeeBasicCalc.Sum(x => x.RegularNDPay);
        payrollLine.RegularNDOTPay = employeeBasicCalc.Sum(x => x.RegularNDOTPay);

        payrollLine.RestDayPay = employeeBasicCalc.Sum(x => x.RestDayPay);
        payrollLine.RestDayOTPay = employeeBasicCalc.Sum(x => x.RestDayOTPay);
        payrollLine.RestDayNDPay = employeeBasicCalc.Sum(x => x.RestDayNDPay);
        payrollLine.RestDayNDOTPay = employeeBasicCalc.Sum(x => x.RestDayNDOTPay);

        payrollLine.LegalPay = employeeBasicCalc.Sum(x => x.LegalPay);
        payrollLine.LegalOTPay = employeeBasicCalc.Sum(x => x.LegalOTPay);
        payrollLine.LegalNDPay = employeeBasicCalc.Sum(x => x.LegalNDPay);
        payrollLine.LegalNDOTPay = employeeBasicCalc.Sum(x => x.LegalNDOTPay);

        payrollLine.SpecialPay = employeeBasicCalc.Sum(x => x.SpecialPay);
        payrollLine.SpecialOTPay = employeeBasicCalc.Sum(x => x.SpecialOTPay);
        payrollLine.SpecialNDPay = employeeBasicCalc.Sum(x => x.SpecialNDPay);
        payrollLine.SpecialNDOTPay = employeeBasicCalc.Sum(x => x.SpecialNDOTPay);

        payrollLine.RestLegalPay = employeeBasicCalc.Sum(x => x.RestLegalPay);
        payrollLine.RestLegalOTPay = employeeBasicCalc.Sum(x => x.RestLegalOTPay);
        payrollLine.RestLegalNDPay = employeeBasicCalc.Sum(x => x.RestLegalNDPay);
        payrollLine.RestLegalNDOTPay = employeeBasicCalc.Sum(x => x.RestLegalNDOTPay);

        payrollLine.RestSpecialPay = employeeBasicCalc.Sum(x => x.RestSpecialPay);
        payrollLine.RestSpecialOTPay = employeeBasicCalc.Sum(x => x.RestSpecialOTPay);
        payrollLine.RestSpecialNDPay = employeeBasicCalc.Sum(x => x.RestSpecialNDPay);
        payrollLine.RestSpecialNDOTPay = employeeBasicCalc.Sum(x => x.RestSpecialNDOTPay);

        payrollLine.DoubleLegalPay = employeeBasicCalc.Sum(x => x.DoubleLegalPay);
        payrollLine.DoubleLegalOTPay = employeeBasicCalc.Sum(x => x.DoubleLegalOTPay);
        payrollLine.DoubleLegalNDPay = employeeBasicCalc.Sum(x => x.DoubleLegalNDPay);
        payrollLine.DoubleLegalNDOTPay = employeeBasicCalc.Sum(x => x.DoubleLegalNDOTPay);

        payrollLine.RestDoubleLegalPay = employeeBasicCalc.Sum(x => x.RestDoubleLegalPay);
        payrollLine.RestDoubleLegalOTPay = employeeBasicCalc.Sum(x => x.RestDoubleLegalOTPay);
        payrollLine.RestDoubleLegalNDPay = employeeBasicCalc.Sum(x => x.RestDoubleLegalNDPay);
        payrollLine.RestDayNDOTPay = employeeBasicCalc.Sum(x => x.RestDayNDOTPay);
        payrollLine.UnpaidLeaves = employeeBasicCalc.Sum(x => x.UnpaidLeave);
        payrollLine.PaidLeaves = employeeBasicCalc.Sum(x => x.PaidLeave);

        payrollLine.HolidayPay = employeeBasicCalc.Sum(x => x.Holiday);
        payrollLine.LegalHolidayUnworkedPay = employeeBasicCalc.Sum(x => x.LegalUnWorked);

    }

    // Splits PaidLeaves by funding source using each DTR day's LeavesInfo (LeaveId + hours,
    // written by the DTR reconciliation engine) joined against Leave.PaySource. Expressed
    // as a proportional share of the already-computed PaidLeaves money (rather than
    // re-deriving hourly-rate pay here) so it can never exceed PaidLeaves and stays
    // consistent with whatever rate/proration LeavePolicy applied. Display/reporting only —
    // see NonCompanyPaidLeaves doc comment for why this must NOT be added into GrossIncome.
    internal static void ComputeNonCompanyPaidLeaves(
        List<LeaveMetaDataModel>? leaveInfo,
        Dictionary<Guid, PaySource> leavePaySourceMap,
        PayrollSummaryLine payrollLine)
    {
        // Needs payrollLine.PaidLeaves, just set above — display-only split, does not
        // touch GrossIncome/BasicPay (see NonCompanyPaidLeaves doc comment).
        if (leaveInfo == null || leaveInfo.Count == 0 || payrollLine.PaidLeaves == 0) return;
        var paidLeaveInfo = leaveInfo.Where(x => x.PayType == PayType.WithPay).ToList();
        var totalHours = paidLeaveInfo.Sum(x => x.Hours);
        if (totalHours <= 0) return;
        var nonCompanyHours = paidLeaveInfo
            .Where(x => leavePaySourceMap.TryGetValue(x.LeaveId, out var source) && source != PaySource.Company)
            .Sum(x => x.Hours);
        payrollLine.NonCompanyPaidLeaves = payrollLine.PaidLeaves * ((decimal)nonCompanyHours / (decimal)totalHours);
    }


    private List<DTRPayModel> CalculateDTRTimePay(
        DateRangePayload payload,
        List<DailyRecordRunModel> dtrs,
        EmployeeModelPayrollRun employee,
        CalculatorPayload calcPayload)
    {

        var basicResultMoel = new List<DTRPayModel>();
        for (var date = payload.FromDate; date <= payload.ToDate; date = date.AddDays(1))
        {
            var record = dtrs.FirstOrDefault(x => x.WorkDate == date && x.EmployeeId == employee.Id);
            if (record == null) continue;
            var context = new PayrollContextBuilder()
                .SetEmployee(employee)
                .SetDailyRecord(record)
                .SetWorkType(record)
                .SetPayrollDate(date)
                .SetPayload(calcPayload)
                .Build();

            var result = _basicPayrollCalculator.Calculate(context);
            if (result == null) continue;
            result.Date = date;
            result.DTRRef = record.BatchCode;
            result.DtrId = record.Id;
            result.SalaryType = employee.SalaryType;
            basicResultMoel.Add(result);
        }
        return basicResultMoel;
    }

    private void GetBasicPay(PayrollSummaryLine payrollLine, List<DTRPayModel> TimeCalcResult, EmployeeModelPayrollRun employee)
    {
        if (employee.SalaryType != SalaryType.FIXED)
        {
            payrollLine.BasicPay = TimeCalcResult.Sum(x => x.RegularDayPay);
            return;
        }
        var divisor = GetDivisor(payrollLine.PayPeriodStart, employee);
        var basicTotal = employee.MonthlyRate / divisor;
        var deductions = Math.Max(0, TimeCalcResult.Sum(x => x.LateAmount + x.UTAmount + x.UnpaidLeave + x.AbsentAmount));
        basicTotal = basicTotal - deductions;
        payrollLine.BasicPay += basicTotal;
    }


    private int GetDivisor(DateOnly fromDate, EmployeeModelPayrollRun employee)
    {
        if (employee.PayrollGroup == null) return 2;
        switch (employee.PayrollGroup.PayrollFrequency)
        {
            case PayrollFrequency.DAILY:
                var days = DateTime.DaysInMonth(fromDate.Year, fromDate.Month);
                return days;
            case PayrollFrequency.WEEKLY:
                return 4;
            case PayrollFrequency.SEMI_MONTHLY:
                return 2;
            case PayrollFrequency.MONTHLY:
                return 1;
            default:
                return 2;
        }
    }

    private AllowancePipeData ComputeAllowances(
        DateRangePayload payload,
        CalculatorPayload rangePayload,
        EmployeeModelPayrollRun employee,
        PayrollSummaryLine payrollLine)
    {
        var pp = new PayrollCalcPayload(payload.FromDate, payload.ToDate, null, null, null, null);
        var context = new PayrollContextBuilder()
            .SetEmployee(employee)
            .SetPayload(rangePayload)
            .Build();

        AllowancePipeData IncomeCalcResult = _allowancesCalculator.Calculate(context);
        payrollLine.Cola = IncomeCalcResult.Cola;
        payrollLine.TotalDeminimises = IncomeCalcResult.Deminimises.Sum(x => x.Amount);
        payrollLine.TotalOtherIncome = IncomeCalcResult.OtherIncome.Sum(x => x.Amount);
        payrollLine.TotalCommissions = IncomeCalcResult.Commissions.Sum(x => x.Amount);
        payrollLine.TotalBonuses = IncomeCalcResult.Bonuses.Sum(x => x.Amount);
        payrollLine.Reimbursement = IncomeCalcResult.Reimbursements.Sum(x => x.Amount);
        payrollLine.TotalRegularAllowances = IncomeCalcResult.RegularAllowances.Sum(x => x.Amount);
        payrollLine.OtherIncomeCollection = IncomeCalcResult.AllIncome;
        payrollLine.RegularAllowanceProrated = CaptureProratedAllowance(pp, payrollLine.TotalRegularAllowances);
        payrollLine.TotalAllIncome = IncomeCalcResult.RunningTotal;
        IdentifyTaxableIncome(payrollLine, IncomeCalcResult);
        return IncomeCalcResult;
    }

    private void ComputeDeductions(
        CalculatorPayload rangePayload,
        EmployeeModelPayrollRun employee,
        PayrollSummaryLine payrollLine)
    {
        var deductionContext = new DeductionPayloadContextBuilder()
            .SetPayload(rangePayload)
            .SetEmployee(employee)
            .SetPayrollLine(payrollLine)
            .Build();

        var deductionPipeLine = _deductionCalculator.Calculate(deductionContext);
        payrollLine.DeductionCollection = deductionPipeLine.ScheduledDeductions;
        payrollLine.NetPay = PayrollProcessorUtil.GetNetPay(payrollLine, deductionPipeLine);
        payrollLine.SSSContribution = deductionPipeLine.SSS.EE;
        payrollLine.PhilHealthContribution = deductionPipeLine.PHIC.EE;
        payrollLine.PagIbigContribution = deductionPipeLine.HDMF.EE;
        payrollLine.WithholdingTax = deductionPipeLine.TaxInfo.TaxDue;
        payrollLine.OtherDeductions = deductionPipeLine.ScheduledDeductions.Sum(x => x.Amount);
        payrollLine.TotalLoans = deductionPipeLine.ScheduledDeductions
            .Where(x => x.Type == DeductionInfoType.Loan)
            .Sum(x => x.Amount);
        payrollLine.TotalDeductions = deductionPipeLine.RunningTotal;
        payrollLine.EmployerSSSContribution = deductionPipeLine.SSS.TotalER;
        payrollLine.EmployerPhilHealthContribution = deductionPipeLine.PHIC.Total;
        payrollLine.EmployerPagIbigContribution = deductionPipeLine.HDMF.Total;
        payrollLine.EmployerECContribution = deductionPipeLine.SSS.EC;
    }

    // Injects an approved OneTime leave payout (see LeaveApplication.PayoutMode) matched to
    // this run via ReleasePayrollDate. GovernmentAmount is deliberately kept out of
    // GrossIncome — it's a government benefit pass-through, not compensation, and all four
    // statutory calculators (SSS/PHIC/HDMF/WTax) share the same GrossIncome-based bracket
    // lookup, so there's no cheaper way to exempt it from WTax alone. CompanyAmount is taxable
    // compensation, so it's added to GrossIncome here and (see the call site) NetPay is left
    // for ComputeDeductions to (re)compute from that — GovernmentAmount is added to NetPay
    // separately, after ComputeDeductions runs.
    // internal (not private) so hrms.test can exercise this directly without a DB — see
    // Hrms.Core's InternalsVisibleTo for hrms.test.
    internal static void ApplyOneTimeLeavePayoutsToGross(
        CalculatorPayload rangePayload,
        EmployeeModelPayrollRun employee,
        PayrollSummaryLine payrollLine)
    {
        if (!rangePayload.OneTimeLeavePayouts.TryGetValue(new EmployeeKey(employee.Id), out var payouts))
            return;

        var breakdownParts = new List<string>();
        foreach (var payout in payouts)
        {
            var gov = payout.GovernmentAmount ?? 0;
            var comp = payout.CompanyAmount ?? 0;
            payrollLine.GovernmentFundedLeavePay += gov;
            payrollLine.CompanyFundedLeavePay += comp;
            payrollLine.NonTaxableBenefits += gov;
            payrollLine.TaxableBenefits += comp;
            // Deliberately NOT added to GrossIncome — gov is a government benefit
            // pass-through, exempt from the SSS/PHIC/HDMF/WTax bracket lookups that all key
            // off GrossIncome (see NetPay reconciliation at the call site).
            payrollLine.GrossIncome += comp;
            var leaveName = payout.Leave.Description;
            breakdownParts.Add(gov > 0 && comp > 0
                ? $"{leaveName}: {comp:0.00} (Company) + {gov:0.00} (Government)"
                : comp > 0
                    ? $"{leaveName}: {comp:0.00} (Company)"
                    : $"{leaveName}: {gov:0.00} (Government)");
        }
        payrollLine.OneTimePayoutBreakdown = breakdownParts.Count == 0 ? null : string.Join("; ", breakdownParts);
    }

    private static void ApplySalaryAdjustments(
        CalculatorPayload rangePayload,
        EmployeeModelPayrollRun employee,
        PayrollSummaryLine payrollLine)
    {
        if (!rangePayload.SalaryAdjustments.TryGetValue(new EmployeeKey(employee.Id), out var adjustments))
            return;

        foreach (var adj in adjustments)
        {
            //TODO add other salary type adjustment here
            switch (adj.AdjustmentType)
            {
                case SalaryAdjustmentType.Salary:
                    payrollLine.BasicPay += adj.Amount;
                    payrollLine.GrossIncome += adj.Amount;
                    payrollLine.NetPay += adj.Amount;
                    break;
                case SalaryAdjustmentType.Allowance:
                    payrollLine.TotalOtherIncome += adj.Amount;
                    payrollLine.GrossIncome += adj.Amount;
                    payrollLine.NetPay += adj.Amount;
                    break;
                case SalaryAdjustmentType.Deduction:
                    payrollLine.OtherDeductions += adj.Amount;
                    payrollLine.TotalDeductions += adj.Amount;
                    payrollLine.NetPay -= adj.Amount;
                    break;
            }
        }
    }

    private List<ProratedAllowanceModel> CaptureProratedAllowance(PayrollCalcPayload payload, decimal regularAllowance)
    {

        var result = new List<ProratedAllowanceModel>();
        var allDates = new List<DateOnly>();
        // Collect all dates in the range
        for (var date = payload.FromDate; date <= payload.ToDate; date = date.AddDays(1))
        {
            allDates.Add(date);
        }

        // Total days covered in the entire range
        int totalDays = allDates.Count;

        // Daily rate based on total allowance
        decimal dailyRate = regularAllowance / totalDays;

        // Group by month/year and compute prorated allowance
        var monthDays = allDates
            .GroupBy(x => new MonthYear(x.Month, x.Year))
            .Select(g => new { Key = g.Key, DayCount = g.Count() })
            .ToList();

        foreach (var item in monthDays)
        {
            result.Add(new ProratedAllowanceModel
            {
                Month = item.Key.Month,
                Year = item.Key.Year,
                Amount = dailyRate * item.DayCount // prorated value for that month
            });
        }
        return result;
    }
    private void IdentifyTaxableIncome(PayrollSummaryLine payrollLine, AllowancePipeData incomeInfo)
    {
        //var allincome = incomeInfo.OtherIncome
        //      .Union(incomeInfo.RegularAllowances)
        //      .Union(incomeInfo.Deminimises)
        //      .Union(incomeInfo.Commissions)
        //      .Union(incomeInfo.Bonuses)
        ;

        var allincome = incomeInfo.AllIncome;

        payrollLine.NonTaxableBenefits = allincome
            .Where(x => !x.Taxable)
            .Sum(x => x.Amount);

        payrollLine.TaxableBenefits = allincome
            .Where(x => x.Taxable)
            .Sum(x => x.Amount);
    }
}

public class ProratedAllowanceModel
{
    public int Month { get; set; }
    public int Year { get; set; }
    public decimal Amount { get; set; }
}

public class PayrollProcessorUtil
{
    public static decimal GetGross(PayrollSummaryLine payrollLine)
    {
        var gross = payrollLine.BasicPay
            + payrollLine.TimeHourPayResults.Sum(x => x.TotalExcludingBasic)
            + payrollLine.TotalAllIncome
            + payrollLine.CompanyFundedLeavePay
            + payrollLine.GovernmentFundedLeavePay
            ;
        return gross;
    }

    public static decimal GetNetPay(PayrollSummaryLine payrollLine, DeductionPipeData deductionPipeLine)
    {
        return payrollLine.GrossIncome - deductionPipeLine.RunningTotal;
    }
}
public record MonthYear(int Month, int Year);
