using Hrms.Core.Policies.DeductionPolicies;
using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

// Reads, stages and releases PayrollDeductionClaim rows -- the record of which run carries an
// employee's deductions for a deduction period. All lookups are set-based (one query per 1,000
// employees), never per employee.
public class PayrollDeductionClaimService : BaseService<PayrollDeductionClaim>
{
    public PayrollDeductionClaimService(IUnitOfWorkService uow) : base(uow)
    {
    }

    // Who already carries each (employee, period start): the batch id of the run holding the claim.
    public async Task<Dictionary<(Guid EmployeeId, DateOnly PeriodStart), Guid>> FindClaimsAsync(
        IReadOnlyCollection<Guid> employeeIds, DateOnly earliestStart, DateOnly latestStart, CancellationToken token)
    {
        var result = new Dictionary<(Guid, DateOnly), Guid>();
        foreach (var chunk in employeeIds.Chunk(1000))
        {
            var ids = chunk.ToList();
            var rows = await Context.PayrollDeductionClaims
                .AsNoTracking()
                .Where(x => ids.Contains(x.EmployeeId) && x.PeriodStart >= earliestStart && x.PeriodStart <= latestStart)
                .Select(x => new { x.EmployeeId, x.PeriodStart, x.PayrollBatchId })
                .ToListAsync(token);
            foreach (var r in rows) result[(r.EmployeeId, r.PeriodStart)] = r.PayrollBatchId;
        }
        return result;
    }

    // Every claim whose deduction period overlaps the window -- the Cutoff Tracker's carrier column.
    // One query, no employee list: a cutoff has at most one claim per employee.
    public async Task<List<PayrollDeductionClaim>> FindOverlappingAsync(DateOnly from, DateOnly to, CancellationToken token) =>
        await Context.PayrollDeductionClaims
            .AsNoTracking()
            .Where(x => x.PeriodStart <= to && x.PeriodEnd >= from)
            .ToListAsync(token);

    // Stages the claims in the caller's unit of work; the caller's commit writes them with the
    // run, so a unique-index conflict (another run claimed first) rolls the whole run back.
    public void Stage(Guid payrollBatchId, IEnumerable<(Guid EmployeeId, DeductionPeriod Period)> claims)
    {
        Context.PayrollDeductionClaims.AddRange(claims.Select(c => new PayrollDeductionClaim
        {
            EmployeeId = c.EmployeeId,
            PeriodStart = c.Period.Start,
            PeriodEnd = c.Period.End,
            PayrollBatchId = payrollBatchId,
        }));
    }

    // One employee's slice of ReleaseByBatchAsync. An employee this run paid earnings only holds no
    // claim in it (another run carries them), so nothing of theirs is released here.
    public Task ReleaseByBatchAndEmployeeAsync(Guid payrollBatchId, Guid employeeId, CancellationToken token) =>
        Context.PayrollDeductionClaims
            .Where(x => x.PayrollBatchId == payrollBatchId && x.EmployeeId == employeeId)
            .ExecuteDeleteAsync(token);

    // Deleting a run frees every period it carried so another run can take them.
    public Task ReleaseByBatchAsync(Guid payrollBatchId, CancellationToken token) =>
        Context.PayrollDeductionClaims
            .Where(x => x.PayrollBatchId == payrollBatchId)
            .ExecuteDeleteAsync(token);
}
