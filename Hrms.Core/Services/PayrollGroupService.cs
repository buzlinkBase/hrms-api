using Hrms.Domain.Entities;
using Mapster;

namespace Hrms.Core.Services;

public class PayrollGroupService : BaseService<PayrollGroup>
{
    public PayrollGroupService(IUnitOfWorkService uow) : base(uow)
    {
    }
    protected override async Task<EvaluationResult> CreateValidatorAsync(PayrollGroup model,
        CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(model.Code))
        {
            return new EvaluationResult("Payroll group code is required");
        }
        else if (string.IsNullOrWhiteSpace(model.Name))
        {
            return new EvaluationResult("Payroll group name is required");
        }
        else if (string.IsNullOrWhiteSpace(model.Status))
        {
            return new EvaluationResult("Payroll group status is required");
        }

        // Runs for both create and update (BaseService.ModifyAsync reuses this same hook),
        // so cutoff misconfiguration is caught here instead of surfacing later as a
        // CutoffMismatchException mid payroll-run — see CutoffPolicyResolver.
        var cutoffDays = model.CutoffDays?.ToList() ?? new List<CutoffDay>();

        // GetCurrentCutoff throws the moment payroll runs for this group if it has none —
        // every frequency needs at least one, including MONTHLY/DAILY.
        if (cutoffDays.Count == 0)
        {
            return new EvaluationResult("At least one cutoff day is required.");
        }

        foreach (var cd in cutoffDays)
        {
            if (!cd.IsEndOfMonth && (cd.Day < 1 || cd.Day > 31))
            {
                var name = string.IsNullOrWhiteSpace(cd.Label) ? cd.Day.ToString() : cd.Label;
                return new EvaluationResult($"Cutoff day '{name}' must be between 1 and 31.");
            }
        }

        // Two rows pointing at the same actual day make CutoffPolicyResolver's
        // first/second/last-cutoff detection ambiguous.
        if (cutoffDays.Count(cd => cd.IsEndOfMonth) > 1)
        {
            return new EvaluationResult("Only one cutoff day can be marked as End of Month.");
        }
        var duplicateDay = cutoffDays
            .Where(cd => !cd.IsEndOfMonth)
            .GroupBy(cd => cd.Day)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicateDay != null)
        {
            return new EvaluationResult($"Cutoff day {duplicateDay.Key} is configured more than once.");
        }

        // Semi-Monthly and Weekly rely on a genuine "first" and "second" cutoff
        // (CutoffPolicyResolver.GetFirstCutoff/GetSecondCutoff) — a single cutoff throws
        // CutoffMismatchException as soon as a Fixed/PerPayroll statutory calculator runs.
        if (model.PayrollFrequency is PayrollFrequency.SEMI_MONTHLY or PayrollFrequency.WEEKLY
            && cutoffDays.Count < 2)
        {
            var frequencyName = model.PayrollFrequency == PayrollFrequency.SEMI_MONTHLY ? "Semi-Monthly" : "Weekly";
            return new EvaluationResult($"{frequencyName} payroll requires at least two cutoff days.");
        }

        return await base.CreateValidatorAsync(model, token);
    }

    public async Task AddAsync(PayrollGroup model, CancellationToken token)
    {
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }
    public async Task UpdateAsync(UpdatePayrollGroup payload, CancellationToken token)
    {
        var existing = await Context.PayrollGroups
            .Include(x => x.CutoffDays)
            .FirstOrDefaultAsync(x => x.Id == payload.Id, token);
        if (existing == null)
        {
            throw new NotFoundException("Record not found");
        }
        payload.Adapt(existing);
        await ModifyAsync(existing, token);
        await CommitChangesAsync(token);

    }
    public async Task<List<PayrollGroup>> FindAllAsync(RecordStatus? status = RecordStatus.Any)
    {
        return await GetQueryable()
            .Where(x => status == null || status == RecordStatus.Any ? true : x.Status.Contains(status.ToString()))
            .ToListAsync();
    }
    public async Task<PayrollGroup?> FineOneAsync(Guid Id, CancellationToken token)
    {
        var group = await GetOneAsync(Id, token);
        // CutoffDays is an unordered EF Core collection navigation — without an explicit
        // order, rows come back in whatever order the DB happens to return them, which the
        // Payroll Group Detail screen then renders as-is. Order by Id (insertion order) so
        // the list is stable and matches the sequence cutoffs were added in.
        if (group?.CutoffDays != null)
            group.CutoffDays = group.CutoffDays.OrderBy(x => x.Id).ToList();
        return group;
    }
    public async Task Delete(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);

    }
}

