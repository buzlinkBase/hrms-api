using Hrms.Domain.Entities;
using Mapster;

namespace Hrms.Core.Services;

public class LeaveApplicationService : BaseService<LeaveApplication>
{
    private readonly TypeAdapterConfig _config;
    private readonly IMapper _mapper;
    public LeaveApplicationService(IUnitOfWorkService uow,
        TypeAdapterConfig config,
        IMapper mapper) : base(uow)
    {
        _config = config;
        _mapper = mapper;
    }

    protected override Task<EvaluationResult> CreateValidatorAsync(LeaveApplication model, CancellationToken token = default)
    {
        Guard.ThrowIfNull(model, nameof(LeaveApplication));
        return base.CreateValidatorAsync(model, token);
    }

    public async Task<LeaveApplicationModel?> AddAsync(CreateLeaveApplication payload, CancellationToken token)
    {
        var model = _mapper.Map<LeaveApplication>(payload);
        if (model == null) return null;
        await CreateAsync(model, token);
        await AddDetailAsync(model, token);
        await CommitChangesAsync(token);
        return _mapper.Map<LeaveApplicationModel>(model);
    }

    public async Task<LeaveApplicationModel?> AddAsync(LeaveApplication model,
        CancellationToken token)
    {
        await CreateAsync(model, token);
        await AddDetailAsync(model, token);
        return _mapper.Map<LeaveApplicationModel>(model);
    }

    private async Task AddDetailAsync(LeaveApplication model, CancellationToken token)
    {
        DateTime start = model.LeaveDateFrom.ToDateTime(TimeOnly.MinValue);
        DateTime end = model.LeaveDateTo.ToDateTime(TimeOnly.MinValue);
        int totalDays = (end - start).Days;
        _uow.Repository.Remove<LeaveApplicationDetail>(x => x.ApplicationId == model.Id);
        for (int i = 0; i < totalDays; i++)
        {
            var lad = new LeaveApplicationDetail()
            {
                ApplicationId = model.Id,
                LeaveDate = model.LeaveDateFrom.AddDays(i),
            };
            _uow.Repository.Add(lad);
        }
        await Task.CompletedTask;
    }
    public async Task UpdateAsync(UpdateLeaveApplication payload, CancellationToken token)
    {
        var existing = await Context.leaveApplications.FindAsync(new object[] { payload.Id }, token);
        if (existing == null)
        {
            throw new NotFoundException("Record not found");
        }
        payload.Adapt(existing);
        await ModifyAsync(existing, token);
        await AddDetailAsync(existing, token);
        await CommitChangesAsync(token);
    }

    public async Task<Dictionary<EmployeePayDateKey, List<LeaveApplicationPyRun>>>
        FindLeaveDetailsAsync(DateOnly fromDate, DateOnly toDate,
        CancellationToken token)
    {
        //EMPLOYEE CAN HAVE 2 LEAVES IN A DAY EXAMPLE:HALF DAY FOR EACH TYPE OF LEAVE
        // MUST BE APPROVED 
        return await _uow.Repository
            .Find<LeaveApplicationDetail>(x =>
                x.LeaveDate >= fromDate &&
                x.LeaveDate <= toDate &&
                x.Application.ApprovalStatus == ApprovalStatus.Approved)
            .ProjectToType<LeaveApplicationPyRun>(_config)
            .GroupBy(x => new EmployeePayDateKey(x.EmployeeId, x.LeaveDate))
            .ToDictionaryAsync(g => g.Key, g => g.ToList(), token);
    }
    public Task<List<LeaveApplicationModel>> RangeAsync(DateEmployeeRequestPayload payload,
        CancellationToken token)
    {
        return GetQueryable()
            .Where(x => x.LeaveDateFrom >= payload.FromDate
                    && x.LeaveDateTo <= payload.ToDate
                    && payload.EmployeeIds.Contains(x.EmployeeId))
            .ProjectToType<LeaveApplicationModel>(_config)
            .ToListAsync(token);
    }

    public Task<List<LeaveApplicationModel>> FindAllAsync(CancellationToken token, DateOnly? from = null, DateOnly? to = null)
    {
        var query = GetQueryable();
        if (from.HasValue) query = query.Where(x => DateOnly.FromDateTime(x.CreatedAt) >= from.Value);
        if (to.HasValue) query = query.Where(x => DateOnly.FromDateTime(x.CreatedAt) <= to.Value);
        return query.ProjectToType<LeaveApplicationModel>(_config).ToListAsync(token);
    }
    public async Task<LeaveApplicationModel?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return _mapper.Map<LeaveApplicationModel>(await GetOneAsync(Id, token));
    }
    public async Task Delete(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
    }

    public async Task<Dictionary<Leavekey, List<LeaveApplication>>>
        FindByDateRangeAsync(DateOnly fromDate,
        DateOnly toDate,
        HashSet<Guid> employeeIds,
        CancellationToken token)
    {
        var data = await _uow.Repository
                .Find<LeaveApplication>(x => x.LeaveDateFrom >= fromDate
                    && x.ApprovalStatus == ApprovalStatus.Approved
                    && x.LeaveDateTo <= toDate
                    && employeeIds.Contains(x.EmployeeId))
                 .GroupBy(a => new Leavekey(a.EmployeeId))
                 .ToDictionaryAsync(g => g.Key, g => g.OrderBy(x => x.LeaveDateFrom).ToList(), token);
        ;
        return data;
    }
}
public readonly record struct Leavekey(Guid EmpId);
public class ApprovalResult
{
    public string Status { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; }
    public string Message { get; set; }
}