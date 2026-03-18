using Hrms.Domain.Entities;
using Mapster;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace Hrms.Core.Services;
public class DailyRecordService : BaseService<DailyRecord>
{
    private readonly TypeAdapterConfig _config;
    private readonly IMapper _mapper;
    private readonly ILogger<DailyRecordService> _logger;
    public DailyRecordService(IUnitOfWorkService uow,
        TypeAdapterConfig config,
        IMapper mapper,
        ILogger<DailyRecordService> logger) : base(uow)
    {
        _config = config;
        _mapper = mapper;
        _logger = logger;
    }
    public async Task AddRangeAsync(List<DailyRecord> records, CancellationToken token)
    {
        await _uow.Repository.AddRangeAsync(records, token);
        await CommitChangesAsync(token);
    }

    public async Task DeleteAsync(DateRangePayload payload, CancellationToken token)
    {
        await ExecuteDeleteAsync(x =>
        x.WorkDate >= payload.FromDate
        && x.WorkDate <= payload.ToDate, token);
        await CommitChangesAsync(token);
    }
    public async Task DeleteAsync(DateRangePayload payload, List<Guid> employeeIds,
        CancellationToken token)
    {
        await ExecuteDeleteAsync(x =>
        employeeIds.Contains(x.EmployeeId) &&
        x.WorkDate >= payload.FromDate && x.WorkDate <= payload.ToDate, token);
        await CommitChangesAsync(token);
    }

    public async Task<Dictionary<EmployeeKey, List<DailyRecordRunModel>>>
        LoadForPayrollAsync(DTRQueryPayload payload,
        CancellationToken token)
    {
        var expression = GetQueryExpression(payload)
            ;
        var query = GetQueryable(expression)
            .AsNoTracking()
            .Include(x => x.Employee)
            ;
        var data = query
            .ProjectToType<DailyRecordRunModel>(_config)
            .ToList();

        return await query
            .ProjectToType<DailyRecordRunModel>(_config)
             .Where(x => x.EmployeeId != null) // filter out nulls
            .GroupBy(x => new EmployeeKey(x.EmployeeId))
            .ToDictionaryAsync(x => x.Key, x => x.ToList(), token);
    }

    private Expression<Func<DailyRecord, bool>> GetQueryExpression(DTRQueryPayload payload)
    {
        //Expression<Func<DailyRecord, bool>> expression = x => 
        //         (x.WorkDate >= payload.FromDate && x.WorkDate <= payload.ToDate) &&
        //         (payload.EmployeeId == null || x.EmployeeId == payload.EmployeeId) &&
        //         (payload.DepartmentId == null || (x.DepartmentId.HasValue ? x.DepartmentId.Value == payload.DepartmentId : x.DepartmentId == payload.DepartmentId)) &&
        //         (payload.PayrollGroupId == null || (x.PayrollGroupId.HasValue ? x.PayrollGroupId.Value == payload.PayrollGroupId : x.PayrollGroupId == payload.PayrollGroupId)) &&
        //         (payload.ClientId == null || (x.ClientId.HasValue ? x.ClientId.Value == payload.ClientId : x.ClientId == payload.ClientId))
        //         ;
        Expression<Func<DailyRecord, bool>> expression = x => true;

        return expression;
    }

    public async Task<PaginatedResult<List<DailyRecordModel>>> GetAllPaginatedResult(DTRQueryPayload payload, PaginationPayload pageInfo, CancellationToken token)
    {
        var expression = GetQueryExpression(payload);
        var query = GetQueryable(expression);
        var dataQuery = await PaginatedQuerable(query, pageInfo.Page, pageInfo.Limit).ToListAsync(token);
        var data = _mapper.Map<List<DailyRecordModel>>(dataQuery);
        return new PaginatedResult<List<DailyRecordModel>>
        {
            Data = data,
            MetaData = new PaginationMetaData(await query.CountAsync(), pageInfo.Page, pageInfo.Limit)
        };
    }
}
