using Hrms.Core.Validations;
using Hrms.Domain.Entities;
using System.Linq.Expressions;

namespace Hrms.Core.Services;

public class DeductionApplicationService : BaseService<DeductionApplication>
{
    private readonly DeductionService _DeductionService;
    private readonly EmployeeService _employeeService;
    private readonly IMapper _mapper;

    public DeductionApplicationService(IUnitOfWorkService uow,
           DeductionService DeductionService,
           EmployeeService employeeService,
           IMapper mapper
        ) : base(uow)
    {
        _DeductionService = DeductionService;
        _employeeService = employeeService;
        _mapper = mapper;
    }

    protected override async Task<EvaluationResult> CreateValidatorAsync(DeductionApplication model, CancellationToken token)
    {
        var validator = new DeductionApplicationValidator(_DeductionService, _employeeService).Validate(model);
        if (!validator.IsValid)
        {
            return EvaluationResult.Fail(validator.Errors);
        }
        return await base.CreateValidatorAsync(model, token);
    }

    private async Task AddOrDeleteChildAsync(DeductionApplication model, CancellationToken token)
    {
        var existing = await _uow.Context.DeductionApplicationDetails
         .Where(x => x.ApplicationId == model.Id)
         .ToListAsync(token);

        foreach (var detail in model.Breakdown)
        {
            var match = existing.FirstOrDefault(x => x.Id == detail.Id);
            if (match != null)
            {
                // Update fields
                match.Date = detail.Date;
                match.Amount = detail.Amount;
                match.Balance = detail.Balance;
                match.Notes = detail.Notes;
            }
            else
            {
                await _uow.Context.DeductionApplicationDetails.AddAsync(detail, token);
            }
        }
        // remove deleted ones
        var toRemove = existing.Where(x => !model.Breakdown.Any(d => d.Id == x.Id));
        _uow.Context.DeductionApplicationDetails.RemoveRange(toRemove);
    }
    public async Task<DeductionApplication> AddAsync(CreateDeductionApplication payload, CancellationToken token)
    {
        var model = _mapper.Map<DeductionApplication>(payload);
        foreach (var item in payload.Breakdown)
        {
            var detail = new DeductionApplicationDetail
            {
                Date = item.Date,
                Interest = item.Interest,
                Principal = item.Principal,
                Amount = item.Amount,
                Balance = item.Amount,
                ApplicationId = model.Id,
                DeductionId = model.DeductionId,
                EmployeeId = model.EmployeeId,
            };
            model.Breakdown.Add(detail);
        }
        await AddOrDeleteChildAsync(model, token);
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
        return model;
    }
    public async Task<DeductionApplication> UpdateAsync(UpdateDeductionApplication payload,
        CancellationToken token)
    {
        var model = _mapper.Map<DeductionApplication>(payload);
        foreach (var item in payload.Details)
        {
            var detail = new DeductionApplicationDetail
            {
                Id = item.Id,
                Date = item.Date,
                Interest = item.Interest,
                Principal = item.Principal,
                Amount = item.Amount,
                Balance = item.Amount,
                ApplicationId = model.Id,
                DeductionId = model.DeductionId,
                EmployeeId = model.EmployeeId,
            };
            model.Breakdown.Add(detail);
        }
        await AddOrDeleteChildAsync(model, token);
        await ModifyAsync(model, token);
        await CommitChangesAsync(token);
        return model;
    }
    public Task<List<DeductionApplication>> FindAllAsync(CancellationToken token)
    {
        return GetQueryable().ToListAsync(token);
    }

    public Task<List<DeductionApplicationDetail>> FindDetail(Expression<Func<DeductionApplicationDetail, bool>> expression)
    {
        return _uow.Repository.Find(expression).ToListAsync();
    }

    public async Task<DeductionApplication?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }
    public async Task DeleteParent(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);

        var models = _uow.Repository
            .Find<DeductionApplicationDetail>(x => x.ApplicationId == Id);
        _uow.Repository.RemoveRange(models);
        await CommitChangesAsync(token);

    }
    public async Task DeleteChildAsync(Guid Id, CancellationToken token)
    {
        var item = _uow.Repository.FindOne<DeductionApplicationDetail>(Id);
        if (item == null) return;
        _uow.Repository.Remove(item);
        await CommitChangesAsync(token);
    }
}

public class DeductionAplDtlService : BaseService<DeductionApplicationDetail>
{
    public DeductionAplDtlService(IUnitOfWorkService uow) : base(uow) { }
    public Task<Dictionary<EmployeeKey, List<DeductionInfo>>>
        LoadAsync(List<Guid> employeeIds, DateOnly fromDate, DateOnly toDate,
        CancellationToken token)
    {
        return GetQueryable()
            .AsNoTracking()
            .Where(x => x.Date <= toDate && employeeIds.Contains(x.EmployeeId) && x.Balance > 0)
            .GroupBy(x => new EmployeeKey(x.EmployeeId))
            .ToDictionaryAsync(x => x.Key, x => x
                .Select(x => new DeductionInfo
                {
                    Id = x.Id,
                    PayrollDate = x.Date,
                    Amount = x.Balance,
                    DeductionId = x.DeductionId,
                }).ToList(), token)
            ;
    }
}
