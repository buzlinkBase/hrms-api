
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Hrms.Domain.Entities;
using MessagePack;

namespace Hrms.Core.Services;

public class WorkSchedulePlanService : BaseService<WorkSchedulePlan>
{
    private readonly IMapper _mapper;

    public WorkSchedulePlanService(IUnitOfWorkService uow, IMapper mapper) : base(uow)
    {
        _mapper = mapper;
    }

    public async Task AddRange(List<WorkSchedulePlan> models, CancellationToken token)
    {
        var recordKeys = models
        .Select(x => new { x.PayrollDate, x.EmployeeId })
        .Distinct()
        .ToList();

        var payrollDates = recordKeys.Select(k => k.PayrollDate).Distinct().ToList();
        var employees = recordKeys.Select(k => k.EmployeeId).Distinct().ToList();

        var allExisting = GetQueryable()
            .Where(x =>
                payrollDates.Contains(x.PayrollDate) &&
                employees.Contains(x.EmployeeId));

        allExisting.ExecuteDelete();
        await CreateRangeAsync(models, token);
        await CommitChangesAsync(token);
    }

    public async Task AddAsync(WorkSchedulePlan model, CancellationToken token)
    {
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }

    public async Task UpdateAsync(WorkSchedulePlan model, CancellationToken token)
    {
        await ModifyAsync(model, token);
        await CommitChangesAsync(token);
    }

    public async Task<List<WorkSchedulePlanModel>> FindAllAsync(CancellationToken token)
    {
        return await GetQueryable()
            .ProjectTo<WorkSchedulePlanModel>(_mapper.ConfigurationProvider)
            .ToListAsync(token);
    }

    public async Task<List<WorkSchedulePlanModel>> FindRange(DateRequestPayload payload,
        CancellationToken token)
    {
        return await _uow.Repository
              .FindAll<WorkSchedulePlan>()
              .AsNoTracking()
              .Where(x => x.PayrollDate >= payload.FromDate && x.PayrollDate <= payload.ToDate)
              .ProjectTo<WorkSchedulePlanModel>(_mapper.ConfigurationProvider)
              .ToListAsync(token)
              ;
    }
    public async Task<WorkSchedulePlan?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }
    public async Task DeleteAsync(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);
    }

    public async Task<Dictionary<CurrentTimeShiftKey, WorkSchedulePlan?>> GetAllCustomShiftsAync(DateOnly fromDate, DateOnly toDate, CancellationToken token)
    {
        return await _uow.Repository
              .FindAll<WorkSchedulePlan>()
              //.Include(x => x.Employee)
              //.ThenInclude(x => x.TimeShift)
              .AsNoTracking()
              .Where(x => x.PayrollDate >= fromDate && x.PayrollDate <= toDate)
              .GroupBy(x => new CurrentTimeShiftKey(x.EmployeeId, x.PayrollDate))
              .ToDictionaryAsync(key => key.Key, val => val.FirstOrDefault(), token)
              ;
    }
}

public readonly record struct CurrentTimeShiftKey(Guid EmpId, DateOnly ShiftDate);
