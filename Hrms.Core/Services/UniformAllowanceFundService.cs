using Hrms.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Hrms.Core.Services;

// Owns every write against UniformAllowanceFund/UniformAllowanceLedger -- accrual (called by
// UniformAllowanceAccrualWorker), manual HR adjustment, and release (disbursement for a period).
// Keeping all three write paths here (rather than accrual living inline in the worker, the way
// Retirement's Post-time mutation lives inline in PayrollService) keeps them consistent, since
// there's no natural "owning" payroll-adjacent service the way PayrollService is for Retirement.
public class UniformAllowanceFundService : BaseService<UniformAllowanceFund>
{
    public UniformAllowanceFundService(IUnitOfWorkService uow) : base(uow)
    {
    }

    // Clamp defensively -- never pays out/removes more than what's actually there, and never a
    // negative amount. Mirrors PayrollService.ClampRetirementPayout's exact reasoning.
    internal static decimal ClampAmount(decimal requestedAmount, decimal currentBalance) =>
        Math.Min(Math.Max(requestedAmount, 0), Math.Max(currentBalance, 0));

    private async Task<UniformAllowanceFund> GetOrCreateFundAsync(Guid employeeId, CancellationToken token)
    {
        var fund = await Context.UniformAllowanceFunds.FirstOrDefaultAsync(x => x.EmployeeId == employeeId, token);
        if (fund == null)
        {
            fund = new UniformAllowanceFund { EmployeeId = employeeId, Balance = 0 };
            Context.UniformAllowanceFunds.Add(fund);
        }
        return fund;
    }

    // UniformAllowanceAccrualWorker's monthly credit. Commits per employee (not batched into one
    // SaveChanges for the whole run) specifically so a duplicate-key collision on one employee
    // can never roll back another employee's legitimate accrual in the same run -- this worker
    // only fires once a month, so the extra round-trips are a non-issue. Returns false if this
    // employee's accrual for this month was already committed by another instance/redelivery
    // (caught via AccrualDedupeKey's unique index, the hard stop behind the worker's own
    // pre-check query), true otherwise.
    public async Task<bool> AccrueAsync(Guid employeeId, decimal amount, DateOnly entryDate, string particulars, CancellationToken token)
    {
        var fund = await GetOrCreateFundAsync(employeeId, token);
        fund.Balance += amount;
        var ledgerEntry = new UniformAllowanceLedger
        {
            EmployeeId = employeeId,
            UniformAllowanceFund = fund,
            EntryType = UniformAllowanceEntryType.Accrual,
            EntryDate = entryDate,
            Amount = amount,
            Balance = fund.Balance,
            Particulars = particulars,
            AccrualDedupeKey = AccrualKey(employeeId, entryDate),
        };
        Context.UniformAllowanceLedgers.Add(ledgerEntry);

        try
        {
            await CommitChangesAsync(token);
            return true;
        }
        catch (DbUpdateException ex) when (IsDuplicateKeyException(ex))
        {
            // Revert the in-memory mutation and detach the failed insert -- otherwise EF's
            // change tracker would keep retrying both on the NEXT employee's commit (a single
            // DbContext lives for the whole worker run), poisoning every remaining employee.
            fund.Balance -= amount;
            Context.Entry(fund).State = EntityState.Unchanged;
            Context.Entry(ledgerEntry).State = EntityState.Detached;
            return false;
        }
    }

    internal static string AccrualKey(Guid employeeId, DateOnly creditDate) => $"{employeeId}|{creditDate:yyyy-MM}";

    internal static string ExpiryKey(Guid employeeId, DateOnly date) => $"{employeeId}|expiry|{date:yyyy-MM}";

    /// <summary>The funds of these employees, tracked, for the accrual run to update in place.</summary>
    public Task<Dictionary<Guid, UniformAllowanceFund>> GetTrackedFundsAsync(List<Guid> employeeIds, CancellationToken token) =>
        Context.UniformAllowanceFunds
            .Where(x => employeeIds.Contains(x.EmployeeId))
            .ToDictionaryAsync(x => x.EmployeeId, token);

    /// <summary>A new, tracked, empty fund -- the caller commits it with whatever it records next.</summary>
    public UniformAllowanceFund NewFund(Guid employeeId, DateOnly? serviceStart)
    {
        var fund = new UniformAllowanceFund { EmployeeId = employeeId, Balance = 0, ServiceStartDate = serviceStart };
        Context.UniformAllowanceFunds.Add(fund);
        return fund;
    }

    // The methods below are the accrual run's: they SAVE inside the run's unit-of-work transaction
    // and UniformAllowanceAccrualWorker commits once at the end. BuzlinkRepository's UnitOfWork
    // commits only once per instance -- a later CommitChangesAsync silently saves nothing -- so a
    // commit per employee would keep only the first employee's changes. Saving per employee still
    // isolates a duplicate-key collision to that employee (reverted and detached below).

    /// <summary>
    /// A scheduled credit: one Accrual ledger row (stamped with the policy and period that earned
    /// it) saved together with its credit-log row. False when another instance/redelivery already
    /// credited this employee this month (the dedupe key's unique index) -- then nothing is kept.
    /// </summary>
    public async Task<bool> CreditAsync(UniformAllowanceFund fund, UniformAllowanceCreditLog log, string particulars, CancellationToken token)
    {
        fund.Balance += log.Amount;
        var entry = new UniformAllowanceLedger
        {
            Id = Guid.NewGuid(),
            EmployeeId = log.EmployeeId,
            UniformAllowanceFund = fund,
            EntryType = UniformAllowanceEntryType.Accrual,
            EntryDate = log.CreditDate,
            Amount = log.Amount,
            Balance = fund.Balance,
            Particulars = particulars,
            PolicyId = log.PolicyId,
            PeriodStart = log.PeriodStart,
            PeriodEnd = log.PeriodEnd,
            AccrualDedupeKey = AccrualKey(log.EmployeeId, log.CreditDate),
        };
        log.LedgerId = entry.Id;
        Context.UniformAllowanceLedgers.Add(entry);
        Context.UniformAllowanceCreditLogs.Add(log);
        return await SaveOrRevertAsync(fund, () => fund.Balance -= log.Amount, token, entry, log);
    }

    /// <summary>A skipped credit's log row. False when the day was already decided by another run.</summary>
    public async Task<bool> RecordSkipAsync(UniformAllowanceCreditLog log, CancellationToken token)
    {
        Context.UniformAllowanceCreditLogs.Add(log);
        return await SaveOrRevertAsync(null, null, token, log);
    }

    /// <summary>Writes the whole balance off on separation (not saved -- saved with the fund's separation date).</summary>
    public void Forfeit(UniformAllowanceFund fund, DateOnly date, string particulars)
    {
        if (fund.Balance <= 0) return;
        var amount = fund.Balance;
        fund.Balance = 0;
        Context.UniformAllowanceLedgers.Add(new UniformAllowanceLedger
        {
            EmployeeId = fund.EmployeeId,
            UniformAllowanceFund = fund,
            EntryType = UniformAllowanceEntryType.Forfeiture,
            EntryDate = date,
            Amount = -amount,
            Balance = 0,
            Particulars = particulars,
        });
    }

    /// <summary>Lapses part or all of the balance under the policy's expiry rule; once per employee per month.</summary>
    public async Task<bool> ExpireAsync(UniformAllowanceFund fund, decimal amount, DateOnly date, string particulars, CancellationToken token)
    {
        var lapsed = ClampAmount(amount, fund.Balance);
        if (lapsed <= 0) return false;
        fund.Balance -= lapsed;
        var entry = new UniformAllowanceLedger
        {
            EmployeeId = fund.EmployeeId,
            UniformAllowanceFund = fund,
            EntryType = UniformAllowanceEntryType.Expiry,
            EntryDate = date,
            Amount = -lapsed,
            Balance = fund.Balance,
            Particulars = particulars,
            AccrualDedupeKey = ExpiryKey(fund.EmployeeId, date),
        };
        Context.UniformAllowanceLedgers.Add(entry);
        return await SaveOrRevertAsync(fund, () => fund.Balance += lapsed, token, entry);
    }

    /// <summary>Saves fund changes made in place (service start, separation date, a forfeiture).</summary>
    public Task<int> SaveAsync(CancellationToken token) => SaveChangesAsync(token);

    /// <summary>
    /// Credits and additions since <paramref name="since"/> per employee -- what rolling expiry
    /// leaves untouched.
    /// </summary>
    public async Task<Dictionary<Guid, decimal>> FreshCreditsSinceAsync(List<Guid> employeeIds, DateOnly since, CancellationToken token)
    {
        if (employeeIds.Count == 0) return new();
        return await Context.UniformAllowanceLedgers
            .Where(x => employeeIds.Contains(x.EmployeeId) && x.EntryDate >= since && x.Amount > 0 &&
                        (x.EntryType == UniformAllowanceEntryType.Accrual || x.EntryType == UniformAllowanceEntryType.Adjustment))
            .GroupBy(x => x.EmployeeId)
            .Select(g => new { g.Key, Total = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.Key, x => x.Total, token);
    }

    // Saves; on a duplicate-key collision undoes the in-memory change and detaches the inserts,
    // so the shared DbContext doesn't retry them on the next employee's save.
    private async Task<bool> SaveOrRevertAsync(
        UniformAllowanceFund? fund, Action? revert, CancellationToken token, params object[] inserted)
    {
        try
        {
            await SaveChangesAsync(token);
            return true;
        }
        catch (DbUpdateException ex) when (IsDuplicateKeyException(ex))
        {
            revert?.Invoke();
            foreach (var row in inserted) Context.Entry(row).State = EntityState.Detached;
            if (fund != null)
            {
                var entry = Context.Entry(fund);
                entry.State = entry.State == EntityState.Added ? EntityState.Detached : EntityState.Unchanged;
            }
            return false;
        }
    }

    private static bool IsDuplicateKeyException(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) == true ||
        ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase)    == true;

    // Manual HR correction -- an additive delta + direction, not "set a new balance": a Remove
    // is clamped so it can never drive Balance negative.
    public async Task AdjustAsync(Guid employeeId, decimal amount, bool isAddition, string particulars, DateOnly entryDate, CancellationToken token)
    {
        var fund = await GetOrCreateFundAsync(employeeId, token);
        var signed = isAddition ? amount : -ClampAmount(amount, fund.Balance);
        fund.Balance += signed;
        Context.UniformAllowanceLedgers.Add(new UniformAllowanceLedger
        {
            EmployeeId = employeeId,
            UniformAllowanceFund = fund,
            EntryType = UniformAllowanceEntryType.Adjustment,
            EntryDate = entryDate,
            Amount = signed,
            Balance = fund.Balance,
            Particulars = particulars,
        });
        await CommitChangesAsync(token);
    }

    // HR disburses (issues uniforms/cash for) accumulated balance for a specific period, for one
    // or more employees at once -- each employee's amount is independently clamped to their own
    // current balance; the returned list names whoever got clamped so the caller can flag it
    // rather than silently releasing less than what was requested.
    public async Task<List<Guid>> ReleaseBatchAsync(
        List<(Guid EmployeeId, decimal Amount)> releases, DateOnly periodDate, string particulars, CancellationToken token)
    {
        var clampedEmployeeIds = new List<Guid>();
        if (releases.Count == 0) return clampedEmployeeIds;

        var employeeIds = releases.Select(r => r.EmployeeId).Distinct().ToList();
        var funds = await Context.UniformAllowanceFunds
            .Where(x => employeeIds.Contains(x.EmployeeId))
            .ToDictionaryAsync(x => x.EmployeeId, token);

        foreach (var (employeeId, amount) in releases)
        {
            if (!funds.TryGetValue(employeeId, out var fund))
            {
                fund = new UniformAllowanceFund { EmployeeId = employeeId, Balance = 0 };
                Context.UniformAllowanceFunds.Add(fund);
                funds[employeeId] = fund;
            }

            var payout = ClampAmount(amount, fund.Balance);
            if (payout < amount) clampedEmployeeIds.Add(employeeId);
            fund.Balance -= payout;
            Context.UniformAllowanceLedgers.Add(new UniformAllowanceLedger
            {
                EmployeeId = employeeId,
                UniformAllowanceFund = fund,
                EntryType = UniformAllowanceEntryType.Release,
                EntryDate = periodDate,
                Amount = -payout,
                Balance = fund.Balance,
                Particulars = particulars,
            });
        }

        await CommitChangesAsync(token);
        return clampedEmployeeIds;
    }

    public async Task<Dictionary<Guid, decimal>> GetBalancesAsync(List<Guid> employeeIds, CancellationToken token)
    {
        if (employeeIds.Count == 0) return new();
        // Only the two columns needed -- not full tracked fund entities.
        return await Context.UniformAllowanceFunds
            .Where(x => employeeIds.Contains(x.EmployeeId))
            .Select(x => new { x.EmployeeId, x.Balance })
            .ToDictionaryAsync(x => x.EmployeeId, x => x.Balance, token);
    }
}
