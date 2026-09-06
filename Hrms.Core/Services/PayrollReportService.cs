using Hrms.Core.Extensions;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Domain.ValueObjects;

namespace Hrms.Core.Services;

// Cross-cutting payroll reports that don't naturally belong to any single existing
// service — Bank Disbursement, Loan Ledger, Leave Ledger, Department/Client/Branch Cost
// Summary, Year-to-Date Summary, and 13th Month Pay. All read-only over already-persisted data.
public class PayrollReportService : BaseService<Payroll>
{
    private readonly EmployeeService _employeeService;

    public PayrollReportService(IUnitOfWorkService uow, EmployeeService employeeService) : base(uow)
    {
        _employeeService = employeeService;
    }

    public async Task<List<BankDisbursementModel>> GetBankDisbursementAsync(DateOnly from, DateOnly to, CancellationToken token)
    {
        var rows = await GetQueryable(x => x.PayPeriodStart >= from && x.PayPeriodEnd <= to && x.IsPosted).ToListAsync(token);
        var employeeMap = await LoadEmployeeMapAsync(rows.Select(x => x.EmployeeId), token);

        return rows.Select(r =>
        {
            employeeMap.TryGetValue(r.EmployeeId, out var e);
            return new BankDisbursementModel
            {
                EmployeeId = r.EmployeeId,
                EmployeeNo = e?.EmployeeNo ?? "",
                FullName = e.FullName(),
                ModeOfPayment = e?.ModeOfPayment ?? PaymentMethod.Cash,
                BankName = e?.BankName ?? "",
                BankNo = e?.BankNo ?? "",
                PayPeriodStart = r.PayPeriodStart,
                PayPeriodEnd = r.PayPeriodEnd,
                NetPay = r.NetPay,
            };
        }).OrderBy(x => x.FullName).ToList();
    }

    // "Current outstanding balance" for a loan = the Balance of the highest-RecordOrder row
    // (the most recently-scheduled still-unpaid installment) among that (Employee, Deduction)
    // pair's rows with Balance > 0 — DeductionApplicationDetail rows are an amortization
    // schedule (one row per installment), not one row per loan.
    public async Task<List<LoanLedgerModel>> GetLoanLedgerAsync(DateOnly asOf, CancellationToken token)
    {
        var dueRows = await Context.DeductionApplicationDetails.AsNoTracking()
            .Where(x => x.Date <= asOf && x.Balance > 0)
            .ToListAsync(token);

        var latestPerLoan = dueRows
            .GroupBy(x => new { x.EmployeeId, x.DeductionId, x.ApplicationId })
            .Select(g => g.OrderByDescending(x => x.RecordOrder).First())
            .ToList();

        var applicationIds = latestPerLoan.Select(x => x.ApplicationId).Distinct().ToList();
        var applicationMap = (await Context.DeductionApplications.AsNoTracking()
            .Where(x => applicationIds.Contains(x.Id)).ToListAsync(token))
            .ToDictionary(x => x.Id);

        var deductionIds = latestPerLoan.Select(x => x.DeductionId).Distinct().ToList();
        var deductions = await Context.Deductions.AsNoTracking()
            .Where(x => deductionIds.Contains(x.Id)).ToListAsync(token);
        var deductionMap = deductions.ToDictionary(x => x.Id);

        var categoryIds = deductions.Where(x => x.CategoryId.HasValue).Select(x => x.CategoryId!.Value).Distinct().ToList();
        var categoryMap = (await Context.DeductionTypes.AsNoTracking()
            .Where(x => categoryIds.Contains(x.Id)).ToListAsync(token))
            .ToDictionary(x => x.Id);

        var employeeMap = await LoadEmployeeMapAsync(latestPerLoan.Select(x => x.EmployeeId), token);

        return latestPerLoan.Select(x =>
        {
            applicationMap.TryGetValue(x.ApplicationId, out var app);
            deductionMap.TryGetValue(x.DeductionId, out var deduction);
            var loanTypeName = deduction?.CategoryId != null && categoryMap.TryGetValue(deduction.CategoryId.Value, out var cat)
                ? cat.Name : "";
            employeeMap.TryGetValue(x.EmployeeId, out var e);
            return new LoanLedgerModel
            {
                EmployeeId = x.EmployeeId,
                EmployeeNo = e?.EmployeeNo ?? "",
                FullName = e.FullName(),
                DeductionId = x.DeductionId,
                LoanTypeName = loanTypeName,
                LoanName = deduction?.Name ?? "",
                TotalPrincipal = app?.TotalPrincipal ?? 0,
                InterestRate = app?.InterestRate ?? 0,
                StartDate = app?.StartDate ?? default,
                EndDate = app?.EndDate ?? default,
                CurrentBalance = x.Balance,
            };
        }).OrderBy(x => x.FullName).ToList();
    }

    // Leave credits balances — LeaveCredits already holds the authoritative per-employee,
    // per-leave-type, per-period balance (Granted/Used/Balance/Reserved/AvailableToFile),
    // so this reads it directly rather than re-deriving from the LeaveLedger transaction log.
    public async Task<List<LeaveCreditsBalanceModel>> GetLeaveLedgerAsync(int year, CancellationToken token)
    {
        var rows = await Context.LeaveCredits.AsNoTracking()
            .Where(x => x.PeriodYear == year)
            .ToListAsync(token);

        var employeeMap = await LoadEmployeeMapAsync(rows.Select(x => x.EmployeeId), token);

        var leaveIds = rows.Select(x => x.LeaveId).Distinct().ToList();
        var leaveMap = (await Context.Leaves.AsNoTracking()
            .Where(x => leaveIds.Contains(x.Id)).ToListAsync(token))
            .ToDictionary(x => x.Id);

        return rows.Select(r =>
        {
            employeeMap.TryGetValue(r.EmployeeId, out var e);
            leaveMap.TryGetValue(r.LeaveId, out var leave);
            return new LeaveCreditsBalanceModel
            {
                EmployeeId = r.EmployeeId,
                EmployeeNo = e?.EmployeeNo ?? "",
                FullName = e.FullName(),
                LeaveId = r.LeaveId,
                LeaveCode = leave?.Code ?? "",
                LeaveDescription = leave?.Description ?? "",
                PeriodYear = r.PeriodYear,
                Granted = r.Granted,
                Used = r.Used,
                Balance = r.Balance,
                Reserved = r.Reserved,
                AvailableToFile = r.AvailableToFile,
            };
        }).OrderBy(x => x.FullName).ThenBy(x => x.LeaveCode).ToList();
    }

    public async Task<List<CostSummaryModel>> GetCostSummaryAsync(DateOnly from, DateOnly to, string groupBy, CancellationToken token)
    {
        var payrolls = await GetQueryable(x => x.PayPeriodStart >= from && x.PayPeriodEnd <= to && x.IsPosted).ToListAsync(token);

        if (groupBy == "client")
        {
            var clientIds = payrolls.Where(x => x.ClientId.HasValue).Select(x => x.ClientId!.Value).Distinct().ToList();
            var clientMap = (await Context.Clients.AsNoTracking()
                .Where(x => clientIds.Contains(x.Id)).ToListAsync(token))
                .ToDictionary(x => x.Id);
            return payrolls.GroupBy(x => x.ClientId)
                .Select(g => BuildCostSummary(g.Key, g.Key.HasValue && clientMap.TryGetValue(g.Key.Value, out var c) ? c.Name : "Unassigned", g))
                .OrderByDescending(x => x.TotalNetPay).ToList();
        }

        // Department / Branch — Payroll has neither FK directly, must join through Employee.
        var employeeMap = await LoadEmployeeMapAsync(payrolls.Select(x => x.EmployeeId), token);

        if (groupBy == "branch")
        {
            var branchIds = employeeMap.Values.Where(x => x.BranchId.HasValue).Select(x => x.BranchId!.Value).Distinct().ToList();
            var branchMap = (await Context.Branches.AsNoTracking()
                .Where(x => branchIds.Contains(x.Id)).ToListAsync(token))
                .ToDictionary(x => x.Id);
            return payrolls.GroupBy(x => employeeMap.TryGetValue(x.EmployeeId, out var e) ? e.BranchId : null)
                .Select(g => BuildCostSummary(g.Key, g.Key.HasValue && branchMap.TryGetValue(g.Key.Value, out var b) ? b.Name : "Unassigned", g))
                .OrderByDescending(x => x.TotalNetPay).ToList();
        }

        // default: department
        var deptIds = employeeMap.Values.Where(x => x.DepartmentId.HasValue).Select(x => x.DepartmentId!.Value).Distinct().ToList();
        var deptMap = (await Context.Departments.AsNoTracking()
            .Where(x => deptIds.Contains(x.Id)).ToListAsync(token))
            .ToDictionary(x => x.Id);
        return payrolls.GroupBy(x => employeeMap.TryGetValue(x.EmployeeId, out var e) ? e.DepartmentId : null)
            .Select(g => BuildCostSummary(g.Key, g.Key.HasValue && deptMap.TryGetValue(g.Key.Value, out var d) ? d.Name : "Unassigned", g))
            .OrderByDescending(x => x.TotalNetPay).ToList();
    }

    private static CostSummaryModel BuildCostSummary(Guid? groupId, string groupName, IEnumerable<Payroll> rows)
    {
        var list = rows.ToList();
        return new CostSummaryModel
        {
            GroupId = groupId,
            GroupName = groupName,
            EmployeeCount = list.Select(x => x.EmployeeId).Distinct().Count(),
            TotalBasicPay = list.Sum(x => x.BasicPay),
            TotalGrossIncome = list.Sum(x => x.GrossIncome),
            TotalDeductions = list.Sum(x => x.TotalDeductions),
            TotalNetPay = list.Sum(x => x.NetPay),
            EmployerContributionsCost = list.Sum(x =>
                x.EmployerSSSContribution + x.EmployerPhilHealthContribution +
                x.EmployerPagIbigContribution + x.EmployerECContribution),
        };
    }

    public async Task<List<YtdPayrollSummaryModel>> GetYtdSummaryAsync(int year, Guid? employeeId, CancellationToken token)
    {
        var rows = await GetQueryable(x => x.PayPeriodStart.Year == year && x.IsPosted && (employeeId == null || x.EmployeeId == employeeId)).ToListAsync(token);
        var employeeMap = await LoadEmployeeMapAsync(rows.Select(x => x.EmployeeId), token);

        return rows.GroupBy(x => x.EmployeeId).Select(g =>
        {
            employeeMap.TryGetValue(g.Key, out var e);
            return new YtdPayrollSummaryModel
            {
                EmployeeId = g.Key,
                EmployeeNo = e?.EmployeeNo ?? "",
                FullName = e.FullName(),
                Year = year,
                TotalBasicPay = g.Sum(x => x.BasicPay),
                TotalOvertimePay = g.Sum(x => x.OvertimePay),
                TotalHolidayPay = g.Sum(x => x.HolidayPay),
                TotalAllowances = g.Sum(x => x.TotalRegularAllowances),
                TotalOtherIncome = g.Sum(x => x.TotalOtherIncome),
                TotalGrossIncome = g.Sum(x => x.GrossIncome),
                TotalSSS = g.Sum(x => x.SSSContribution),
                TotalPhilHealth = g.Sum(x => x.PhilHealthContribution),
                TotalPagIbig = g.Sum(x => x.PagIbigContribution),
                TotalWithholdingTax = g.Sum(x => x.WithholdingTax),
                TotalOtherDeductions = g.Sum(x => x.OtherDeductions),
                TotalDeductions = g.Sum(x => x.TotalDeductions),
                TotalNetPay = g.Sum(x => x.NetPay),
            };
        }).OrderBy(x => x.FullName).ToList();
    }

    // Standard PH formula: total Basic Pay earned in the calendar year / 12. Reflects
    // whatever Payroll rows exist for that employee in the year — not specially prorated
    // for employees hired/separated mid-year beyond that. Filters on PostingPeriod (not
    // PayPeriodStart) to match GetAlphalistAsync/GetMonthlyRemittanceReturnAsync's
    // BIR-year-boundary convention, and excludes PayrollType.ThirteenthMonth rows so a
    // year's own 13th month payout can never fold into a later year's calculation.
    // asOfDate, when given, bounds BasicPay/Special Bonuses to what was actually earned up to
    // that date instead of the full calendar year — PD 851 self-prorates the resulting
    // ThirteenthMonthPay simply by summing less. Used by Last Pay's prorated 13th month
    // component (PayrollProcessorService.GenerateLastPayAsync); the regular 13th month run
    // and reports pass none and keep today's full-year behavior.
    public async Task<List<ThirteenthMonthModel>> GetThirteenthMonthAsync(int year, CancellationToken token, DateOnly? asOfDate = null)
    {
        var rows = await GetQueryable(x =>
                x.PostingPeriod.Year == year && x.IsPosted && x.PayrollType == PayrollType.Regular &&
                (asOfDate == null || x.PostingPeriod <= asOfDate))
            .ToListAsync(token);
        var employeeMap = await LoadEmployeeMapAsync(rows.Select(x => x.EmployeeId), token);

        // This year's own 13th month payout row, if generated — used to surface a
        // released/unreleased Status per employee (NotGenerated/Draft/Posted), independent
        // of the `rows` query above (which deliberately excludes ThirteenthMonth rows so a
        // payout can never fold into its own entitlement calculation).
        var thirteenthMonthRuns = await GetQueryable(x =>
                x.PayrollType == PayrollType.ThirteenthMonth && x.PayPeriodStart.Year == year)
            .Select(x => new { x.Id, x.EmployeeId, x.IsPosted, x.NetPay })
            .ToListAsync(token);
        var runByEmployee = thirteenthMonthRuns
            .GroupBy(x => x.EmployeeId)
            .ToDictionary(g => g.Key, g => g.First());

        return rows.GroupBy(x => x.EmployeeId).Select(g =>
        {
            employeeMap.TryGetValue(g.Key, out var e);
            var totalBasic = g.Sum(x => x.BasicPay);
            var totalSpecialBonuses = g.Sum(x => x.TotalBonuses);
            runByEmployee.TryGetValue(g.Key, out var run);
            return new ThirteenthMonthModel
            {
                EmployeeId = g.Key,
                EmployeeNo = e?.EmployeeNo ?? "",
                FullName = e.FullName(),
                Year = year,
                TotalBasicPayForYear = totalBasic,
                ThirteenthMonthPay = totalBasic / 12,
                TotalSpecialBonusesForYear = totalSpecialBonuses,
                Status = run == null ? "NotGenerated" : run.IsPosted ? "Posted" : "Draft",
                NetPay = run?.NetPay,
                PayrollId = run?.Id,
            };
        }).OrderBy(x => x.FullName).ToList();
    }

    // BIR 1601-C's actual return figures for one posting period. Filtered by PostingPeriod
    // (the BIR-specific reporting-period date, independently configurable from
    // PayPeriodStart/End via WTaxCrossMonthCreditPolicy) — not PayPeriodStart/End — to match
    // how WTaxContribution.PayrollDate (and therefore the WTax remittance report) is already
    // credited. See Payroll.PostingPeriod's doc comment.
    // BIR 1601-C — see MonthlyRemittanceReturnModel/MonthlyRemittanceReturnEmployeeModel for
    // the exact Line 15/16A/16B/16C/17/18/19 mapping. amendedReturn is a pass-through filing
    // declaration, not derived from payroll data.
    public async Task<(MonthlyRemittanceReturnModel Summary, List<MonthlyRemittanceReturnEmployeeModel> Employees)>
        GetMonthlyRemittanceReturnAsync(DateOnly from, DateOnly to, bool amendedReturn, CancellationToken token)
    {
        var regularRows = await GetQueryable(x =>
                x.PayrollType == PayrollType.Regular && x.IsPosted &&
                x.PostingPeriod >= from && x.PostingPeriod <= to)
            .ToListAsync(token);

        // 13th month payouts use NonTaxableBenefits/TaxableBenefits, not TaxableIncome — a
        // different field pair than regular runs, so they're loaded and summed separately.
        var thirteenthMonthRows = await GetQueryable(x =>
                x.PayrollType == PayrollType.ThirteenthMonth && x.IsPosted &&
                x.PostingPeriod >= from && x.PostingPeriod <= to)
            .ToListAsync(token);

        var employeeIds = regularRows.Select(x => x.EmployeeId)
            .Concat(thirteenthMonthRows.Select(x => x.EmployeeId))
            .Distinct().ToList();

        var employeeMap = await LoadEmployeeMapAsync(employeeIds, token);

        var branchIds = employeeMap.Values.Where(x => x.BranchId.HasValue).Select(x => x.BranchId!.Value).Distinct().ToList();
        var branchMap = (await Context.Branches.AsNoTracking()
            .Where(x => branchIds.Contains(x.Id)).ToListAsync(token))
            .ToDictionary(x => x.Id);

        var minimumWageRates = await Context.MinimumWageRates.AsNoTracking().ToListAsync(token);

        // Hazard Pay rides the existing generic Other Income mechanism — any category flagged
        // IsHazardPay flows into Gross/Taxable Income normally already; here we just isolate
        // its per-employee amount for the period from the same per-period ledger every other
        // allowance already posts to.
        var hazardPayIncomeIds = await Context.Allowances.AsNoTracking()
            .Where(x => x.IsHazardPay)
            .Select(x => x.Id)
            .ToListAsync(token);
        var hazardPayByEmployee = hazardPayIncomeIds.Count == 0
            ? new Dictionary<Guid, decimal>()
            : (await Context.OtherIncomeApplicationDetails.AsNoTracking()
                .Where(x => hazardPayIncomeIds.Contains(x.IncomeId) && x.Date >= from && x.Date <= to)
                .ToListAsync(token))
                .GroupBy(x => x.EmployeeId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        decimal? ResolveRegionRate(string? regionCode, DateOnly asOf) =>
            string.IsNullOrEmpty(regionCode)
                ? null
                : minimumWageRates
                    .Where(r => r.RegionCode == regionCode && r.EffectiveDate <= asOf)
                    .OrderByDescending(r => r.EffectiveDate)
                    .Select(r => (decimal?)r.DailyRate)
                    .FirstOrDefault();

        var regularByEmployee = regularRows.GroupBy(x => x.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());
        var thirteenthByEmployee = thirteenthMonthRows.GroupBy(x => x.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());

        var employees = new List<MonthlyRemittanceReturnEmployeeModel>();
        foreach (var employeeId in employeeIds)
        {
            employeeMap.TryGetValue(employeeId, out var employee);
            regularByEmployee.TryGetValue(employeeId, out var regRows);
            thirteenthByEmployee.TryGetValue(employeeId, out var tmRows);
            regRows ??= new List<Payroll>();
            tmRows ??= new List<Payroll>();

            var regionCode = employee?.BranchId.HasValue == true && branchMap.TryGetValue(employee.BranchId!.Value, out var branch)
                ? branch.RegionCode
                : null;

            // MWE status is assessed from the employee's most recent regular row in the
            // period, against the region rate effective as of that same row's period — not
            // stored on Employee, so a later wage-rate change never retroactively reclassifies
            // an already-filed period.
            var latestRow = regRows.OrderByDescending(x => x.PostingPeriod).FirstOrDefault();
            var regionRate = latestRow != null ? ResolveRegionRate(regionCode, latestRow.PostingPeriod) : null;
            // Has payroll data to check but no Branch/Region/MinimumWageRate to check it
            // against — defaulted to non-MWE below, but flagged so it's never silent.
            var isUnclassified = latestRow != null && regionRate == null;
            var isMWE = latestRow != null && regionRate.HasValue && latestRow.DailyRate <= regionRate.Value;

            hazardPayByEmployee.TryGetValue(employeeId, out var hazardPay);

            var gross = regRows.Sum(x => x.GrossIncome) + tmRows.Sum(x => x.NonTaxableBenefits + x.TaxableBenefits);
            var line16A = isMWE ? regRows.Sum(x => x.BasicPay) : 0m;
            var line16B = isMWE
                ? regRows.Sum(x => x.HolidayPay + x.OvertimePay + x.NightDifferentialPay + x.NightDifferentialOTPay) + hazardPay
                : 0m;
            // Mandatory contributions + de minimis apply to every employee, MWE or not; the
            // 13th month exempt portion is already ceiling-capped by ComputeThirteenthMonthTaxSplit.
            var line16C = regRows.Sum(x => x.SSSContribution + x.PhilHealthContribution + x.PagIbigContribution + x.TotalDeminimises)
                + tmRows.Sum(x => x.NonTaxableBenefits);
            var taxWithheld = regRows.Sum(x => x.WithholdingTax) + tmRows.Sum(x => x.WithholdingTax);

            employees.Add(new MonthlyRemittanceReturnEmployeeModel
            {
                EmployeeId = employeeId,
                EmployeeNo = employee?.EmployeeNo ?? "",
                FullName = employee.FullName(),
                IsMinimumWageEarner = isMWE,
                AtcCode = isMWE ? "KR020" : "KR010",
                GrossCompensation = gross,
                StatutoryMinimumWage = line16A,
                MWEPremiumPay = line16B,
                OtherNonTaxable = line16C,
                TaxableCompensation = gross - (line16A + line16B + line16C),
                TaxWithheld = taxWithheld,
                IsUnclassified = isUnclassified,
            });
        }

        var ordered = employees.OrderBy(x => x.FullName).ToList();
        var summary = SummarizeMonthlyRemittanceReturn(ordered, from, to, amendedReturn);
        return (summary, ordered);
    }

    // internal (not private), static — pure aggregation over already-fetched rows, testable
    // without a DB. Spec Validations 1/2 (Line 17 = 16A+16B+16C, Line 18 = 15-17) hold by
    // construction here rather than needing a runtime check.
    internal static MonthlyRemittanceReturnModel SummarizeMonthlyRemittanceReturn(
        List<MonthlyRemittanceReturnEmployeeModel> employees, DateOnly from, DateOnly to, bool amendedReturn)
    {
        var line15 = employees.Sum(x => x.GrossCompensation);
        var line16A = employees.Sum(x => x.StatutoryMinimumWage);
        var line16B = employees.Sum(x => x.MWEPremiumPay);
        var line16C = employees.Sum(x => x.OtherNonTaxable);
        var line17 = line16A + line16B + line16C;
        var line18 = line15 - line17;
        var line19 = employees.Sum(x => x.TaxWithheld);

        return new MonthlyRemittanceReturnModel
        {
            PeriodFrom = from,
            PeriodTo = to,
            AmendedReturn = amendedReturn,
            EmployeeCount = employees.Count,
            Line15_TotalCompensation = line15,
            Line16A_StatutoryMinimumWage = line16A,
            Line16B_MWEPremiumPay = line16B,
            Line16C_OtherNonTaxable = line16C,
            Line17_TotalNonTaxable = line17,
            Line18_TaxableCompensation = line18,
            Line19_TaxWithheld = line19,
            HasUnwithheldTaxWarning = line18 > 0 && line19 == 0,
            UnclassifiedEmployeeCount = employees.Count(x => x.IsUnclassified),
        };
    }

    // BIR Alphalist — one row per employee for the year. TaxableIncome/NonTaxableIncome only
    // exist on Payroll rows generated after that migration landed (see Payroll.cs); earlier
    // rows read as 0 here.
    public async Task<List<AlphalistEntryModel>> GetAlphalistAsync(int year, CancellationToken token)
    {
        var rows = await GetQueryable(x => x.PostingPeriod.Year == year && x.IsPosted).ToListAsync(token);
        var employeeMap = await LoadEmployeeMapAsync(rows.Select(x => x.EmployeeId), token);

        return rows.GroupBy(x => x.EmployeeId).Select(g =>
        {
            employeeMap.TryGetValue(g.Key, out var e);
            return new AlphalistEntryModel
            {
                EmployeeId = g.Key,
                EmployeeNo = e?.EmployeeNo ?? "",
                FullName = e.FullName(),
                TIN = e?.TIN ?? "",
                Year = year,
                GrossCompensation = g.Sum(x => x.GrossIncome),
                NonTaxableCompensation = g.Sum(x => x.NonTaxableIncome),
                TaxableCompensation = g.Sum(x => x.TaxableIncome),
                ThirteenthMonthPay = g.Sum(x => x.BasicPay) / 12,
                TotalSSS = g.Sum(x => x.SSSContribution),
                TotalPhilHealth = g.Sum(x => x.PhilHealthContribution),
                TotalPagIbig = g.Sum(x => x.PagIbigContribution),
                TotalTaxWithheld = g.Sum(x => x.WithholdingTax),
            };
        }).OrderBy(x => x.FullName).ToList();
    }

    // Single-employee version of the Alphalist row, plus the identification fields a 2316
    // certificate needs — null if the employee has no posted payroll for that year.
    public async Task<Bir2316Model?> Get2316DataAsync(Guid employeeId, int year, CancellationToken token)
    {
        var rows = await GetQueryable(x => x.EmployeeId == employeeId && x.PostingPeriod.Year == year && x.IsPosted).ToListAsync(token);
        if (rows.Count == 0) return null;

        var employee = await _employeeService.GetFullByIdAsync(employeeId, token);
        return new Bir2316Model
        {
            EmployeeId = employeeId,
            EmployeeNo = employee?.EmployeeNo ?? "",
            FullName = employee?.FullName ?? "",
            TIN = employee?.TIN ?? "",
            RDOCode = employee?.RDOCode ?? "",
            Address = string.Join(", ", new[] { employee?.Address1, employee?.Address2 }
                .Where(s => !string.IsNullOrWhiteSpace(s))),
            CivilStatus = employee?.CivilStatus ?? "",
            Year = year,
            GrossCompensation = rows.Sum(x => x.GrossIncome),
            NonTaxableCompensation = rows.Sum(x => x.NonTaxableIncome),
            TaxableCompensation = rows.Sum(x => x.TaxableIncome),
            ThirteenthMonthPay = rows.Sum(x => x.BasicPay) / 12,
            TotalSSS = rows.Sum(x => x.SSSContribution),
            TotalPhilHealth = rows.Sum(x => x.PhilHealthContribution),
            TotalPagIbig = rows.Sum(x => x.PagIbigContribution),
            TotalTaxWithheld = rows.Sum(x => x.WithholdingTax),
        };
    }

    // OneTime, employer-advanced government payouts awaiting/undergoing SSS-style
    // reimbursement — see LeaveApplication.EmployerAdvancesPayment/ReimbursementStatus.
    // Direct-deposit payouts are excluded: the employer never advanced that money, so there's
    // nothing for it to be reimbursed for. Sourced straight from LeaveApplication (not posted
    // Payroll rows, unlike this service's other reports) since the claim exists independently
    // of whether this run's payroll has been posted yet.
    public async Task<List<ReimbursementListModel>> GetReimbursementListAsync(DateOnly from, DateOnly to, CancellationToken token)
    {
        var apps = await Context.leaveApplications
            .Include(x => x.Leave)
            .Where(x =>
                x.PayoutMode == PayoutMode.OneTime &&
                x.ApprovalStatus == ApprovalStatus.Approved &&
                x.GovernmentAmount != null && x.GovernmentAmount > 0 &&
                x.ReleasePayrollDate != null &&
                x.ReleasePayrollDate >= from && x.ReleasePayrollDate <= to)
            .AsNoTracking()
            .ToListAsync(token);

        var employerAdvanced = apps
            .Where(x => x.EmployerAdvancesPayment ?? x.Leave.EmployerAdvancesPayment)
            .ToList();

        var employeeMap = await LoadEmployeeMapAsync(employerAdvanced.Select(x => x.EmployeeId), token);

        return employerAdvanced.Select(a =>
        {
            employeeMap.TryGetValue(a.EmployeeId, out var e);
            return new ReimbursementListModel
            {
                LeaveApplicationId = a.Id,
                EmployeeId = a.EmployeeId,
                EmployeeNo = e?.EmployeeNo ?? "",
                FullName = e.FullName(),
                LeaveDescription = a.Leave.Description,
                LeaveDateFrom = a.LeaveDateFrom,
                LeaveDateTo = a.LeaveDateTo,
                ReleasePayrollDate = a.ReleasePayrollDate,
                GovernmentAmount = a.GovernmentAmount ?? 0,
                Status = a.ReimbursementStatus,
                FiledDate = a.ReimbursementFiledDate,
                ReceivedDate = a.ReimbursementReceivedDate,
                ReferenceNo = a.ReimbursementReferenceNo,
            };
        }).OrderBy(x => x.FullName).ToList();
    }

    private async Task<Dictionary<Guid, Employee>> LoadEmployeeMapAsync(IEnumerable<Guid> employeeIds, CancellationToken token)
    {
        var employees = await _employeeService.FindByIds(employeeIds.Distinct().ToList(), token);
        return employees.ToDictionary(x => x.Id);
    }
}
