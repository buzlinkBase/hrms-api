using DocumentFormat.OpenXml.VariantTypes;
using Hrms.Domain.Entities;
namespace Hrms.Core.Services;

public class PayrollService : BaseService<Payroll>
{
    private readonly IMapper _mapper;
    public PayrollService(IUnitOfWorkService uow, IMapper mapper) : base(uow)
    {
        _mapper = mapper;
    }

    public async Task<Dictionary<EmployeeKey, List<Payroll>>> LoadPostedPayrollAsync(DateOnly fromDate, DateOnly toDate,
        CancellationToken token)
    {
        //var spec = new IsPostedSpec<Payroll>(true)
        //    .And(new IsDateByMonthYearSpec<Payroll>(fromDate))
        //    .AndNot(new IsDateWithinRangeSpec<Payroll>(fromDate, toDate))
        //    ;
        return await GetQueryable(x =>
                 //x.PayrollDate >= fromDate && x.PayrollDate <= toDate &&
                 x.PayrollDate.Month == fromDate.Month && x.PayrollDate.Year == fromDate.Year  &&
                 x.IsPosted)
            .GroupBy(x => new EmployeeKey(x.EmployeeId))
            .ToDictionaryAsync(x => x.Key, x => x.ToList(), token);
        ;
    }

    public async Task<Payroll?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }

    // Setup > Payslip/13th Month/Last Pay > Received by Employee (MeController.
    // AcknowledgeMyPayslip) — idempotent: only the first acknowledgment sets the timestamp, so
    // this records "when did they first receive it," not "when did they last click the
    // button." Scoped to employeeId (not just id) so a caller can only ever acknowledge their
    // own payslip, matching PrintMyPayslip's identical ownership check. Returns null if no
    // matching row exists (not found or not this employee's).
    public async Task<DateTime?> AcknowledgeAsync(Guid id, Guid employeeId, CancellationToken token)
    {
        var payroll = await GetQueryable(x => x.Id == id && x.EmployeeId == employeeId, noTracking: false)
            .FirstOrDefaultAsync(token);
        if (payroll == null) return null;

        if (payroll.AcknowledgedAt == null)
        {
            payroll.AcknowledgedAt = DateTime.UtcNow;
            await ModifyRangeAsync(new[] { payroll }, token);
            await CommitChangesAsync(token);
        }
        return payroll.AcknowledgedAt;
    }

    // The last regular-payroll period end this employee was ever actually paid for — null if
    // they've never had a regular payroll row. Used by LastPayrollService to find the gap
    // between "the last cutoff that actually ran for them" and their separation date, both
    // for Salary Adjustment/Other Income consumption windows and the DTR attendance warning.
    public async Task<DateOnly?> GetLatestRegularPayPeriodEndAsync(Guid employeeId, CancellationToken token)
    {
        return await GetQueryable(x => x.EmployeeId == employeeId && x.PayrollType == PayrollType.Regular)
            .OrderByDescending(x => x.PayPeriodEnd)
            .Select(x => (DateOnly?)x.PayPeriodEnd)
            .FirstOrDefaultAsync(token);
    }

    public async Task Delete(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
    }

    // Every row from one Generate run — all rows written by a single PayrollProcessorService
    // .CalculateAsync call share the same PayrollBatchId (assigned once per run, the id of
    // the PayrollBatch header row, not per employee).
    public async Task<List<Payroll>> GetByBatchIdAsync(Guid payrollBatchId, CancellationToken token)
    {
        return await GetQueryable(x => x.PayrollBatchId == payrollBatchId).ToListAsync(token);
    }

    public async Task DeleteByBatchIdAsync(Guid payrollBatchId, CancellationToken token)
    {
        await ExecuteDeleteAsync(x => x.PayrollBatchId == payrollBatchId, token);
    }

    // A draft that's deleted before posting never had its DeductionApplicationDetail.Balance
    // touched (see PostBatchAsync/ReduceDeductionBalancesAsync below — only Post does that), so
    // this is pure cleanup of the now-orphaned breakdown rows, same reasoning as the SSS/PHIC/
    // HDMF/WTax contribution ledger deletes already done by PayrollBatchLifecycleService.
    // DeleteBatchAsync alongside this call.
    public async Task DeleteDeductionDetailsByPayrollIdsAsync(List<Guid> payrollIds, CancellationToken token)
    {
        if (payrollIds.Count == 0) return;
        await Context.PayrollDeductionDetails.Where(x => payrollIds.Contains(x.PayrollId)).ExecuteDeleteAsync(token);
    }

    // Same reasoning as DeleteDeductionDetailsByPayrollIdsAsync above -- PayrollDtrDetail is the
    // other Payroll-child table (PayrollId FK, no cascade configured -- see Payroll.
    // TimeHourPayResults), and was previously left orphaned by PayrollBatchLifecycleService.
    // DeleteBatchAsync even though DeductionDetails already got cleaned up alongside it.
    public async Task DeleteDtrDetailsByPayrollIdsAsync(List<Guid> payrollIds, CancellationToken token)
    {
        if (payrollIds.Count == 0) return;
        await Context.PayrollDtrDetails.Where(x => payrollIds.Contains(x.PayrollId)).ExecuteDeleteAsync(token);
    }

    // Mirrors PayrollBatch.IsPosted onto every child row of the batch — PayrollBatch is the
    // canonical source for Post/Delete decisions, but the child rows keep their own copy so
    // hot-path report filters (PayrollReportService, LoadPostedPayrollAsync above) don't
    // need to join PayrollBatch. See PayrollProcessorService.PostBatchAsync, which calls
    // this alongside PayrollBatchService.PostAsync.
    // commit=false: see PayrollBatchService.PostAsync's doc comment -- the two are only ever
    // composed together by PayrollBatchLifecycleService.PostBatchAsync, which needs to flush
    // both (plus, for a YearEndAdjustment batch, YearLockService.LockYearAsync) into the SAME
    // final CommitChangesAsync so the whole Post either fully succeeds or fully rolls back.
    public async Task PostBatchAsync(Guid payrollBatchId, CancellationToken token, bool commit = true)
    {
        // GetQueryable defaults to AsNoTracking — must opt into tracking here (noTracking:
        // false), or mutating IsPosted below never gets picked up by SaveChanges and this
        // silently no-ops: no exception, no rows actually updated. ModifyRangeAsync/UpdateRange
        // is kept as a belt-and-suspenders explicit mark, but the real fix is tracking the
        // query in the first place — same reasoning as DailyRecordService.PostAsync/UnpostAsync,
        // which mutate tracked entities directly with no explicit Update() call at all.
        var payrolls = await GetQueryable(x => x.PayrollBatchId == payrollBatchId, noTracking: false).ToListAsync(token);
        if (payrolls.Count == 0) throw new ValidationException("Payroll batch not found.");
        foreach (var payroll in payrolls) payroll.IsPosted = true;
        await ModifyRangeAsync(payrolls, token);
        await ReduceDeductionBalancesAsync(payrolls.Select(x => x.Id).ToList(), token);
        await ProcessRetirementFundActivityAsync(payrolls, token);
        if (commit) await CommitChangesAsync(token);
        else await SaveChangesAsync(token);
    }

    // Setup > Company Policy > Minimum Take-Home Pay / loan installments — only NOW, once the
    // run is finalized, does a scheduled deduction actually count as paid. A loan installment's
    // DeductionApplicationDetail.Balance is never touched at Generate/Preview time, so a draft
    // that gets regenerated (recalculated from scratch) or deleted before posting can't corrupt
    // it — this is the one and only place Balance moves. PayrollDeductionDetail rows (written
    // at Generate time — see MappingProfile's DeductionInfo -> PayrollDeductionDetail config)
    // record exactly which installment was charged and how much; amounts are summed per
    // installment first since in principle more than one Payroll row in a batch could reference
    // the same installment (e.g. a correction/adjustment run), even though one-row-each is the
    // common case. A loan blocked by the minimum take-home floor (or any other reason it wasn't
    // included in ScheduledDeductions — see ScheduledDeductionPolicy) simply has no
    // PayrollDeductionDetail row at all, so its Balance stays untouched and it's naturally
    // picked up again by DeductionAplDtlService.LoadAsync on the next payroll run.
    private async Task ReduceDeductionBalancesAsync(List<Guid> payrollIds, CancellationToken token)
    {
        var applied = await Context.PayrollDeductionDetails
            .Where(x => payrollIds.Contains(x.PayrollId))
            .GroupBy(x => x.DeductionApplicationDetailId)
            .Select(g => new { DetailId = g.Key, Amount = g.Sum(x => x.Amount) })
            .ToListAsync(token);
        if (applied.Count == 0) return;

        var detailIds = applied.Select(x => x.DetailId).ToList();
        var details = await Context.DeductionApplicationDetails
            .Where(x => detailIds.Contains(x.Id))
            .ToListAsync(token);

        foreach (var detail in details)
        {
            var amount = applied.First(x => x.DetailId == detail.Id).Amount;
            // Clamp defensively — Balance could have been hand-edited by HR, or reduced by an
            // overlapping batch, between this run's Generate and its Post.
            detail.Balance = Math.Max(detail.Balance - amount, 0);
            if (detail.Balance == 0) detail.Status = "Paid";
        }
    }

    // Setup > Client > Settings > Allowances > Retirement (days/year) -- same "only move the
    // running balance at Post" rule as ReduceDeductionBalancesAsync above, and for the same
    // reason: Payroll.RetirementAccrual is computed at Generate time (EmployeePayrollLineService
    // .ComputeRetirementAccrual) but must never touch RetirementFund.Balance until the run is
    // actually finalized, so a regenerated or deleted draft can't corrupt it. Unlike deductions,
    // no separate detail table/query is needed -- RetirementAccrual/RetirementPayout are already
    // plain columns on the just-loaded `payrolls` (one scalar per employee per payroll row, not
    // a fan-out across multiple installments), so this reads them straight from memory. Handles
    // both directions in one pass (one funds dictionary, one query) since a single Payroll row
    // could in principle carry both -- a Last Pay row cashing out RetirementPayout while a
    // regular row elsewhere in the same mixed batch accrues, though in practice a Last Pay row
    // never itself has RetirementAccrual (LastPayrollService builds its own PayrollSummaryLine
    // and never calls EmployeePayrollLineService.ComputeRetirementAccrual).
    private async Task ProcessRetirementFundActivityAsync(List<Payroll> payrolls, CancellationToken token)
    {
        var relevant = payrolls.Where(x => x.RetirementAccrual > 0 || x.RetirementPayout > 0).ToList();
        if (relevant.Count == 0) return;

        var employeeIds = relevant.Select(x => x.EmployeeId).Distinct().ToList();
        var funds = await Context.RetirementFunds
            .Where(x => employeeIds.Contains(x.EmployeeId))
            .ToDictionaryAsync(x => x.EmployeeId, token);

        foreach (var payroll in relevant)
        {
            if (!funds.TryGetValue(payroll.EmployeeId, out var fund))
            {
                fund = new RetirementFund { EmployeeId = payroll.EmployeeId, Balance = 0 };
                Context.RetirementFunds.Add(fund);
                funds[payroll.EmployeeId] = fund;
            }

            if (payroll.RetirementAccrual > 0)
            {
                fund.Balance += payroll.RetirementAccrual;
                Context.RetirementLedgers.Add(new RetirementLedger
                {
                    EmployeeId = payroll.EmployeeId,
                    RetirementFund = fund,
                    EntryType = RetirementLedgerEntryType.Accrual,
                    PayrollId = payroll.Id,
                    EntryDate = payroll.PayPeriodEnd,
                    Add = payroll.RetirementAccrual,
                    Balance = fund.Balance,
                    Particulars = $"Retirement accrual for payroll {payroll.PayrollPeriod}",
                });
            }

            if (payroll.RetirementPayout > 0)
            {
                var payout = ClampRetirementPayout(payroll.RetirementPayout, fund.Balance);
                fund.Balance -= payout;
                Context.RetirementLedgers.Add(new RetirementLedger
                {
                    EmployeeId = payroll.EmployeeId,
                    RetirementFund = fund,
                    EntryType = RetirementLedgerEntryType.Payout,
                    PayrollId = payroll.Id,
                    EntryDate = payroll.PayPeriodEnd,
                    Less = payout,
                    Balance = fund.Balance,
                    Particulars = $"Retirement fund payout — {payroll.PayrollPeriod}",
                });
            }
        }
    }

    // Setup > Payroll Reports > Retirement Ledger — manual HR correction, e.g. seeding an
    // employee's opening Retirement Fund balance when this system is adopted mid-year (before
    // any payroll run here has ever accrued their history). An additive delta + direction, not
    // "set a new balance" — mirrors UniformAllowanceFundService.AdjustAsync exactly. No
    // PayrollId: this entry isn't tied to any specific payroll run.
    public async Task AdjustRetirementBalanceAsync(
        Guid employeeId, decimal amount, bool isAddition, string particulars, DateOnly entryDate, CancellationToken token)
    {
        var fund = await Context.RetirementFunds.FirstOrDefaultAsync(x => x.EmployeeId == employeeId, token);
        if (fund == null)
        {
            fund = new RetirementFund { EmployeeId = employeeId, Balance = 0 };
            Context.RetirementFunds.Add(fund);
        }

        var add = isAddition ? amount : 0m;
        var less = isAddition ? 0m : ClampRetirementPayout(amount, fund.Balance);
        fund.Balance += add - less;
        Context.RetirementLedgers.Add(new RetirementLedger
        {
            EmployeeId = employeeId,
            RetirementFund = fund,
            EntryType = RetirementLedgerEntryType.Adjustment,
            EntryDate = entryDate,
            Add = add,
            Less = less,
            Balance = fund.Balance,
            Particulars = particulars,
        });

        await CommitChangesAsync(token);
    }

    // Clamp defensively -- Balance could have shifted between this run's Generate and its Post
    // (e.g. another of this employee's payrolls posted in between). Never pays out more than
    // what's actually left, and never a negative amount if a stale/negative balance somehow
    // got through. `internal` (not private) so it can be unit tested without a database.
    internal static decimal ClampRetirementPayout(decimal requestedPayout, decimal currentBalance) =>
        Math.Min(Math.Max(requestedPayout, 0), Math.Max(currentBalance, 0));

    // LastPayrollService.GenerateAsync's IncludeRetirementPayout option -- the current
    // RetirementFund.Balance per employee, read-only (mirrors LeaveLedgerService
    // .GetConvertibleLeaveValueAsync's role for leave conversion). Unlike leave, no rate
    // conversion is needed: the balance is already a peso amount.
    public async Task<Dictionary<Guid, decimal>> GetRetirementBalancesAsync(List<Guid> employeeIds, CancellationToken token)
    {
        if (employeeIds.Count == 0) return new();
        return await Context.RetirementFunds
            .Where(x => employeeIds.Contains(x.EmployeeId))
            .ToDictionaryAsync(x => x.EmployeeId, x => x.Balance, token);
    }

    // commit=false lets callers compose this with PayrollBatchService.AddAsync(commit: false) as
    // ONE atomic unit — see AddAsync's doc comment for why (closes the orphaned-empty-batch gap).
    public async Task SavePayrollsAsync(IEnumerable<Payroll> payrolls, CancellationToken token, bool commit = true)
    {
        await Uow.Repository.AddRangeAsync(payrolls, token);
        await Uow.SaveChangesAsync(token);
        if (commit) await CommitChangesAsync(token);
    }

    // Employees who already have a 13th month payout Payroll row for the given calendar
    // year — used by PayrollProcessorService.GenerateThirteenthMonthAsync to block
    // regenerating for an employee already paid, the 13th-month analog of
    // PayrollBatchService.GetUsedDtrBatchCodesAsync.
    public async Task<HashSet<Guid>> GetThirteenthMonthPaidEmployeeIdsAsync(int year, CancellationToken token)
    {
        var ids = await GetQueryable(x => x.PayrollType == PayrollType.ThirteenthMonth && x.PayPeriodStart.Year == year)
            .Select(x => x.EmployeeId)
            .Distinct()
            .ToListAsync(token);
        return ids.ToHashSet();
    }

    // Last Pay is a one-time-ever payout per employee (not annual like 13th month), so no
    // year bound — an employee who already has a LastPay row must have it deleted first to
    // regenerate. See PayrollProcessorService.GenerateLastPayAsync.
    public async Task<HashSet<Guid>> GetLastPayPaidEmployeeIdsAsync(CancellationToken token)
    {
        var ids = await GetQueryable(x => x.PayrollType == PayrollType.LastPay)
            .Select(x => x.EmployeeId)
            .Distinct()
            .ToListAsync(token);
        return ids.ToHashSet();
    }

    // Employees who already have a Year-End Tax Adjustment Payroll row for the given calendar
    // year — annual like 13th month (not one-time-ever like Last Pay), so year-bound. Used by
    // TaxAnnualizationService.GenerateAsync to block regenerating for an employee already
    // adjusted. Filtered on PostingPeriod (not PayPeriodStart) — the BIR-reporting-period-
    // aligned field every other annualization/BIR aggregation keys off (see
    // PayrollReportService.GetAnnualTaxAnnualizationInputsAsync/GetMonthlyRemittanceReturnAsync),
    // robust even if a caller sets an explicit PayDate that lands outside PayPeriodStart/End's
    // own year.
    public async Task<HashSet<Guid>> GetYearEndAdjustmentGeneratedEmployeeIdsAsync(int year, CancellationToken token)
    {
        var ids = await GetQueryable(x => x.PayrollType == PayrollType.YearEndAdjustment && x.PostingPeriod.Year == year)
            .Select(x => x.EmployeeId)
            .Distinct()
            .ToListAsync(token);
        return ids.ToHashSet();
    }

    public async Task<List<Payroll>> GetAsync(
        DateOnly from, DateOnly to,
        Guid? employeeId, Guid? clientId, Guid? payrollGroupId,
        CancellationToken token)
    {
        return await GetQueryable(x =>
                x.PayPeriodStart >= from && x.PayPeriodEnd <= to &&
                (employeeId == null || x.EmployeeId == employeeId) &&
                (clientId == null || x.ClientId == clientId) &&
                (payrollGroupId == null || x.PayrollGroupId == payrollGroupId))
            .Include(x => x.TimeHourPayResults)
            .OrderByDescending(x => x.PayPeriodStart)
            .ThenBy(x => x.FullName)
            .ToListAsync(token);
    }
}

