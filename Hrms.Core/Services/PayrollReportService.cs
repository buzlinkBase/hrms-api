using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Core.Services;

// Cross-cutting payroll reports that don't naturally belong to any single existing
// service — Bank Disbursement, Deduction Ledger, Leave Ledger, Department/Client/Branch Cost
// Summary, Year-to-Date Summary, and 13th Month Pay. All read-only over already-persisted data.
public class PayrollReportService : BaseService<Payroll>
{
    private readonly EmployeeService _employeeService;
    private readonly PayrollOpeningBalanceService _payrollOpeningBalanceService;

    public PayrollReportService(IUnitOfWorkService uow, EmployeeService employeeService, PayrollOpeningBalanceService payrollOpeningBalanceService) : base(uow)
    {
        _employeeService = employeeService;
        _payrollOpeningBalanceService = payrollOpeningBalanceService;
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

    // "Current outstanding balance" for a deduction schedule = the Balance of the highest-
    // RecordOrder row (the most recently-scheduled still-unpaid installment) among that
    // (Employee, Deduction) pair's rows with Balance > 0 — DeductionApplicationDetail rows are
    // an amortization schedule (one row per installment), not one row per application. Not
    // filtered to any single DeductionType category — covers Loans, Cash Advances, Cash Bond,
    // and any other installment-based deduction alike.
    public async Task<List<DeductionLedgerModel>> GetDeductionLedgerAsync(DateOnly asOf, CancellationToken token)
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
            var deductionTypeName = deduction?.CategoryId != null && categoryMap.TryGetValue(deduction.CategoryId.Value, out var cat)
                ? cat.Name : "";
            employeeMap.TryGetValue(x.EmployeeId, out var e);
            return new DeductionLedgerModel
            {
                EmployeeId = x.EmployeeId,
                EmployeeNo = e?.EmployeeNo ?? "",
                FullName = e.FullName(),
                DeductionId = x.DeductionId,
                DeductionTypeName = deductionTypeName,
                DeductionName = deduction?.Name ?? "",
                TotalPrincipal = app?.TotalPrincipal ?? 0,
                InterestRate = app?.InterestRate ?? 0,
                StartDate = app?.StartDate ?? default,
                EndDate = app?.EndDate ?? default,
                CurrentBalance = x.Balance,
            };
        }).OrderBy(x => x.FullName).ToList();
    }

    // Cash Bond tracking — one row per employee's Cash Bond DeductionApplication (identified by
    // DeductionType.Code == "CASHBOND"), summing every scheduled installment's Amount up to
    // asOf as "collected so far" against the application's own TotalPrincipal as the target.
    // Optionally scoped to a subset of employees (used by LastPayrollService's informational
    // status line). Same "Balance never decrements" caveat as GetLoanLedgerAsync used to
    // document doesn't apply here since this sums Amount directly rather than reading Balance.
    public async Task<List<CashBondReportModel>> GetCashBondReportAsync(DateOnly asOf, CancellationToken token, List<Guid>? employeeIds = null)
    {
        var cashBondTypeId = await Context.DeductionTypes.AsNoTracking()
            .Where(x => x.Code == "CASHBOND")
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(token);
        if (cashBondTypeId == null) return [];

        var cashBondDeductionIds = await Context.Deductions.AsNoTracking()
            .Where(x => x.CategoryId == cashBondTypeId)
            .Select(x => x.Id)
            .ToListAsync(token);
        if (cashBondDeductionIds.Count == 0) return [];

        var dueRows = await Context.DeductionApplicationDetails.AsNoTracking()
            .Where(x => cashBondDeductionIds.Contains(x.DeductionId) && x.Date <= asOf
                && (employeeIds == null || employeeIds.Contains(x.EmployeeId)))
            .ToListAsync(token);
        if (dueRows.Count == 0) return [];

        var applicationIds = dueRows.Select(x => x.ApplicationId).Distinct().ToList();
        var applicationMap = (await Context.DeductionApplications.AsNoTracking()
            .Where(x => applicationIds.Contains(x.Id))
            .ToListAsync(token))
            .ToDictionary(x => x.Id);

        var employeeMap = await LoadEmployeeMapAsync(dueRows.Select(x => x.EmployeeId), token);

        return dueRows
            .Where(x => applicationMap.ContainsKey(x.ApplicationId))
            .GroupBy(x => new { x.EmployeeId, x.DeductionId, x.ApplicationId })
            .Select(g =>
            {
                var app = applicationMap[g.Key.ApplicationId];
                employeeMap.TryGetValue(g.Key.EmployeeId, out var e);
                var totalCollected = g.Sum(x => x.Amount);
                return new CashBondReportModel
                {
                    EmployeeId = g.Key.EmployeeId,
                    EmployeeNo = e?.EmployeeNo ?? "",
                    FullName = e.FullName(),
                    DeductionId = g.Key.DeductionId,
                    ApplicationId = g.Key.ApplicationId,
                    TargetAmount = app.TotalPrincipal,
                    TotalCollected = totalCollected,
                    Remaining = ComputeCashBondRemaining(app.TotalPrincipal, totalCollected),
                    StartDate = app.StartDate,
                    EndDate = app.EndDate,
                    ApprovalStatus = app.ApprovalStatus,
                };
            })
            .OrderBy(x => x.FullName)
            .ToList();
    }

    // Floors at 0 rather than going negative once collected meets or exceeds the target --
    // "remaining to collect" doesn't have a meaningful negative value for a monitoring report.
    // internal (not private) so this is testable without a database -- see Hrms.Core's
    // InternalsVisibleTo for hrms.test.
    internal static decimal ComputeCashBondRemaining(decimal target, decimal totalCollected) =>
        Math.Max(0, target - totalCollected);

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

        // Fold in each employee's pre-system-cutover Opening Balance (see
        // PayrollOpeningBalance) so a company onboarding mid-year isn't understated for the
        // months before this system existed. Employees present only via an Opening Balance
        // (no Payroll rows yet this year) still get a row, not just the ones with Payroll data.
        var openingBalances = await _payrollOpeningBalanceService.FindAllByYearAsync(year, token);
        if (employeeId != null)
        {
            openingBalances = openingBalances.Where(x => x.Key == employeeId).ToDictionary(x => x.Key, x => x.Value);
        }

        var payrollGroups = rows.GroupBy(x => x.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());
        var employeeIds = payrollGroups.Keys.Union(openingBalances.Keys).ToList();
        var employeeMap = await LoadEmployeeMapAsync(employeeIds, token);

        return employeeIds.Select(id =>
        {
            employeeMap.TryGetValue(id, out var e);
            payrollGroups.TryGetValue(id, out var g);
            g ??= new List<Payroll>();
            openingBalances.TryGetValue(id, out var ob);
            return new YtdPayrollSummaryModel
            {
                EmployeeId = id,
                EmployeeNo = e?.EmployeeNo ?? "",
                FullName = e.FullName(),
                Year = year,
                TotalBasicPay = g.Sum(x => x.BasicPay) + (ob?.BasicPay ?? 0),
                TotalOvertimePay = g.Sum(x => x.OvertimePay) + (ob?.OvertimePay ?? 0),
                TotalHolidayPay = g.Sum(x => x.HolidayPay) + (ob?.HolidayPay ?? 0),
                TotalAllowances = g.Sum(x => x.TotalRegularAllowances) + (ob?.Allowances ?? 0),
                TotalOtherIncome = g.Sum(x => x.TotalOtherIncome) + (ob?.OtherIncome ?? 0),
                TotalGrossIncome = g.Sum(x => x.GrossIncome) + (ob?.GrossIncome ?? 0),
                TotalSSS = g.Sum(x => x.SSSContribution) + (ob?.SSSContribution ?? 0),
                TotalPhilHealth = g.Sum(x => x.PhilHealthContribution) + (ob?.PhilHealthContribution ?? 0),
                TotalPagIbig = g.Sum(x => x.PagIbigContribution) + (ob?.PagIbigContribution ?? 0),
                TotalWithholdingTax = g.Sum(x => x.WithholdingTax) + (ob?.WithholdingTax ?? 0),
                TotalOtherDeductions = g.Sum(x => x.OtherDeductions) + (ob?.OtherDeductions ?? 0),
                TotalDeductions = g.Sum(x => x.TotalDeductions) + (ob?.TotalDeductions ?? 0),
                TotalNetPay = g.Sum(x => x.NetPay) + (ob?.NetPay ?? 0),
            };
        }).OrderBy(x => x.FullName).ToList();
    }

    // Standard PH formula: total Basic Pay earned in the calendar year / 12. Reflects
    // whatever Payroll rows exist for that employee in the year — not specially prorated
    // for employees hired/separated mid-year beyond that. The YEAR filter uses PostingPeriod
    // (not PayPeriodStart) to match GetAlphalistAsync/GetMonthlyRemittanceReturnAsync's
    // BIR-year-boundary convention, and excludes PayrollType.ThirteenthMonth rows so a
    // year's own 13th month payout can never fold into a later year's calculation.
    // asOfDate, when given, bounds BasicPay/Special Bonuses to what was actually earned up to
    // that date instead of the full calendar year — PD 851 self-prorates the resulting
    // ThirteenthMonthPay simply by summing less. Used by Last Pay's prorated 13th month
    // component (LastPayrollService.GenerateAsync); the regular 13th month run and reports
    // pass none and keep today's full-year behavior.
    //
    // The asOfDate cutoff deliberately compares against PayPeriodStart, not PostingPeriod:
    // PostingPeriod is a BIR statutory-credit date (resolved per CrossMonthStatutoryCreditPolicy,
    // often the cutoff's END date or even the pay date — see StatutoryCreditDateResolver), which
    // can legitimately fall AFTER an employee's separation date even though the cutoff's basic
    // pay was already correctly capped to their actual last day worked (attendance can't exist
    // past separation). Gating on PostingPeriod would wrongly drop an employee's own final,
    // already-posted partial cutoff out of their Last Pay's 13th month proration whenever they
    // separated mid-cutoff. A cutoff that STARTED on or before asOfDate necessarily contains at
    // least some pre-separation work, so it always belongs in the proration.
    // employeeId, when given, scopes every query to that one employee instead of computing for
    // the whole company — used by MeController.GetMy13thMonth so a self-service lookup doesn't
    // pay the cost of (and can never accidentally leak) every other employee's figures.
    public async Task<List<ThirteenthMonthModel>> GetThirteenthMonthAsync(int year, CancellationToken token, DateOnly? asOfDate = null, Guid? employeeId = null)
    {
        var rows = await GetQueryable(x =>
                x.PostingPeriod.Year == year && x.IsPosted && x.PayrollType == PayrollType.Regular &&
                (asOfDate == null || x.PayPeriodStart <= asOfDate) &&
                (employeeId == null || x.EmployeeId == employeeId))
            .ToListAsync(token);

        // Opening Balance (pre-system-cutover) BasicPay/Bonuses are always fully in the past
        // relative to any asOfDate within the same year, so they're folded in unconditionally
        // regardless of the asOfDate slice — see PayrollOpeningBalance's doc comment.
        var openingBalances = await _payrollOpeningBalanceService.FindAllByYearAsync(year, token);
        if (employeeId != null)
        {
            openingBalances = openingBalances.Where(x => x.Key == employeeId).ToDictionary(x => x.Key, x => x.Value);
        }

        // This year's own 13th month payout row, if generated — used to surface a
        // released/unreleased Status per employee (NotGenerated/Draft/Posted), independent
        // of the `rows` query above (which deliberately excludes ThirteenthMonth rows so a
        // payout can never fold into its own entitlement calculation).
        var thirteenthMonthRuns = await GetQueryable(x =>
                x.PayrollType == PayrollType.ThirteenthMonth && x.PayPeriodStart.Year == year &&
                (employeeId == null || x.EmployeeId == employeeId))
            .Select(x => new { x.Id, x.EmployeeId, x.IsPosted, x.NetPay, x.AcknowledgedAt })
            .ToListAsync(token);
        var runByEmployee = thirteenthMonthRuns
            .GroupBy(x => x.EmployeeId)
            .ToDictionary(g => g.Key, g => g.First());

        var payrollGroups = rows.GroupBy(x => x.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());
        var employeeIds = payrollGroups.Keys.Union(openingBalances.Keys).ToList();
        var employeeMap = await LoadEmployeeMapAsync(employeeIds, token);

        return employeeIds.Select(id =>
        {
            employeeMap.TryGetValue(id, out var e);
            payrollGroups.TryGetValue(id, out var g);
            g ??= new List<Payroll>();
            openingBalances.TryGetValue(id, out var ob);
            var totalBasic = g.Sum(x => x.BasicPay) + (ob?.BasicPay ?? 0);
            var totalSpecialBonuses = g.Sum(x => x.TotalBonuses) + (ob?.Bonuses ?? 0);
            runByEmployee.TryGetValue(id, out var run);
            return new ThirteenthMonthModel
            {
                EmployeeId = id,
                EmployeeNo = e?.EmployeeNo ?? "",
                FullName = e.FullName(),
                Year = year,
                TotalBasicPayForYear = totalBasic,
                ThirteenthMonthPay = totalBasic / 12,
                TotalSpecialBonusesForYear = totalSpecialBonuses,
                Status = run == null ? "NotGenerated" : run.IsPosted ? "Posted" : "Draft",
                NetPay = run?.NetPay,
                PayrollId = run?.Id,
                AcknowledgedAt = run?.AcknowledgedAt,
            };
        }).OrderBy(x => x.FullName).ToList();
    }

    // Year-End Tax Annualization's per-employee RAW annual aggregate (RR 11-2018 §2.79.4) —
    // deliberately not netted/clamped here (see TaxAnnualizationInputModel's doc comment) so
    // TaxAnnualizationService.ComputeAsync can consolidate these with any PriorEmployerTaxRecord
    // before flooring at 0 and running the bracket lookup. Per-row taxable-gross formula
    // mirrors TableWTaxCalculator's own per-period calculation (GrossIncome - SSS.EE - PHIC.EE
    // - HDMF.EE), additionally netting out NonTaxableBenefits (immaterial per-period, matters
    // for an accurate annual figure):
    //   CurrentGrossIncome = Sum(GrossIncome)
    //   CurrentNonTaxableBenefits = Sum(NonTaxableBenefits)
    //   CurrentStatutoryDeductions = Sum(SSSContribution + PhilHealthContribution + PagIbigContribution)
    // Source rows: PostingPeriod.Year == year && IsPosted && PayrollType IN (Regular,
    // ThirteenthMonth, LastPay) — explicitly enumerated (not != YearEndAdjustment) so a future
    // PayrollType is excluded by default rather than silently leaking in. ThirteenthMonth/
    // LastPay rows are INCLUDED (unlike GetThirteenthMonthAsync's own sourcing query, which
    // deliberately excludes ThirteenthMonth to avoid a payout feeding its own entitlement calc)
    // because their TaxableBenefits/WithholdingTax already reflect the
    // ThirteenthMonthExemptionCeiling split applied at payout time — excluding them would
    // undercount both annual taxable income and tax already withheld for the year.
    public async Task<List<TaxAnnualizationInputModel>> GetAnnualTaxAnnualizationInputsAsync(int year, CancellationToken token)
    {
        var rows = await GetQueryable(x =>
                x.PostingPeriod.Year == year && x.IsPosted &&
                (x.PayrollType == PayrollType.Regular || x.PayrollType == PayrollType.ThirteenthMonth || x.PayrollType == PayrollType.LastPay))
            .ToListAsync(token);

        // Fold in pre-system-cutover Opening Balance figures as more "Current" employer data
        // (same employer, just pre-dating this system) — this is why TaxAnnualizationService
        // itself needs no changes: it already treats these five Current* fields as one bucket,
        // regardless of whether they came from real Payroll rows or an Opening Balance.
        var openingBalances = await _payrollOpeningBalanceService.FindAllByYearAsync(year, token);

        var payrollGroups = rows.GroupBy(x => x.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());
        var employeeIds = payrollGroups.Keys.Union(openingBalances.Keys).ToList();
        var employeeMap = await LoadEmployeeMapAsync(employeeIds, token);

        var branchIds = employeeMap.Values.Where(x => x.BranchId.HasValue).Select(x => x.BranchId!.Value).Distinct().ToList();
        var branchMap = (await Context.Branches.AsNoTracking()
            .Where(x => branchIds.Contains(x.Id)).ToListAsync(token))
            .ToDictionary(x => x.Id);

        var minimumWageRates = await Context.MinimumWageRates.AsNoTracking().ToListAsync(token);

        return employeeIds.Select(id =>
        {
            employeeMap.TryGetValue(id, out var employee);
            payrollGroups.TryGetValue(id, out var g);
            g ??= new List<Payroll>();
            openingBalances.TryGetValue(id, out var ob);
            Branch? branch = employee?.BranchId.HasValue == true && branchMap.TryGetValue(employee.BranchId!.Value, out var foundBranch)
                ? foundBranch
                : null;

            // Only Regular-type rows feed MWE detection — ThirteenthMonth/LastPay rows have
            // DailyRate == 0 and would corrupt the "latest row" pick (see
            // MinimumWageEarnerResolver's doc comment). An employee with only an Opening
            // Balance (no Regular row in this system yet) can't be classified from here and
            // falls back to IsUnclassified via the resolver's own empty-list handling.
            var regularRows = g.Where(x => x.PayrollType == PayrollType.Regular).ToList();
            var (isMWE, isUnclassified) = MinimumWageEarnerResolver.IsMinimumWageEarner(
                regularRows, branch?.RegionCode, branch?.WageOrderClass, minimumWageRates);

            var latestGroupRow = g.OrderByDescending(x => x.PostingPeriod).FirstOrDefault();

            return new TaxAnnualizationInputModel
            {
                EmployeeId = id,
                EmployeeNo = employee?.EmployeeNo ?? "",
                FullName = employee.FullName(),
                Year = year,
                PayrollGroupId = latestGroupRow?.PayrollGroupId,
                AreaId = latestGroupRow?.AreaId,
                ClientId = latestGroupRow?.ClientId,
                SalaryType = latestGroupRow?.SalaryType ?? default,
                CurrentGrossIncome = g.Sum(x => x.GrossIncome) + (ob?.GrossIncome ?? 0),
                CurrentNonTaxableBenefits = g.Sum(x => x.NonTaxableBenefits) + (ob?.NonTaxableIncome ?? 0),
                CurrentStatutoryDeductions = g.Sum(x => x.SSSContribution + x.PhilHealthContribution + x.PagIbigContribution)
                    + (ob == null ? 0 : ob.SSSContribution + ob.PhilHealthContribution + ob.PagIbigContribution),
                CurrentWithholdingTaxYTD = g.Sum(x => x.WithholdingTax) + (ob?.WithholdingTax ?? 0),
                CurrentAverageMonthlyNetPay = (g.Sum(x => x.NetPay) + (ob?.NetPay ?? 0)) / 12,
                IsMinimumWageEarner = isMWE,
                IsUnclassified = isUnclassified,
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

        // Year-End Tax Adjustment rows (see TaxAnnualizationService) — per RR 11-2018
        // §2.79.4(B), an under-withheld year-end collection must be remitted with that month's
        // 1601-C, and an over-withheld refund is netted against it. Kept in its own bucket
        // (never merged into regRows) since its DailyRate/BasicPay/etc. are always 0 and would
        // corrupt the MWE "latest regular row" pick above if merged in.
        var yearEndAdjustmentRows = await GetQueryable(x =>
                x.PayrollType == PayrollType.YearEndAdjustment && x.IsPosted &&
                x.PostingPeriod >= from && x.PostingPeriod <= to)
            .ToListAsync(token);

        var employeeIds = regularRows.Select(x => x.EmployeeId)
            .Concat(thirteenthMonthRows.Select(x => x.EmployeeId))
            .Concat(yearEndAdjustmentRows.Select(x => x.EmployeeId))
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

        var regularByEmployee = regularRows.GroupBy(x => x.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());
        var thirteenthByEmployee = thirteenthMonthRows.GroupBy(x => x.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());
        var yeaByEmployee = yearEndAdjustmentRows.GroupBy(x => x.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());

        var employees = new List<MonthlyRemittanceReturnEmployeeModel>();
        foreach (var employeeId in employeeIds)
        {
            employeeMap.TryGetValue(employeeId, out var employee);
            regularByEmployee.TryGetValue(employeeId, out var regRows);
            thirteenthByEmployee.TryGetValue(employeeId, out var tmRows);
            regRows ??= new List<Payroll>();
            tmRows ??= new List<Payroll>();

            Branch? branch = employee?.BranchId.HasValue == true && branchMap.TryGetValue(employee.BranchId!.Value, out var foundBranch)
                ? foundBranch
                : null;
            var regionCode = branch?.RegionCode;
            var wageOrderClass = branch?.WageOrderClass;

            // MWE status is assessed from the employee's most recent regular row in the
            // period, against the region rate effective as of that same row's period — not
            // stored on Employee, so a later wage-rate change never retroactively reclassifies
            // an already-filed period. Shared with TaxAnnualizationService via
            // MinimumWageEarnerResolver so both use the exact same determination.
            var (isMWE, isUnclassified) = MinimumWageEarnerResolver.IsMinimumWageEarner(
                regRows, regionCode, wageOrderClass, minimumWageRates);

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
            yeaByEmployee.TryGetValue(employeeId, out var yeaRows);
            yeaRows ??= new List<Payroll>();
            // Includes any posted Year-End Tax Adjustment row for the period — a positive
            // WithholdingTax there is an additional collection to remit this period, a negative
            // one is a refund netted against it. Both nets correctly into one Sum since every
            // other field on a YearEndAdjustment row (GrossIncome/BasicPay/etc.) is always 0,
            // so it never affects Lines 15/16A/16B/16C above.
            var taxWithheld = regRows.Sum(x => x.WithholdingTax) + tmRows.Sum(x => x.WithholdingTax) + yeaRows.Sum(x => x.WithholdingTax);

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

        // See PayrollOpeningBalance's doc comment — folds pre-system-cutover figures into
        // this full-year BIR return so a mid-year onboarded company isn't understated.
        var openingBalances = await _payrollOpeningBalanceService.FindAllByYearAsync(year, token);

        var payrollGroups = rows.GroupBy(x => x.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());
        var employeeIds = payrollGroups.Keys.Union(openingBalances.Keys).ToList();
        var employeeMap = await LoadEmployeeMapAsync(employeeIds, token);

        return employeeIds.Select(id =>
        {
            employeeMap.TryGetValue(id, out var e);
            payrollGroups.TryGetValue(id, out var g);
            g ??= new List<Payroll>();
            openingBalances.TryGetValue(id, out var ob);
            var obTaxable = ob?.DerivedTaxableIncome ?? 0;
            return new AlphalistEntryModel
            {
                EmployeeId = id,
                EmployeeNo = e?.EmployeeNo ?? "",
                FullName = e.FullName(),
                TIN = e?.TIN ?? "",
                Year = year,
                GrossCompensation = g.Sum(x => x.GrossIncome) + (ob?.GrossIncome ?? 0),
                NonTaxableCompensation = g.Sum(x => x.NonTaxableIncome) + (ob?.NonTaxableIncome ?? 0),
                TaxableCompensation = g.Sum(x => x.TaxableIncome) + obTaxable,
                ThirteenthMonthPay = (g.Sum(x => x.BasicPay) + (ob?.BasicPay ?? 0)) / 12,
                TotalSSS = g.Sum(x => x.SSSContribution) + (ob?.SSSContribution ?? 0),
                TotalPhilHealth = g.Sum(x => x.PhilHealthContribution) + (ob?.PhilHealthContribution ?? 0),
                TotalPagIbig = g.Sum(x => x.PagIbigContribution) + (ob?.PagIbigContribution ?? 0),
                TotalTaxWithheld = g.Sum(x => x.WithholdingTax) + (ob?.WithholdingTax ?? 0),
            };
        }).OrderBy(x => x.FullName).ToList();
    }

    // Single-employee version of the Alphalist row, plus the identification fields a 2316
    // certificate needs — null if the employee has no posted payroll for that year.
    public async Task<Bir2316Model?> Get2316DataAsync(Guid employeeId, int year, CancellationToken token)
    {
        var rows = await GetQueryable(x => x.EmployeeId == employeeId && x.PostingPeriod.Year == year && x.IsPosted).ToListAsync(token);

        // An employee whose only data for the year is a pre-cutover Opening Balance (no
        // Payroll rows posted here yet) still gets a certificate, not a null.
        var openingBalances = await _payrollOpeningBalanceService.FindAllByYearAsync(year, token);
        openingBalances.TryGetValue(employeeId, out var ob);
        if (rows.Count == 0 && ob == null) return null;

        var obTaxable = ob?.DerivedTaxableIncome ?? 0;

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
            GrossCompensation = rows.Sum(x => x.GrossIncome) + (ob?.GrossIncome ?? 0),
            NonTaxableCompensation = rows.Sum(x => x.NonTaxableIncome) + (ob?.NonTaxableIncome ?? 0),
            TaxableCompensation = rows.Sum(x => x.TaxableIncome) + obTaxable,
            ThirteenthMonthPay = (rows.Sum(x => x.BasicPay) + (ob?.BasicPay ?? 0)) / 12,
            TotalSSS = rows.Sum(x => x.SSSContribution) + (ob?.SSSContribution ?? 0),
            TotalPhilHealth = rows.Sum(x => x.PhilHealthContribution) + (ob?.PhilHealthContribution ?? 0),
            TotalPagIbig = rows.Sum(x => x.PagIbigContribution) + (ob?.PagIbigContribution ?? 0),
            TotalTaxWithheld = rows.Sum(x => x.WithholdingTax) + (ob?.WithholdingTax ?? 0),
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
