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
    // for employees hired/separated mid-year beyond that.
    public async Task<List<ThirteenthMonthModel>> GetThirteenthMonthAsync(int year, CancellationToken token)
    {
        var rows = await GetQueryable(x => x.PayPeriodStart.Year == year && x.IsPosted).ToListAsync(token);
        var employeeMap = await LoadEmployeeMapAsync(rows.Select(x => x.EmployeeId), token);

        return rows.GroupBy(x => x.EmployeeId).Select(g =>
        {
            employeeMap.TryGetValue(g.Key, out var e);
            var totalBasic = g.Sum(x => x.BasicPay);
            return new ThirteenthMonthModel
            {
                EmployeeId = g.Key,
                EmployeeNo = e?.EmployeeNo ?? "",
                FullName = e.FullName(),
                Year = year,
                TotalBasicPayForYear = totalBasic,
                ThirteenthMonthPay = totalBasic / 12,
            };
        }).OrderBy(x => x.FullName).ToList();
    }

    // BIR 1601-C's actual return figures for one posting period. Filtered by PostingPeriod
    // (the BIR-specific reporting-period date, independently configurable from
    // PayPeriodStart/End via WTaxCrossMonthCreditPolicy) — not PayPeriodStart/End — to match
    // how WTaxContribution.PayrollDate (and therefore the WTax remittance report) is already
    // credited. See Payroll.PostingPeriod's doc comment.
    public async Task<MonthlyRemittanceReturnModel> GetMonthlyRemittanceReturnAsync(DateOnly from, DateOnly to, CancellationToken token)
    {
        var rows = await GetQueryable(x => x.PostingPeriod >= from && x.PostingPeriod <= to && x.IsPosted).ToListAsync(token);
        return new MonthlyRemittanceReturnModel
        {
            PeriodFrom = from,
            PeriodTo = to,
            EmployeeCount = rows.Select(x => x.EmployeeId).Distinct().Count(),
            TotalTaxableCompensation = rows.Sum(x => x.TaxableIncome),
            TotalTaxWithheld = rows.Sum(x => x.WithholdingTax),
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

    private async Task<Dictionary<Guid, Employee>> LoadEmployeeMapAsync(IEnumerable<Guid> employeeIds, CancellationToken token)
    {
        var employees = await _employeeService.FindByIds(employeeIds.Distinct().ToList(), token);
        return employees.ToDictionary(x => x.Id);
    }
}
