using AutoMapper;
using AutoMapper.QueryableExtensions;
using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

public class TimeShiftService : BaseService<TimeShift>
{
    private readonly IMapper _mapper;

    public TimeShiftService(IUnitOfWorkService uow,
        IMapper mapper) : base(uow)
    {
        _mapper = mapper;
    }
    public async Task<TimeShiftModel> AddAsync(CreateTimeShift payload, CancellationToken token)
    {
        var model = _mapper.Map<TimeShift>(payload);
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
        return _mapper.Map<TimeShiftModel>(model);
    }
 

    public async Task<TimeShiftModel> UpdateAsync(Guid id, UpdateTimeShift payload,
        CancellationToken token)
    {
        var model = _mapper.Map<TimeShift>(payload);
        model.Id = id;
        await ModifyAsync(model, token);
        await CommitChangesAsync(token);
        return _mapper.Map<TimeShiftModel>(model);
    }

    public async Task<List<TimeShiftModel>> FindAllAsync(CancellationToken token)
    {
        return await GetQueryable()
            .ProjectTo<TimeShiftModel>(_mapper.ConfigurationProvider)
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

