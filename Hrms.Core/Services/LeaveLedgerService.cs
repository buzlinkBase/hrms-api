using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;

namespace Hrms.Core.Services;

public class LeaveLedgerService : BaseService<LeaveLedger>
{
    public LeaveLedgerService(IUnitOfWorkService uow) : base(uow)
    {
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
