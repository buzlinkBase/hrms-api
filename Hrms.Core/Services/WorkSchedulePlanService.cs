
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

    // Changes one entry's shift, payroll date and project site. The employee never changes (delete
    // and add instead). Like AddRange, an employee holds one entry per date, but moving an entry
    // onto a day that already has another one is refused rather than silently replacing it.
    public async Task UpdateAsync(UpdateWorkSchedulePlan payload, CancellationToken token)
    {
        var existing = await Context.WorkSchedulePlans.FirstOrDefaultAsync(x => x.Id == payload.Id, token)
            ?? throw new NotFoundException("Record not found");

        if (existing.PayrollDate != payload.PayrollDate)
        {
            var taken = await Context.WorkSchedulePlans.AnyAsync(
                x => x.EmployeeId == existing.EmployeeId && x.PayrollDate == payload.PayrollDate && x.Id != existing.Id,
                token);
            if (taken)
                throw new ValidationException(
                    $"This employee already has a work rotation on {payload.PayrollDate:MMM dd, yyyy}. Delete that entry first, or pick another date.");
        }

        existing.PayrollDate = payload.PayrollDate;
        existing.TimeShiftId = payload.TimeShiftId;
        existing.OperationAreaId = payload.OperationAreaId;
        existing.ClientId = payload.ClientId;
        await ModifyAsync(existing, token);
        await CommitChangesAsync(token);
    }

    public async Task<WorkSchedulePlanModel?> GetModelAsync(Guid id, CancellationToken token)
    {
        var row = await GetQueryable(x => x.Id == id)
            .AsNoTracking()
            .Include(x => x.OperationArea)
            .Include(x => x.Client)
            .Include(x => x.TimeShift)
            .Include(x => x.Employee)
            .FirstOrDefaultAsync(token);
        return row == null ? null : ToModel(row);
    }

    public async Task<List<WorkSchedulePlanModel>> FindAllAsync(WorkRotationPlanFilter filter,
        CancellationToken token)
    {
        DateTime fromDateTime = filter.FromDate.ToDateTime(TimeOnly.MinValue);
        DateTime toDateTime = filter.ToDate.ToDateTime(TimeOnly.MaxValue);

        var data = await GetQueryable()
           .AsNoTracking()
           .Include(x => x.OperationArea)
           .Include(x => x.Client)
           .Include(x => x.TimeShift)
           // Loaded here rather than lazily per row, which cost one extra query for every entry.
           .Include(x => x.Employee)
           .Where(x => (!filter.EmployeeId.HasValue || x.EmployeeId == filter.EmployeeId.Value)
                    && x.CreatedAt >= fromDateTime
                    && x.CreatedAt <= toDateTime)
           .ToListAsync(token);

        // Grouped by employee, each one's days in date order -- how the list is read.
        return data
            .Select(ToModel)
            .OrderBy(x => x.FullName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.PayrollDate)
            .ToList();
    }

    // The shift shown is the entry's own, not the employee's permanent one -- otherwise editing an
    // entry's shift would never show up in the list.
    private static WorkSchedulePlanModel ToModel(WorkSchedulePlan x) => new()
    {
        Id = x.Id,
        TimeShiftId = x.TimeShiftId,
        EmployeeId = x.EmployeeId,
        BatchCode = x.BatchCode,
        FullName = x.Employee.FullName(),
        ShiftName = x.TimeShift?.ShiftName,
        PayrollDate = x.PayrollDate,
        OperationAreaId = x.OperationAreaId,
        AreaName = x.OperationArea?.Name,
        ClientId = x.ClientId,
        ClientName = x.Client?.Name,
    };

    public Task<bool> AreaExistsAsync(Guid areaId, CancellationToken token)
    {
        return _uow.Repository.FindAll<CostCenters>().AsNoTracking().AnyAsync(x => x.Id == areaId, token);
    }

    public Task<bool> ClientExistsAsync(Guid clientId, CancellationToken token)
    {
        return _uow.Repository.FindAll<Client>().AsNoTracking().AnyAsync(x => x.Id == clientId, token);
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
        var plans = await _uow.Repository
              .FindAll<WorkSchedulePlan>()
              // The day's project site (with its branch/client) rides along with the shift, and so
              // does a client picked directly on the day.
              .Include(x => x.OperationArea)
              .Include(x => x.Client)
              .AsNoTracking()
              .Where(x => x.PayrollDate >= fromDate && x.PayrollDate <= toDate)
              .ToListAsync(token);
        return plans
              .GroupBy(x => new CurrentTimeShiftKey(x.EmployeeId, x.PayrollDate))
              .ToDictionary(key => key.Key, val => val.FirstOrDefault());
    }
}

public readonly record struct CurrentTimeShiftKey(Guid EmpId, DateOnly ShiftDate);
