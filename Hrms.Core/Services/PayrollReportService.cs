using Hrms.Core.Services.UniformAllowance;
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
    private readonly Security.ConfidentialAccessScope? _access;

    // access is optional so direct constructions in tests stay valid; the container supplies it.
    public PayrollReportService(IUnitOfWorkService uow, EmployeeService employeeService, PayrollOpeningBalanceService payrollOpeningBalanceService,
        Security.ConfidentialAccessScope? access = null) : base(uow)
    {
        _employeeService = employeeService;
        _payrollOpeningBalanceService = payrollOpeningBalanceService;
        _access = access;
    }

    // Payroll rows as a source of cash bond deductions. Payroll is a compensation record, hidden for
    // confidential employees from partial clearances -- but the cash bond deducted on it is an
    // Entries figure, so a caller cleared for Entries reads it for those employees too. Callers
    // project the cash bond columns only.
    private IQueryable<Payroll> CashBondPayrolls()
    {
        if (_access is not { Restricted: true }) return Context.Payrolls.AsNoTracking();
        var hidden = _access.HiddenEmployeeIdsFor(ConfidentialDataClass.Entries);
        return Context.IgnoringConfidentiality<Payroll>().AsNoTracking().Where(x => !hidden.Contains(x.EmployeeId));
    }

    // The optional client filter every report here takes: it narrows to employees whose CURRENT client
    // is that one (the same rule the Uniform Allowance ledger uses), applied in SQL as an employee-id
    // subquery. A null clientId means everyone and adds nothing to the query.
    private IQueryable<Guid> ClientEmployeeIds(Guid clientId) =>
        _uow.Repository.FindAll<Employee>().AsNoTracking().Where(x => x.ClientId == clientId).Select(x => x.Id);

    private IQueryable<Payroll> ForClient(IQueryable<Payroll> payrolls, Guid? clientId) =>
        clientId is { } c ? payrolls.Where(x => ClientEmployeeIds(c).Contains(x.EmployeeId)) : payrolls;

    // For reports that fold in per-employee dictionaries (opening balances) after the SQL step.
    private async Task<HashSet<Guid>?> ClientEmployeeSetAsync(Guid? clientId, CancellationToken token) =>
        clientId is { } c ? (await ClientEmployeeIds(c).ToListAsync(token)).ToHashSet() : null;

    private static Dictionary<Guid, T> OnlyClient<T>(Dictionary<Guid, T> map, HashSet<Guid>? clientEmployees) =>
        clientEmployees == null ? map : map.Where(x => clientEmployees.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value);

    public async Task<List<BankDisbursementModel>> GetBankDisbursementAsync(
        DateOnly from, DateOnly to, CancellationToken token, Guid? clientId = null)
    {
        var rows = await ForClient(GetQueryable(x => x.PayPeriodStart >= from && x.PayPeriodEnd <= to && x.IsPosted), clientId)
            .Select(x => new { x.EmployeeId, x.PayPeriodStart, x.PayPeriodEnd, x.NetPay })
            .ToListAsync(token);
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
    public async Task<List<DeductionLedgerModel>> GetDeductionLedgerAsync(DateOnly asOf, CancellationToken token, Guid? clientId = null)
    {
        // The latest open installment per schedule is picked in SQL (no later open row exists),
        // reading only the columns used -- instead of loading every open installment row.
        // A skipped attendance installment is forgiven, not owed, so it never counts as open.
        var details = Context.DeductionApplicationDetails.AsNoTracking().Where(x => !x.IsSkipped);
        if (clientId is { } client) details = details.Where(x => ClientEmployeeIds(client).Contains(x.EmployeeId));
        var latestCandidates = await details
            .Where(x => x.Date <= asOf && x.Balance > 0 &&
                !details.Any(y =>
                    y.EmployeeId == x.EmployeeId && y.DeductionId == x.DeductionId && y.DeductionApplicationId == x.DeductionApplicationId &&
                    y.Date <= asOf && y.Balance > 0 && y.RecordOrder > x.RecordOrder))
            .Select(x => new { x.EmployeeId, x.DeductionId, x.DeductionApplicationId, x.Balance })
            .ToListAsync(token);

        // Two open rows can share the top RecordOrder -- keep one per schedule, as before.
        var latestPerLoan = latestCandidates
            .GroupBy(x => new { x.EmployeeId, x.DeductionId, x.DeductionApplicationId })
            .Select(g => g.First())
            .ToList();

        var applicationIds = latestPerLoan.Select(x => x.DeductionApplicationId).Distinct().ToList();
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
            applicationMap.TryGetValue(x.DeductionApplicationId, out var app);
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

    // Cash Bond tracking — one row per employee: every posted payroll run's CashBondDeduction
    // (see CashBondDeductionPolicy) up to asOf, plus manual entries (CashBondAdjustment, e.g. the
    // opening balance from before this system) dated up to asOf, alongside the employee's current
    // per-run rate. Optionally scoped to a subset of employees (used by LastPayrollService's
    // informational status line, which therefore counts the opening balance too).
    public async Task<List<CashBondReportModel>> GetCashBondReportAsync(
        DateOnly asOf, CancellationToken token, List<Guid>? employeeIds = null, Guid? clientId = null)
    {
        var fromPayroll = await ForClient(CashBondPayrolls(), clientId)
            .Where(x => x.CashBondDeduction > 0 && x.PayPeriodEnd <= asOf
                && (employeeIds == null || employeeIds.Contains(x.EmployeeId)))
            .GroupBy(x => x.EmployeeId)
            .Select(g => new { EmployeeId = g.Key, Collected = g.Sum(x => x.CashBondDeduction), RunsCount = g.Count() })
            .ToDictionaryAsync(x => x.EmployeeId, token);
        var adjustmentRows = Context.CashBondAdjustments.AsNoTracking();
        if (clientId is { } client) adjustmentRows = adjustmentRows.Where(x => ClientEmployeeIds(client).Contains(x.EmployeeId));
        var adjustments = await adjustmentRows
            .Where(x => x.EntryDate <= asOf && (employeeIds == null || employeeIds.Contains(x.EmployeeId)))
            .GroupBy(x => x.EmployeeId)
            .Select(g => new { EmployeeId = g.Key, Net = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.EmployeeId, x => x.Net, token);

        var ids = fromPayroll.Keys.Union(adjustments.Keys).ToList();
        if (ids.Count == 0) return [];
        var employeeMap = await LoadEmployeeMapAsync(ids, token);

        return ids.Select(id =>
        {
            employeeMap.TryGetValue(id, out var e);
            fromPayroll.TryGetValue(id, out var payroll);
            var collected = payroll?.Collected ?? 0m;
            var adjusted = adjustments.GetValueOrDefault(id);
            return new CashBondReportModel
            {
                EmployeeId = id,
                EmployeeNo = e?.EmployeeNo ?? "",
                FullName = e.FullName(),
                CashBondRate = e?.CashBond ?? 0,
                Adjustments = adjusted,
                CollectedFromPayroll = collected,
                TotalCollected = adjusted + collected,
                PayrollRunsCount = payroll?.RunsCount ?? 0,
            };
        })
        .OrderBy(x => x.FullName)
        .ToList();
    }

    // "My Cash Bond" (employee portal) — same source as GetCashBondReportAsync, scoped to one
    // employee, plus the payroll runs and manual entries that make up the total.
    public async Task<MyCashBondModel> GetMyCashBondAsync(Guid employeeId, CancellationToken token)
    {
        var employee = await _employeeService.FineOneAsync(employeeId, token);
        var runs = await Context.Payrolls.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.CashBondDeduction > 0)
            .OrderByDescending(x => x.PayPeriodEnd)
            .Select(x => new MyCashBondRunModel
            {
                PayrollId = x.Id,
                PayPeriodStart = x.PayPeriodStart,
                PayPeriodEnd = x.PayPeriodEnd,
                Amount = x.CashBondDeduction,
            })
            .ToListAsync(token);
        var adjustments = await Context.CashBondAdjustments.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId)
            .OrderByDescending(x => x.EntryDate)
            .Select(x => new MyCashBondAdjustmentModel
            {
                EntryDate = x.EntryDate,
                Amount = x.Amount,
                Particulars = x.Particulars,
            })
            .ToListAsync(token);

        return new MyCashBondModel
        {
            CashBondRate = employee?.CashBond ?? 0,
            TotalCollected = runs.Sum(x => x.Amount) + adjustments.Sum(x => x.Amount),
            Runs = runs,
            Adjustments = adjustments,
        };
    }

    // Manual HR entry against an employee's cash bond collected total -- typically the opening
    // balance of cash bond deducted before this system was adopted. An additive delta +
    // direction, like PayrollService.AdjustRetirementBalanceAsync; a removal is clamped to what
    // has been collected so far, so the total never goes negative.
    public async Task AdjustCashBondAsync(
        Guid employeeId, decimal amount, bool isAddition, string particulars, DateOnly entryDate, CancellationToken token)
    {
        if (amount <= 0)
            throw new InvalidOperationException("Amount must be greater than zero.");
        if (!await _uow.Repository.FindAll<Employee>().AnyAsync(x => x.Id == employeeId, token))
            throw new NotFoundException("Employee not found.");

        var less = 0m;
        if (!isAddition)
        {
            var collected = (await GetCashBondReportAsync(DateOnly.MaxValue, token, [employeeId]))
                .SingleOrDefault()?.TotalCollected ?? 0m;
            less = Math.Min(amount, Math.Max(collected, 0m));
            if (less <= 0)
                throw new InvalidOperationException("This employee has no cash bond collected to remove.");
        }

        Context.CashBondAdjustments.Add(new CashBondAdjustment
        {
            EmployeeId = employeeId,
            EntryDate = entryDate,
            Amount = isAddition ? amount : -less,
            Particulars = particulars.Trim(),
        });
        await CommitChangesAsync(token);
    }

    // Leave credits balances — LeaveCredits already holds the authoritative per-employee,
    // per-leave-type, per-period balance (Granted/Used/Balance/Reserved/AvailableToFile),
    // so this reads it directly rather than re-deriving from the LeaveLedger transaction log.
    public Task<List<LeaveCreditsBalanceModel>> GetLeaveLedgerAsync(int year, CancellationToken token) =>
        GetLeaveLedgerAsync(year, null, token);

    // The optional scope narrows the ledger to employees of a client / area / payroll group /
    // department / branch. Applied in SQL as an employee-id subquery, so an empty scope costs nothing.
    public async Task<List<LeaveCreditsBalanceModel>> GetLeaveLedgerAsync(
        int year, EmployeeScopeFilter? scope, CancellationToken token)
    {
        var credits = Context.LeaveCredits.AsNoTracking().Where(x => x.PeriodYear == year);
        if (scope is { IsEmpty: false })
        {
            var employees = _uow.Repository.FindAll<Employee>().AsNoTracking();
            if (scope.ClientId.HasValue) employees = employees.Where(x => x.ClientId == scope.ClientId);
            if (scope.AreaId.HasValue) employees = employees.Where(x => x.AreaId == scope.AreaId);
            if (scope.PayrollGroupId.HasValue) employees = employees.Where(x => x.PayrollGroupId == scope.PayrollGroupId.Value);
            if (scope.DepartmentId.HasValue) employees = employees.Where(x => x.DepartmentId == scope.DepartmentId);
            if (scope.BranchId.HasValue) employees = employees.Where(x => x.BranchId == scope.BranchId);
            var scopedIds = employees.Select(x => x.Id);
            credits = credits.Where(x => scopedIds.Contains(x.EmployeeId));
        }
        var rows = await credits.ToListAsync(token);

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

    // Retirement Ledger main list — one row per employee with entries dated up to asOf.
    public async Task<List<FundLedgerSummaryModel>> GetRetirementSummaryAsync(DateOnly asOf, CancellationToken token, Guid? clientId = null) =>
        await SummarizeFundAsync(await Context.RetirementLedgers.AsNoTracking()
            .Where(x => x.EntryDate <= asOf)
            .Where(x => clientId == null || x.Employee.ClientId == clientId)
            .Select(x => new FundMovement(x.EmployeeId, x.EntryDate, x.Amount))
            .ToListAsync(token), token);

    // Uniform Allowance Ledger main list — same shape as GetRetirementSummaryAsync, optionally for
    // one client's current employees, and flagging who has separated (their balance is due with
    // last pay under a KeepForRelease policy). separatedWithBalance keeps only those still owed.
    public async Task<List<FundLedgerSummaryModel>> GetUniformAllowanceSummaryAsync(
        DateOnly asOf, CancellationToken token, Guid? clientId = null, bool separatedWithBalance = false)
    {
        var movements = Context.UniformAllowanceLedgers.AsNoTracking().Where(x => x.EntryDate <= asOf);
        if (clientId is { } client) movements = movements.Where(x => x.Employee.ClientId == client);
        var rows = await SummarizeFundAsync(await movements
            .Select(x => new FundMovement(x.EmployeeId, x.EntryDate, x.Amount))
            .ToListAsync(token), token);

        var ids = rows.Select(r => r.EmployeeId).ToList();
        var statuses = await _uow.Repository.FindAll<Employee>().AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .Select(x => new { x.Id, x.EmploymentStatus, x.DateResigned })
            .ToListAsync(token);
        foreach (var s in statuses.Where(s => UniformSeparation.IsSeparated(s.EmploymentStatus, s.DateResigned, asOf)))
            rows.First(r => r.EmployeeId == s.Id).SeparatedOn = UniformSeparation.SeparatedOn(s.DateResigned, asOf);

        return separatedWithBalance ? rows.Where(r => r.SeparatedOn != null && r.Balance > 0).ToList() : rows;
    }

    private sealed record FundMovement(Guid EmployeeId, DateOnly EntryDate, decimal Amount);

    private async Task<List<FundLedgerSummaryModel>> SummarizeFundAsync(List<FundMovement> movements, CancellationToken token)
    {
        var employeeMap = await LoadEmployeeMapAsync(movements.Select(x => x.EmployeeId), token);

        return movements
            .GroupBy(x => x.EmployeeId)
            .Select(g =>
            {
                employeeMap.TryGetValue(g.Key, out var e);
                return new FundLedgerSummaryModel
                {
                    EmployeeId = g.Key,
                    EmployeeNo = e?.EmployeeNo ?? "",
                    FullName = e.FullName(),
                    TotalAdded = g.Where(x => x.Amount > 0).Sum(x => x.Amount),
                    TotalDeducted = -g.Where(x => x.Amount < 0).Sum(x => x.Amount),
                    Balance = g.Sum(x => x.Amount),
                    EntryCount = g.Count(),
                    LastEntryDate = g.Max(x => x.EntryDate),
                };
            })
            .OrderBy(x => x.FullName)
            .ToList();
    }

    // Retirement Ledger details — one employee's entries up to asOf, oldest first, with the fund's
    // running balance as it stood after each entry.
    public async Task<List<LedgerEntryModel>> GetRetirementEntriesAsync(Guid employeeId, DateOnly asOf, CancellationToken token) =>
        (await Context.RetirementLedgers.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.EntryDate <= asOf)
            .OrderBy(x => x.EntryDate).ThenBy(x => x.CreatedAt)
            .ToListAsync(token))
        .Select(x => new LedgerEntryModel
        {
            EntryDate = x.EntryDate,
            EntryType = x.EntryType.ToString(),
            Amount = x.Amount,
            Balance = x.Balance,
            Remarks = x.Particulars,
            PayrollId = x.PayrollId,
        })
        .ToList();

    // Uniform Allowance Ledger details — same as GetRetirementEntriesAsync.
    public async Task<List<LedgerEntryModel>> GetUniformAllowanceEntriesAsync(Guid employeeId, DateOnly asOf, CancellationToken token) =>
        (await Context.UniformAllowanceLedgers.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.EntryDate <= asOf)
            .OrderBy(x => x.EntryDate).ThenBy(x => x.CreatedAt)
            .ToListAsync(token))
        .Select(x => new LedgerEntryModel
        {
            EntryDate = x.EntryDate,
            EntryType = x.EntryType.ToString(),
            Amount = x.Amount,
            Balance = x.Balance,
            Remarks = x.Particulars,
        })
        .ToList();

    // Cash Bond details — the same two sources GetCashBondReportAsync totals (payroll runs and
    // manual entries), merged oldest first with a running collected total.
    public async Task<List<LedgerEntryModel>> GetCashBondEntriesAsync(Guid employeeId, DateOnly asOf, CancellationToken token)
    {
        var fromPayroll = await CashBondPayrolls()
            .Where(x => x.EmployeeId == employeeId && x.CashBondDeduction > 0 && x.PayPeriodEnd <= asOf)
            .Select(x => new { x.Id, x.PayPeriodStart, x.PayPeriodEnd, x.CashBondDeduction, x.CreatedAt })
            .ToListAsync(token);
        var adjustments = await Context.CashBondAdjustments.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.EntryDate <= asOf)
            .Select(x => new { x.EntryDate, x.Amount, x.Particulars, x.CreatedAt })
            .ToListAsync(token);

        var entries = fromPayroll
            .Select(x => (x.CreatedAt, Entry: new LedgerEntryModel
            {
                EntryDate = x.PayPeriodEnd,
                EntryType = "Payroll",
                Amount = x.CashBondDeduction,
                Remarks = $"Payroll {x.PayPeriodStart:MMM dd} – {x.PayPeriodEnd:MMM dd, yyyy}",
                PayrollId = x.Id,
            }))
            .Concat(adjustments.Select(x => (x.CreatedAt, Entry: new LedgerEntryModel
            {
                EntryDate = x.EntryDate,
                EntryType = "Adjustment",
                Amount = x.Amount,
                Remarks = x.Particulars,
            })))
            .OrderBy(x => x.Entry.EntryDate).ThenBy(x => x.CreatedAt)
            .Select(x => x.Entry)
            .ToList();

        var balance = 0m;
        foreach (var entry in entries)
        {
            balance += entry.Amount;
            entry.Balance = balance;
        }
        return entries;
    }

    // Leave Ledger details — one employee's credit movements for one leave type and period year
    // (grants, accruals, reservations, deductions, adjustments, …), oldest first. Balance is the
    // credits balance after each entry; reservations hold days without moving it.
    public async Task<List<LedgerEntryModel>> GetLeaveEntriesAsync(Guid employeeId, Guid leaveId, int year, CancellationToken token) =>
        (await Context.LeaveLedgers.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.LeaveId == leaveId && x.LeaveCredits.PeriodYear == year)
            .OrderBy(x => x.EntryDate).ThenBy(x => x.CreatedAt)
            .ToListAsync(token))
        .Select(x => new LedgerEntryModel
        {
            EntryDate = x.EntryDate,
            EntryType = x.EntryType.ToString(),
            Amount = x.Amount,
            Balance = x.Balance,
            Remarks = x.Particulars,
        })
        .ToList();

    // Grouped and summed in SQL. Department/Branch come from the employee's CURRENT record
    // (Payroll has neither column) via a left join, so payroll of a deleted/missing employee lands
    // in the "Unassigned" (null) group, as before.
    public async Task<List<CostSummaryModel>> GetCostSummaryAsync(
        DateOnly from, DateOnly to, string groupBy, CancellationToken token, Guid? clientId = null)
    {
        var payrolls = ForClient(GetQueryable(x => x.PayPeriodStart >= from && x.PayPeriodEnd <= to && x.IsPosted), clientId);

        IQueryable<CostRow> rows;
        if (groupBy == "client")
        {
            rows = payrolls.Select(p => new CostRow
            {
                GroupId = p.ClientId, EmployeeId = p.EmployeeId, BasicPay = p.BasicPay, GrossIncome = p.GrossIncome,
                TotalDeductions = p.TotalDeductions, NetPay = p.NetPay,
                EmployerCost = p.EmployerSSSContribution + p.EmployerPhilHealthContribution + p.EmployerPagIbigContribution + p.EmployerECContribution,
            });
        }
        else
        {
            var byBranch = groupBy == "branch";
            rows =
                from p in payrolls
                join e in _uow.Repository.FindAll<Employee>() on p.EmployeeId equals e.Id into employees
                from e in employees.DefaultIfEmpty()
                select new CostRow
                {
                    GroupId = e == null ? null : byBranch ? e.BranchId : e.DepartmentId,
                    EmployeeId = p.EmployeeId, BasicPay = p.BasicPay, GrossIncome = p.GrossIncome,
                    TotalDeductions = p.TotalDeductions, NetPay = p.NetPay,
                    EmployerCost = p.EmployerSSSContribution + p.EmployerPhilHealthContribution + p.EmployerPagIbigContribution + p.EmployerECContribution,
                };
        }

        var groups = await rows
            .GroupBy(x => x.GroupId)
            .Select(g => new
            {
                GroupId = g.Key,
                EmployeeCount = g.Select(x => x.EmployeeId).Distinct().Count(),
                BasicPay = g.Sum(x => x.BasicPay),
                GrossIncome = g.Sum(x => x.GrossIncome),
                TotalDeductions = g.Sum(x => x.TotalDeductions),
                NetPay = g.Sum(x => x.NetPay),
                EmployerCost = g.Sum(x => x.EmployerCost),
            })
            .ToListAsync(token);

        var groupIds = groups.Where(x => x.GroupId.HasValue).Select(x => x.GroupId!.Value).ToList();
        var names = groupBy switch
        {
            "client" => await Context.Clients.AsNoTracking().Where(x => groupIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, token),
            "branch" => await Context.Branches.AsNoTracking().Where(x => groupIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, token),
            _ => await Context.Departments.AsNoTracking().Where(x => groupIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, token),
        };

        return groups
            .Select(g => new CostSummaryModel
            {
                GroupId = g.GroupId,
                GroupName = g.GroupId.HasValue && names.TryGetValue(g.GroupId.Value, out var name) ? name : "Unassigned",
                EmployeeCount = g.EmployeeCount,
                TotalBasicPay = g.BasicPay,
                TotalGrossIncome = g.GrossIncome,
                TotalDeductions = g.TotalDeductions,
                TotalNetPay = g.NetPay,
                EmployerContributionsCost = g.EmployerCost,
            })
            .OrderByDescending(x => x.TotalNetPay).ToList();
    }

    // Member-initialized (not a positional record) so EF can keep composing GroupBy over it.
    private sealed class CostRow
    {
        public Guid? GroupId { get; init; }
        public Guid EmployeeId { get; init; }
        public decimal BasicPay { get; init; }
        public decimal GrossIncome { get; init; }
        public decimal TotalDeductions { get; init; }
        public decimal NetPay { get; init; }
        public decimal EmployerCost { get; init; }
    }

    public async Task<List<YtdPayrollSummaryModel>> GetYtdSummaryAsync(int year, Guid? employeeId, CancellationToken token, Guid? clientId = null)
    {
        var (yearStart, yearEnd) = YearRange(year);
        var clientEmployees = await ClientEmployeeSetAsync(clientId, token);
        var payrollTotals = await SumByEmployeeAsync(ForClient(GetQueryable(x =>
            x.PayPeriodStart >= yearStart && x.PayPeriodStart < yearEnd && x.IsPosted &&
            (employeeId == null || x.EmployeeId == employeeId)), clientId), token);

        // Fold in each employee's pre-system-cutover Opening Balance (see
        // PayrollOpeningBalance) so a company onboarding mid-year isn't understated for the
        // months before this system existed. Employees present only via an Opening Balance
        // (no Payroll rows yet this year) still get a row, not just the ones with Payroll data.
        var openingBalances = await _payrollOpeningBalanceService.FindAllByYearAsync(year, token);
        if (employeeId != null)
        {
            openingBalances = openingBalances.Where(x => x.Key == employeeId).ToDictionary(x => x.Key, x => x.Value);
        }
        openingBalances = OnlyClient(openingBalances, clientEmployees);

        var employeeIds = payrollTotals.Keys.Union(openingBalances.Keys).ToList();
        var employeeMap = await LoadEmployeeMapAsync(employeeIds, token);

        return employeeIds.Select(id =>
        {
            employeeMap.TryGetValue(id, out var e);
            var g = payrollTotals.GetValueOrDefault(id) ?? PayrollTotals.Zero;
            openingBalances.TryGetValue(id, out var ob);
            return new YtdPayrollSummaryModel
            {
                EmployeeId = id,
                EmployeeNo = e?.EmployeeNo ?? "",
                FullName = e.FullName(),
                Year = year,
                TotalBasicPay = g.BasicPay + (ob?.BasicPay ?? 0),
                TotalOvertimePay = g.OvertimePay + (ob?.OvertimePay ?? 0),
                TotalHolidayPay = g.HolidayPay + (ob?.HolidayPay ?? 0),
                TotalAllowances = g.Allowances + (ob?.Allowances ?? 0),
                TotalOtherIncome = g.OtherIncome + (ob?.OtherIncome ?? 0),
                TotalGrossIncome = g.GrossIncome + (ob?.GrossIncome ?? 0),
                TotalSSS = g.Sss + (ob?.SSSContribution ?? 0),
                TotalPhilHealth = g.PhilHealth + (ob?.PhilHealthContribution ?? 0),
                TotalPagIbig = g.PagIbig + (ob?.PagIbigContribution ?? 0),
                TotalWithholdingTax = g.WithholdingTax + (ob?.WithholdingTax ?? 0),
                TotalOtherDeductions = g.OtherDeductions + (ob?.OtherDeductions ?? 0),
                TotalDeductions = g.TotalDeductions + (ob?.TotalDeductions ?? 0),
                TotalNetPay = g.NetPay + (ob?.NetPay ?? 0),
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
    public async Task<List<ThirteenthMonthModel>> GetThirteenthMonthAsync(
        int year, CancellationToken token, DateOnly? asOfDate = null, Guid? employeeId = null, Guid? clientId = null)
    {
        var (yearStart, yearEnd) = YearRange(year);
        var clientEmployees = await ClientEmployeeSetAsync(clientId, token);
        var payrollTotals = await SumByEmployeeAsync(ForClient(GetQueryable(x =>
                x.PostingPeriod >= yearStart && x.PostingPeriod < yearEnd && x.IsPosted && x.PayrollType == PayrollType.Regular &&
                (asOfDate == null || x.PayPeriodStart <= asOfDate) &&
                (employeeId == null || x.EmployeeId == employeeId)), clientId), token);

        // Opening Balance (pre-system-cutover) BasicPay/Bonuses are always fully in the past
        // relative to any asOfDate within the same year, so they're folded in unconditionally
        // regardless of the asOfDate slice — see PayrollOpeningBalance's doc comment.
        var openingBalances = await _payrollOpeningBalanceService.FindAllByYearAsync(year, token);
        if (employeeId != null)
        {
            openingBalances = openingBalances.Where(x => x.Key == employeeId).ToDictionary(x => x.Key, x => x.Value);
        }
        openingBalances = OnlyClient(openingBalances, clientEmployees);

        // This year's own 13th month payout row, if generated — used to surface a
        // released/unreleased Status per employee (NotGenerated/Draft/Posted), independent
        // of the `rows` query above (which deliberately excludes ThirteenthMonth rows so a
        // payout can never fold into its own entitlement calculation).
        var thirteenthMonthRuns = await ForClient(GetQueryable(x =>
                x.PayrollType == PayrollType.ThirteenthMonth && x.PayPeriodStart >= yearStart && x.PayPeriodStart < yearEnd &&
                (employeeId == null || x.EmployeeId == employeeId)), clientId)
            .Select(x => new { x.Id, x.EmployeeId, x.IsPosted, x.NetPay, x.AcknowledgedAt })
            .ToListAsync(token);
        var runByEmployee = thirteenthMonthRuns
            .GroupBy(x => x.EmployeeId)
            .ToDictionary(g => g.Key, g => g.First());

        var employeeIds = payrollTotals.Keys.Union(openingBalances.Keys).ToList();
        var employeeMap = await LoadEmployeeMapAsync(employeeIds, token);

        return employeeIds.Select(id =>
        {
            employeeMap.TryGetValue(id, out var e);
            var g = payrollTotals.GetValueOrDefault(id) ?? PayrollTotals.Zero;
            openingBalances.TryGetValue(id, out var ob);
            var totalBasic = g.BasicPay + (ob?.BasicPay ?? 0);
            var totalSpecialBonuses = g.Bonuses + (ob?.Bonuses ?? 0);
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
        var (yearStart, yearEnd) = YearRange(year);
        var inScope = GetQueryable(x =>
                x.PostingPeriod >= yearStart && x.PostingPeriod < yearEnd && x.IsPosted &&
                (x.PayrollType == PayrollType.Regular || x.PayrollType == PayrollType.ThirteenthMonth || x.PayrollType == PayrollType.LastPay));
        var payrollTotals = await SumByEmployeeAsync(inScope, token);
        // Only the columns the MWE check and the "latest row" pick read -- not full rows.
        var rows = await inScope
            .Select(x => new Payroll
            {
                EmployeeId = x.EmployeeId, PayrollType = x.PayrollType, PostingPeriod = x.PostingPeriod, DailyRate = x.DailyRate,
                PayrollGroupId = x.PayrollGroupId, AreaId = x.AreaId, ClientId = x.ClientId, SalaryType = x.SalaryType,
            })
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
            var t = payrollTotals.GetValueOrDefault(id) ?? PayrollTotals.Zero;

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
                CurrentGrossIncome = t.GrossIncome + (ob?.GrossIncome ?? 0),
                CurrentNonTaxableBenefits = t.NonTaxableBenefits + (ob?.NonTaxableIncome ?? 0),
                CurrentStatutoryDeductions = t.Sss + t.PhilHealth + t.PagIbig
                    + (ob == null ? 0 : ob.SSSContribution + ob.PhilHealthContribution + ob.PagIbigContribution),
                CurrentWithholdingTaxYTD = t.WithholdingTax + (ob?.WithholdingTax ?? 0),
                CurrentAverageMonthlyNetPay = (t.NetPay + (ob?.NetPay ?? 0)) / 12,
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
        GetMonthlyRemittanceReturnAsync(DateOnly from, DateOnly to, bool amendedReturn, CancellationToken token, Guid? clientId = null)
    {
        // One query for all three run types, reading only the columns the return uses (the MWE
        // classification needs row-level data, so rows -- narrow ones -- are still fetched).
        var periodRows = await ForClient(GetQueryable(x =>
                (x.PayrollType == PayrollType.Regular || x.PayrollType == PayrollType.ThirteenthMonth ||
                 x.PayrollType == PayrollType.YearEndAdjustment) &&
                x.IsPosted && x.PostingPeriod >= from && x.PostingPeriod <= to), clientId)
            .Select(x => new Payroll
            {
                EmployeeId = x.EmployeeId, PayrollType = x.PayrollType, PostingPeriod = x.PostingPeriod, DailyRate = x.DailyRate,
                GrossIncome = x.GrossIncome, BasicPay = x.BasicPay, HolidayPay = x.HolidayPay, OvertimePay = x.OvertimePay,
                NightDifferentialPay = x.NightDifferentialPay, NightDifferentialOTPay = x.NightDifferentialOTPay,
                SSSContribution = x.SSSContribution, PhilHealthContribution = x.PhilHealthContribution,
                PagIbigContribution = x.PagIbigContribution, TotalDeminimises = x.TotalDeminimises,
                WithholdingTax = x.WithholdingTax, NonTaxableBenefits = x.NonTaxableBenefits, TaxableBenefits = x.TaxableBenefits,
            })
            .ToListAsync(token);

        var regularRows = periodRows.Where(x => x.PayrollType == PayrollType.Regular).ToList();

        // 13th month payouts use NonTaxableBenefits/TaxableBenefits, not TaxableIncome — a
        // different field pair than regular runs, so they're summed separately.
        var thirteenthMonthRows = periodRows.Where(x => x.PayrollType == PayrollType.ThirteenthMonth).ToList();

        // Year-End Tax Adjustment rows (see TaxAnnualizationService) — per RR 11-2018
        // §2.79.4(B), an under-withheld year-end collection must be remitted with that month's
        // 1601-C, and an over-withheld refund is netted against it. Kept in its own bucket
        // (never merged into regRows) since its DailyRate/BasicPay/etc. are always 0 and would
        // corrupt the MWE "latest regular row" pick above if merged in.
        var yearEndAdjustmentRows = periodRows.Where(x => x.PayrollType == PayrollType.YearEndAdjustment).ToList();

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
            : await Context.OtherIncomeApplicationDetails.AsNoTracking()
                .Where(x => hazardPayIncomeIds.Contains(x.IncomeId) && !x.IsSkipped && x.Date >= from && x.Date <= to)
                .GroupBy(x => x.EmployeeId)
                .Select(g => new { EmployeeId = g.Key, Amount = g.Sum(x => x.Amount) })
                .ToDictionaryAsync(x => x.EmployeeId, x => x.Amount, token);

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
    public async Task<List<AlphalistEntryModel>> GetAlphalistAsync(int year, CancellationToken token, Guid? clientId = null)
    {
        var (yearStart, yearEnd) = YearRange(year);
        var payrollTotals = await SumByEmployeeAsync(
            ForClient(GetQueryable(x => x.PostingPeriod >= yearStart && x.PostingPeriod < yearEnd && x.IsPosted), clientId), token);

        // See PayrollOpeningBalance's doc comment — folds pre-system-cutover figures into
        // this full-year BIR return so a mid-year onboarded company isn't understated.
        var openingBalances = OnlyClient(
            await _payrollOpeningBalanceService.FindAllByYearAsync(year, token), await ClientEmployeeSetAsync(clientId, token));

        var employeeIds = payrollTotals.Keys.Union(openingBalances.Keys).ToList();
        var employeeMap = await LoadEmployeeMapAsync(employeeIds, token);

        return employeeIds.Select(id =>
        {
            employeeMap.TryGetValue(id, out var e);
            var g = payrollTotals.GetValueOrDefault(id) ?? PayrollTotals.Zero;
            openingBalances.TryGetValue(id, out var ob);
            var obTaxable = ob?.DerivedTaxableIncome ?? 0;
            return new AlphalistEntryModel
            {
                EmployeeId = id,
                EmployeeNo = e?.EmployeeNo ?? "",
                FullName = e.FullName(),
                TIN = e?.TIN ?? "",
                Year = year,
                GrossCompensation = g.GrossIncome + (ob?.GrossIncome ?? 0),
                NonTaxableCompensation = g.NonTaxableIncome + (ob?.NonTaxableIncome ?? 0),
                TaxableCompensation = g.TaxableIncome + obTaxable,
                ThirteenthMonthPay = (g.BasicPay + (ob?.BasicPay ?? 0)) / 12,
                TotalSSS = g.Sss + (ob?.SSSContribution ?? 0),
                TotalPhilHealth = g.PhilHealth + (ob?.PhilHealthContribution ?? 0),
                TotalPagIbig = g.PagIbig + (ob?.PagIbigContribution ?? 0),
                TotalTaxWithheld = g.WithholdingTax + (ob?.WithholdingTax ?? 0),
            };
        }).OrderBy(x => x.FullName).ToList();
    }

    // Single-employee version of the Alphalist row, plus the identification fields a 2316
    // certificate needs — null if the employee has no posted payroll for that year.
    public async Task<Bir2316Model?> Get2316DataAsync(Guid employeeId, int year, CancellationToken token)
    {
        var (yearStart, yearEnd) = YearRange(year);
        var totals = (await SumByEmployeeAsync(GetQueryable(x =>
            x.EmployeeId == employeeId && x.PostingPeriod >= yearStart && x.PostingPeriod < yearEnd && x.IsPosted), token))
            .GetValueOrDefault(employeeId);

        // An employee whose only data for the year is a pre-cutover Opening Balance (no
        // Payroll rows posted here yet) still gets a certificate, not a null.
        var openingBalances = await _payrollOpeningBalanceService.FindAllByYearAsync(year, token);
        openingBalances.TryGetValue(employeeId, out var ob);
        if (totals == null && ob == null) return null;

        var g = totals ?? PayrollTotals.Zero;
        var obTaxable = ob?.DerivedTaxableIncome ?? 0;

        // Header fields only -- not the full 201 profile (seven child-collection queries).
        (await LoadEmployeeMapAsync([employeeId], token)).TryGetValue(employeeId, out var employee);
        return new Bir2316Model
        {
            EmployeeId = employeeId,
            EmployeeNo = employee?.EmployeeNo ?? "",
            // Same format as EmployeeFullModel.FullName (MappingProfile), which 2316 always used:
            // "Last, First Middle Suffix" -- not the FullName() extension's "Last, First Suffix Middle".
            FullName = employee == null ? "" : $"{employee.LastName}, {employee.FirstName} {employee.MiddleName} {employee.Suffix}",
            TIN = employee?.TIN ?? "",
            RDOCode = employee?.RDOCode ?? "",
            Address = string.Join(", ", new[] { employee?.Address1, employee?.Address2 }
                .Where(s => !string.IsNullOrWhiteSpace(s))),
            CivilStatus = employee?.CivilStatus ?? "",
            Year = year,
            GrossCompensation = g.GrossIncome + (ob?.GrossIncome ?? 0),
            NonTaxableCompensation = g.NonTaxableIncome + (ob?.NonTaxableIncome ?? 0),
            TaxableCompensation = g.TaxableIncome + obTaxable,
            ThirteenthMonthPay = (g.BasicPay + (ob?.BasicPay ?? 0)) / 12,
            TotalSSS = g.Sss + (ob?.SSSContribution ?? 0),
            TotalPhilHealth = g.PhilHealth + (ob?.PhilHealthContribution ?? 0),
            TotalPagIbig = g.PagIbig + (ob?.PagIbigContribution ?? 0),
            TotalTaxWithheld = g.WithholdingTax + (ob?.WithholdingTax ?? 0),
        };
    }

    // OneTime, employer-advanced government payouts awaiting/undergoing SSS-style
    // reimbursement — see LeaveApplication.EmployerAdvancesPayment/ReimbursementStatus.
    // Direct-deposit payouts are excluded: the employer never advanced that money, so there's
    // nothing for it to be reimbursed for. Sourced straight from LeaveApplication (not posted
    // Payroll rows, unlike this service's other reports) since the claim exists independently
    // of whether this run's payroll has been posted yet.
    public async Task<List<ReimbursementListModel>> GetReimbursementListAsync(
        DateOnly from, DateOnly to, CancellationToken token, Guid? clientId = null)
    {
        var applications = Context.leaveApplications.Include(x => x.Leave).AsQueryable();
        if (clientId is { } client) applications = applications.Where(x => ClientEmployeeIds(client).Contains(x.EmployeeId));
        var apps = await applications
            .Where(x =>
                x.PayoutMode == PayoutMode.OneTime &&
                x.ApprovalStatus == ApprovalStatus.Approved &&
                x.GovernmentAmount != null && x.GovernmentAmount > 0 &&
                x.ReleasePayrollDate != null &&
                x.ReleasePayrollDate >= from && x.ReleasePayrollDate <= to &&
                // Employer-advanced only, resolved in SQL (per-application override, else the
                // leave type's default) instead of filtering after loading every payout.
                (x.EmployerAdvancesPayment ?? x.Leave.EmployerAdvancesPayment))
            .AsNoTracking()
            .ToListAsync(token);

        var employerAdvanced = apps;

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

    // Only the employee columns these reports print (names, number, TIN, bank, branch/department,
    // cash bond, 2316 identification) -- not full Employee rows. Soft-deleted employees stay out,
    // exactly as before (their payroll rows report with a blank name).
    private async Task<Dictionary<Guid, Employee>> LoadEmployeeMapAsync(IEnumerable<Guid> employeeIds, CancellationToken token)
    {
        var ids = employeeIds.Distinct().ToList();
        if (ids.Count == 0) return [];

        return await _uow.Repository.FindAll<Employee>().AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .Select(x => new Employee
            {
                Id = x.Id,
                EmployeeNo = x.EmployeeNo,
                FirstName = x.FirstName,
                LastName = x.LastName,
                MiddleName = x.MiddleName,
                Suffix = x.Suffix,
                TIN = x.TIN,
                RDOCode = x.RDOCode,
                Address1 = x.Address1,
                Address2 = x.Address2,
                CivilStatus = x.CivilStatus,
                ModeOfPayment = x.ModeOfPayment,
                BankName = x.BankName,
                BankNo = x.BankNo,
                CashBond = x.CashBond,
                BranchId = x.BranchId,
                DepartmentId = x.DepartmentId,
            })
            .ToDictionaryAsync(x => x.Id, token);
    }

    // Index-friendly calendar-year bounds: [Jan 1, Jan 1 next year). Comparing a date column to
    // a range lets MySQL use the index; wrapping the column (PostingPeriod.Year == year) can't.
    private static (DateOnly Start, DateOnly End) YearRange(int year) => (new DateOnly(year, 1, 1), new DateOnly(year + 1, 1, 1));

    // Per-employee column totals, summed in SQL (GROUP BY EmployeeId) instead of loading every
    // full Payroll row (~270 columns) and summing in C#.
    private sealed record PayrollTotals(
        Guid EmployeeId,
        decimal BasicPay, decimal OvertimePay, decimal HolidayPay, decimal Allowances, decimal OtherIncome,
        decimal Bonuses, decimal GrossIncome, decimal NonTaxableIncome, decimal TaxableIncome,
        decimal NonTaxableBenefits, decimal Sss, decimal PhilHealth, decimal PagIbig, decimal WithholdingTax,
        decimal OtherDeductions, decimal TotalDeductions, decimal NetPay)
    {
        public static readonly PayrollTotals Zero = new(Guid.Empty, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    private static Task<Dictionary<Guid, PayrollTotals>> SumByEmployeeAsync(IQueryable<Payroll> rows, CancellationToken token) =>
        rows.GroupBy(x => x.EmployeeId)
            .Select(g => new PayrollTotals(
                g.Key,
                g.Sum(x => x.BasicPay), g.Sum(x => x.OvertimePay), g.Sum(x => x.HolidayPay),
                g.Sum(x => x.TotalRegularAllowances), g.Sum(x => x.TotalOtherIncome), g.Sum(x => x.TotalBonuses),
                g.Sum(x => x.GrossIncome), g.Sum(x => x.NonTaxableIncome), g.Sum(x => x.TaxableIncome),
                g.Sum(x => x.NonTaxableBenefits), g.Sum(x => x.SSSContribution), g.Sum(x => x.PhilHealthContribution),
                g.Sum(x => x.PagIbigContribution), g.Sum(x => x.WithholdingTax), g.Sum(x => x.OtherDeductions),
                g.Sum(x => x.TotalDeductions), g.Sum(x => x.NetPay)))
            .ToDictionaryAsync(x => x.EmployeeId, token);
}
