
using Hrms.Domain.Entities;
using Mapster;
using MassTransit.Initializers;
namespace Hrms.Core.Services;

public class WorkSchedulePlanService : BaseService<WorkSchedulePlan>
{
    private readonly TypeAdapterConfig _config;
    private readonly IMapper _mapper;

    public WorkSchedulePlanService(IUnitOfWorkService uow,
        TypeAdapterConfig config,
        IMapper mapper) : base(uow)
    {
        _config = config;
        _mapper = mapper;
    }

    public async Task AddRange(List<WorkSchedulePlan> models, CancellationToken token)
    {
        if (models == null || !models.Any())
            return;

        // 1. Get distinct employee & date pairs
        var keys = models
            .Select(x => new { x.EmployeeId, x.PayrollDate })
            .Distinct()
            .ToList();

        // 2. Loop through each key and delete matching records directly in SQL
        foreach (var key in keys)
        {
            await GetQueryable()
                .Where(x => x.EmployeeId == key.EmployeeId && x.PayrollDate == key.PayrollDate)
                .ExecuteDeleteAsync(token);
        }

        var minDate = models.Min(x => x.PayrollDate);
        var maxDate = models.Max(x => x.PayrollDate);
        var batchCode = GetBatchCode(minDate, maxDate);

        foreach (var model in models)
        {
            model.BatchCode = batchCode;
        }
        // 3. Add new records and persist
        await CreateRangeAsync(models, token);
        await CommitChangesAsync(token);
    }

    private string GetBatchCode(DateOnly minDate, DateOnly maxDate)
    {
        var yr = DateTime.UtcNow.Year;
        var count = Context.WorkSchedulePlans
            .Where(x => x.PayrollDate.Year == yr)
            .GroupBy(x => x.BatchCode).Count() + 1;
        return $"WRP{minDate.ToString("MMMddyyyy")}{maxDate.ToString("MMMddyyyy")}{count.ToString().PadLeft(5, '0')}";
    }

    public async Task AddAsync(WorkSchedulePlan model, CancellationToken token)
    {
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }

    public async Task UpdateAsync(UpdateWorkSchedulePlan payload, CancellationToken token)
    {
        var existing = await Context.WorkSchedulePlans.FindAsync(new object[] { payload.Id }, token);
        payload.Adapt(existing);
        await ModifyAsync(existing, token);
        await CommitChangesAsync(token);
    }

    public async Task<List<WorkSchedulePlanModel>> FindAllAsync(WorkRotationPlanFilter filter,
        CancellationToken token)
    {
        DateTime fromDateTime = filter.FromDate.ToDateTime(TimeOnly.MinValue);
        DateTime toDateTime = filter.ToDate.ToDateTime(TimeOnly.MaxValue);

        var data = await GetQueryable()
           .AsNoTracking()
           .Where(x => (!filter.EmployeeId.HasValue || x.EmployeeId == filter.EmployeeId.Value)
                    && x.CreatedAt >= fromDateTime
                    && x.CreatedAt <= toDateTime)
           .ToListAsync(token);

        return data
            .Select(x => new WorkSchedulePlanModel
            {
                Id = x.Id,
                TimeShiftId = x.TimeShiftId,
                EmployeeId = x.EmployeeId,
                BatchCode = x.BatchCode,
                FullName = x.Employee.FullName(),
                ShiftName = x.Employee?.TimeShift?.ShiftName,
                PayrollDate = x.PayrollDate,
            }).ToList();
    }

    //public async Task<List<WorkSchedulePlanModel>> FindRange(DateRequestPayload payload,
    //    CancellationToken token)
    //{
    //    return await _uow.Repository
    //          .FindAll<WorkSchedulePlan>()
    //          .AsNoTracking()
    //          .Where(x => x.PayrollDate >= payload.FromDate && x.PayrollDate <= payload.ToDate)
    //          .ProjectToType<WorkSchedulePlanModel>(_config)
    //          .ToListAsync(token)
    //          ;
    //}
    //public async Task<WorkSchedulePlan?> FineOneAsync(Guid Id, CancellationToken token)
    //{
    //    return await GetOneAsync(Id, token);
    //}

    public async Task DeleteAsync(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);
    }

    public async Task DeleteBatchAsync(string batchCode, CancellationToken token)
    {
        await Context.WorkSchedulePlans
            .Where(x => x.BatchCode == batchCode)
            .ExecuteDeleteAsync(token);
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
