using Hrms.Domain.Entities;
using Mapster;

namespace Hrms.Core.Services;

public class TimeShiftService : BaseService<TimeShift>
{
    private readonly TypeAdapterConfig _config;
    private readonly IMapper _mapper;

    public TimeShiftService(IUnitOfWorkService uow,
        TypeAdapterConfig config,
        IMapper mapper) : base(uow)
    {
        _config = config;
        _mapper = mapper;
    }

    protected override async Task<EvaluationResult> CreateValidatorAsync(TimeShift model, CancellationToken token = default)
    {
        await base.CreateValidatorAsync(model, token);
        Guard.ThrowIfEmpty(model.ShiftName, nameof(model.ShiftName));
        return EvaluationResult.OK;
    }
    public async Task<TimeShiftModel> AddAsync(CreateTimeShift payload, CancellationToken token)
    {
        var model = _mapper.Map<TimeShift>(payload);
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
        return _mapper.Map<TimeShiftModel>(model);
    }

    public async Task AddRangeAsync(List<TimeShift> models, CancellationToken token)
    {
        await CreateRangeAsync(models, token);
    }

    public async Task<TimeShiftModel> UpdateAsync(Guid id, UpdateTimeShift payload,
        CancellationToken token)
    {
        var effectiveId = (payload.Id ?? Guid.Empty) == Guid.Empty ? id : payload.Id.Value;
        var existing = await Context.TimeShifts.FindAsync(new object[] { effectiveId }, token);
        payload.Adapt(existing);
        await ModifyAsync(existing, token);
        await CommitChangesAsync(token);
        return _mapper.Map<TimeShiftModel>(existing);
    }

    public async Task<List<TimeShiftModel>> FindAllAsync(CancellationToken token)
    {
        return await GetQueryable()
            .ProjectToType<TimeShiftModel>(_config)
            .ToListAsync(token);
    }
    public async Task<TimeShift?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }
    public async Task DeleteAsync(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);

    }
}

