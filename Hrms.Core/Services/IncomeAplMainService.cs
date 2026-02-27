using AutoMapper;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using System.Linq.Expressions;

namespace Hrms.Core.Services;

public class IncomeAplMainService : BaseService<OtherIncomeApplication>
{
    private readonly OtherIncomeService _otherIncomeService;
    private readonly EmployeeService _employeeService;
    private readonly IMapper _mapper;

    public IncomeAplMainService(IUnitOfWorkService uow,
           OtherIncomeService otherIncomeService,
           EmployeeService employeeService,
           IMapper mapper
        ) : base(uow)
    {
        _otherIncomeService = otherIncomeService;
        _employeeService = employeeService;
        _mapper = mapper;
    }

    private async Task<bool> ValidateAsync(OtherIncomeApplication model, CancellationToken token)
    {
        var otherIncome = await _otherIncomeService.FineOneAsync(model.IncomeId, token);
        Guard.ThrowIfNull(otherIncome, "Invalid Income Name");
        var employee = await _employeeService.FineOneAsync(model.EmployeeId, token);
        Guard.ThrowIfNull<Employee>(employee, "Invalid employee");
        model.IsTaxable = otherIncome.IsTaxable;
        return true;
    }

    private async Task AddOrDeleteChildAsync(OtherIncomeApplication model,
        CancellationToken token)
    {
        var existing = await _uow.Context.OtherIncomeApplicationDetails
         .Where(x => x.ApplicationId == model.Id)
         .ToListAsync(token);

        foreach (var detail in model.Schedule)
        {
            var match = existing.FirstOrDefault(x => x.Id == detail.Id);
            if (match != null)//match
            {
                match.Date = detail.Date;
                match.Amount = detail.Amount;
                match.Notes = detail.Notes;
                match.IsTaxable = model.IsTaxable;
                match.IsProrated = model.IsProrated;
            }
            else
            {
                _uow.Context.OtherIncomeApplicationDetails.Add(detail);
            }
        }
        var toRemove = existing.Where(x => !model.Schedule.Any(d => d.Id == x.Id));
        _uow.Context.OtherIncomeApplicationDetails.RemoveRange(toRemove);

    }
    public async Task<OtherIncomeApplication> AddAsync(CreateOtherIncomeApplication payload,
        CancellationToken token)
    {
        var model = _mapper.Map<OtherIncomeApplication>(payload);
        //map detail
        foreach (var item in payload.Schedule)
        {
            var detail = new OtherIncomeSchedules
            {
                Date = item.Date,
                Amount = item.Amount,
                ApplicationId = model.Id,
                IncomeId = model.IncomeId,
                EmployeeId = model.EmployeeId,
            };
            model.Schedule.Add(detail);
        }
        await ValidateAsync(model, token);
        await AddOrDeleteChildAsync(model, token);
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
        return model;
    }
    public async Task<OtherIncomeApplication> UpdateAsync(UpdateOtherIncomeApplication payload,
        CancellationToken token)
    {
        var model = _mapper.Map<OtherIncomeApplication>(payload);
        foreach (var item in payload.Schedule)
        {
            var detail = new OtherIncomeSchedules
            {
                Id = item.Id,
                Date = item.Date,
                Amount = item.Amount,
                ApplicationId = model.Id,
                IncomeId = model.IncomeId,
                EmployeeId = model.EmployeeId,
            };
            model.Schedule.Add(detail);
        }
        await ValidateAsync(model, token);
        await AddOrDeleteChildAsync(model, token);
        await ModifyAsync(model, token);
        await CommitChangesAsync(token);
        return model;
    }

    public Task<List<OtherIncomeApplication>> FindAllAsync(CancellationToken token)
    {
        return GetQueryable().ToListAsync(token);
    }

    public Task<List<OtherIncomeSchedules>> FindDetail(Expression<Func<OtherIncomeSchedules,
        bool>> expression,
        CancellationToken token)
    {
        return _uow.Repository.Find(expression).ToListAsync(token);
    }

    public async Task<OtherIncomeApplication?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }
    public async Task DeleteParent(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);
    }
    public async Task DeleteChild(Guid Id, CancellationToken token)
    {
        var item = await _uow.Repository.FindOneAsync<OtherIncomeSchedules>(Id, token);
        if (item == null) return;
        _uow.Repository.Remove(item);
        await CommitChangesAsync(token);
    }
}

public class IncomeAplDtlService : BaseService<OtherIncomeSchedules>
{
    public IncomeAplDtlService(IUnitOfWorkService uow) : base(uow) { }
    public Task<Dictionary<EmployeeKey, List<OtherIncomeInfo>>> LoadAsync(List<Guid> employeeIds,
         DateOnly fromDate, DateOnly toDate,
         CancellationToken token)
    {
        return GetQueryable()
            .AsNoTracking()
            .Where(x => x.Date >= fromDate && x.Date <= toDate && employeeIds.Contains(x.EmployeeId))
            .GroupBy(x => new EmployeeKey(x.EmployeeId))
            .ToDictionaryAsync(x => x.Key, x => x
                .Select(x => new OtherIncomeInfo
                {
                    Id = x.Id,
                    PayrollDate = x.Date,
                    Amount = x.Amount,
                    IncomeId = x.IncomeId,
                    Taxable = x.IsTaxable
                }).ToList(), token)
        ;
    }
}
