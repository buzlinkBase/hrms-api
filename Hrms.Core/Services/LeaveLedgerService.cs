using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;

namespace Hrms.Core.Services;

public class LeaveLedgerService : BaseService<LeaveLedger>
{
    public LeaveLedgerService(IUnitOfWorkService uow) : base(uow)
    {
    }

    // Rate-weighted convertible leave days per employee, for Last Pay's leave conversion
    // component (PayrollProcessorService.GenerateLastPayAsync) — the caller only needs to
    // multiply the returned value by the employee's own DailyRate to get the peso amount.
    // Reads LeaveCredits.Balance directly — the entity's own authoritative hard balance, not
    // the ledger-derived Sum(Add-Less) LoadCreditsAsync computes for other consumers — summed
    // across every PeriodYear row per employee/leave-type (unused balances carry over),
    // restricted to leave types with ConvertToCash enabled, capped per type by
    // MaxCashConversionDays where set, then weighted by that leave type's own
    // CashConversionRate before being combined across leave types (each type can have a
    // different rate, so the weighting must happen before summing).
    public async Task<Dictionary<Guid, decimal>> GetConvertibleLeaveValueAsync(
        List<Guid> employeeIds, CancellationToken token)
    {
        var rows = await (
            from credits in Context.LeaveCredits.AsNoTracking()
            join leave in Context.Leaves.AsNoTracking() on credits.LeaveId equals leave.Id
            where employeeIds.Contains(credits.EmployeeId) && leave.ConvertToCash
            select new
            {
                credits.EmployeeId, credits.LeaveId, credits.Balance,
                leave.MaxCashConversionDays, leave.CashConversionRate,
            })
            .ToListAsync(token);

        return rows
            .GroupBy(x => new { x.EmployeeId, x.LeaveId })
            .Select(g => new
            {
                g.Key.EmployeeId,
                Value = ComputeConvertibleLeaveValue(
                    g.Sum(x => x.Balance), g.First().MaxCashConversionDays, g.First().CashConversionRate),
            })
            .GroupBy(x => x.EmployeeId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Value));
    }

    // One leave type's rate-weighted convertible value: the balance capped at
    // MaxCashConversionDays (uncapped when null), then weighted by CashConversionRate.
    // `internal` so it's directly unit testable without a database — see
    // GetConvertibleLeaveValueAsync, which sums this across an employee's leave types.
    internal static decimal ComputeConvertibleLeaveValue(decimal balance, double? maxCashConversionDays, decimal cashConversionRate)
    {
        var convertibleDays = maxCashConversionDays is { } max ? Math.Min(balance, (decimal)max) : balance;
        return convertibleDays * cashConversionRate;
    }

    // Manual HR correction of an employee's leave credits balance. Recorded as a
    // LedgerEntryType.Adjustment ledger entry (Add/Less computed from the delta to the
    // new balance) rather than overwriting LeaveCredits.Balance directly, so the ledger's
    // running-balance audit trail stays intact — mirrors the Grant entry pattern already
    // used by LeavePeriodGrantWorker (LeaveCredits + a matching LeaveLedger row, committed
    // together). If no LeaveCredits row exists yet for this employee/leave/year, one is
    // created (calendar-year FromDate/ToDate — this ad-hoc tool doesn't look up the
    // company's configured fiscal year start month the way the automated period grant does).
    public async Task<LeaveCredits> AdjustBalanceAsync(AdjustLeaveCreditsPayload payload, CancellationToken token)
    {
        var credits = await Context.LeaveCredits.FirstOrDefaultAsync(x =>
            x.EmployeeId == payload.EmployeeId &&
            x.LeaveId == payload.LeaveId &&
            x.PeriodYear == payload.Year, token);

        decimal delta;
        if (credits == null)
        {
            credits = new LeaveCredits
            {
                Id = Guid.NewGuid(),
                EmployeeId = payload.EmployeeId,
                LeaveId = payload.LeaveId,
                PeriodYear = payload.Year,
                FromDate = new DateTime(payload.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ToDate = new DateTime(payload.Year, 12, 31, 23, 59, 59, DateTimeKind.Utc),
                Granted = 0m,
                Used = 0m,
                Balance = 0m,
            };
            delta = payload.NewBalance;
            await _uow.Repository.AddAsync(credits, token);
        }
        else
        {
            delta = payload.NewBalance - credits.Balance;
        }

        credits.Granted += delta;
        credits.Balance += delta;

        var particulars = string.IsNullOrWhiteSpace(payload.Particulars)
            ? "Manual HR correction"
            : payload.Particulars;

        await _uow.Repository.AddAsync(new LeaveLedger
        {
            Id = Guid.NewGuid(),
            EmployeeId = payload.EmployeeId,
            LeaveId = payload.LeaveId,
            LeaveCreditsId = credits.Id,
            EntryType = LedgerEntryType.Adjustment,
            EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Add = delta > 0 ? delta : 0m,
            Less = delta < 0 ? -delta : 0m,
            Balance = credits.Balance,
            Particulars = particulars,
        }, token);

        await CommitChangesAsync(token);
        return credits;
    }
    public async Task AddAsync(LeaveLedger model, CancellationToken token)
    {
        if (model == null) return;
        await CreateAsync(model, token);
    }

    public async Task UpdateAsync(LeaveLedger model, CancellationToken token)
    {
        await ModifyAsync(model, token);
    }

    public Task<List<LeaveLedger>> FindAllAsync(CancellationToken token)
    {
        return GetQueryable().ToListAsync(token);
    }

    // Current balance for one employee/leave/year — feeds the Leave Balance Entry form so HR
    // sees what's already on record before typing a new balance (AdjustBalanceAsync above is
    // write-only). Null when no LeaveCredits row exists yet for this combination (nothing has
    // ever been granted/adjusted) — the form should just treat that as an all-zero starting
    // balance rather than an error.
    public async Task<LeaveCreditsBalanceModel?> GetBalanceAsync(Guid employeeId, Guid leaveId, int year, CancellationToken token)
    {
        var credits = await Context.LeaveCredits.AsNoTracking()
            .FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.LeaveId == leaveId && x.PeriodYear == year, token);
        if (credits == null) return null;

        var employee = await Context.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == employeeId, token);
        var leave = await Context.Leaves.AsNoTracking().FirstOrDefaultAsync(x => x.Id == leaveId, token);

        return new LeaveCreditsBalanceModel
        {
            EmployeeId = employeeId,
            EmployeeNo = employee?.EmployeeNo ?? "",
            FullName = employee.FullName(),
            LeaveId = leaveId,
            LeaveCode = leave?.Code ?? "",
            LeaveDescription = leave?.Description ?? "",
            PeriodYear = credits.PeriodYear,
            Granted = credits.Granted,
            Used = credits.Used,
            Balance = credits.Balance,
            Reserved = credits.Reserved,
            AvailableToFile = credits.AvailableToFile,
        };
    }

    public async Task<Dictionary<EmployeeLeaveCreditsKey, decimal>> LoadCreditsAsync(List<Guid> employeeIds,
        CancellationToken token)
    {
        return await GetQueryable()
             .Where(x => employeeIds.Contains(x.EmployeeId))
             .GroupBy(x => new EmployeeLeaveCreditsKey(x.EmployeeId, x.LeaveId))
             .ToDictionaryAsync(x => x.Key, x => x.Sum(x => x.Add - x.Less), token)
             ;
    }

    public async Task<LeaveLedger?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }
    public async Task Delete(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
    }
}
