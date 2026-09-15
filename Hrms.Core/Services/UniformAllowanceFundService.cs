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
            Add = amount,
            Balance = fund.Balance,
            Particulars = particulars,
            AccrualDedupeKey = $"{employeeId}|{entryDate:yyyy-MM}",
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

    private static bool IsDuplicateKeyException(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) == true ||
        ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase)    == true;

    // Manual HR correction -- an additive delta + direction, not "set a new balance": a Remove
    // is clamped so it can never drive Balance negative.
    public async Task AdjustAsync(Guid employeeId, decimal amount, bool isAddition, string particulars, DateOnly entryDate, CancellationToken token)
    {
        var fund = await GetOrCreateFundAsync(employeeId, token);
        var add = isAddition ? amount : 0m;
        var less = isAddition ? 0m : ClampAmount(amount, fund.Balance);
        fund.Balance += add - less;
        Context.UniformAllowanceLedgers.Add(new UniformAllowanceLedger
        {
            EmployeeId = employeeId,
            UniformAllowanceFund = fund,
            EntryType = UniformAllowanceEntryType.Adjustment,
            EntryDate = entryDate,
            Add = add,
            Less = less,
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
                Less = payout,
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
        return await Context.UniformAllowanceFunds
            .Where(x => employeeIds.Contains(x.EmployeeId))
            .ToDictionaryAsync(x => x.EmployeeId, x => x.Balance, token);
    }
}
