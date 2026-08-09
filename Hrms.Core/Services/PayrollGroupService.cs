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
            return new EvaluationResult("Payroll model code is required");
        }
        else if (string.IsNullOrWhiteSpace(model.Name))
        {
            return new EvaluationResult("Payroll model name is required");
        }
        else if (string.IsNullOrWhiteSpace(model.Status))
        {
            return new EvaluationResult("Payroll model status is required");
        }

        //switch (model.PayrollFrequency)
        //{
        //    case PayrollFrequency.DAILY:
        //        if (model.CutoffDays.Any())
        //            return new EvaluationResult("Daily payroll should not have cutoff days.");
        //        break;
        //    case PayrollFrequency.WEEKLY:
        //        if (model.CutoffDays.Count != 1)
        //            return new EvaluationResult("Weekly payroll must have exactly one cutoff day.");
        //        break;

        //    case PayrollFrequency.SEMI_MONTHLY:
        //        if (model.CutoffDays.Count != 2)
        //            return new EvaluationResult("Semi-monthly payroll must have two cutoff days.");
        //        break;

        //    case PayrollFrequency.MONTHLY:
        //        if (model.CutoffDays.Count != 1)
        //            return new EvaluationResult("Monthly payroll must have one cutoff day.");
        //        break;
        //}
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
        return await GetOneAsync(Id, token);
    }
    public async Task Delete(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);

    }
}

