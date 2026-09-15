using Hrms.Domain.Entities;

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

    // UniformAllowanceAccrualWorker's monthly credit. Deliberately does NOT commit -- the worker
    // calls this once per eligible employee within a single run and commits once at the end,
    // matching LeaveAccrualWorker's one-commit-per-run shape.
    public async Task AccrueAsync(Guid employeeId, decimal amount, DateOnly entryDate, string particulars, CancellationToken token)
    {
        var fund = await GetOrCreateFundAsync(employeeId, token);
        fund.Balance += amount;
        Context.UniformAllowanceLedgers.Add(new UniformAllowanceLedger
        {
            EmployeeId = employeeId,
            UniformAllowanceFund = fund,
            EntryType = UniformAllowanceEntryType.Accrual,
            EntryDate = entryDate,
            Add = amount,
            Balance = fund.Balance,
            Particulars = particulars,
        });
    }

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
