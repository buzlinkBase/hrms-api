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
        await Uow.Repository.AddRangeAsync(records, token);
        await Uow.SaveChangesAsync(token);
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

    public async Task<List<DailyRecordModel>> DTRSummaryQuery(string BatchCode, CancellationToken token)
    {
        //var fromDate = DateOnly.FromDateTime(payload.FromDate);
        //var toDate = DateOnly.FromDateTime(payload.ToDate);
        var dtr = _uow.Repository
            .FindAll<DailyRecord>()
            .AsNoTracking()
            .Where(x => x.BatchCode == BatchCode)
             ;
            //.Where(x =>
            //     //(x.WorkDate >= fromDate && x.WorkDate <= toDate) &&
            //     (payload.EmployeeId == null || x.EmployeeId == payload.EmployeeId) &&
            //     (payload.DepartmentId == null || (x.DepartmentId.HasValue ? x.DepartmentId.Value == payload.DepartmentId : x.DepartmentId == payload.DepartmentId)) &&
            //     (payload.PayrollGroupId == null || (x.PayrollGroupId.HasValue ? x.PayrollGroupId.Value == payload.PayrollGroupId : x.PayrollGroupId == payload.PayrollGroupId)) &&
            //     (payload.ClientId == null || (x.ClientId.HasValue ? x.ClientId.Value == payload.ClientId : x.ClientId == payload.ClientId))
            //);

        return await dtr
            .Where(x => x.Posted)
            .GroupBy(x => x.EmployeeId)
            .Select(x => new DailyRecordModel()
            {
                FullName = x.First().FullName,
                EmployeeId = x.Key,
                LateHours = x.Sum(x => x.LateHours),
                UTHours = x.Sum(x => x.UTHours),
                OverBreakHours = x.Sum(x => x.OverBreakHours),
                LateForOTHours = x.Sum(x => x.LateForOTHours),

                RegularNetHours = x.Sum(x => x.RegularNetHours),
                RegularOTHours = x.Sum(x => x.RegularOTHours),
                RegularNDHours = x.Sum(x => x.RegularNDHours),
                RegularNDOTHours = x.Sum(x => x.RegularNDOTHours),

                RestDayHours = x.Sum(x => x.RestDayHours),
                RestDayOTHours = x.Sum(x => x.RestDayOTHours),
                RestDayNDHours = x.Sum(x => x.RestDayNDHours),
                RestDayNDOTHours = x.Sum(x => x.RestDayNDOTHours),

                LegalHolHours = x.Sum(x => x.LegalHolHours),
                LegalHolOTHours = x.Sum(x => x.LegalHolOTHours),
                LegalHolNightDiffHours = x.Sum(x => x.LegalHolNightDiffHours),
                LegalHolNightDiffOTHours = x.Sum(x => x.LegalHolNightDiffOTHours),

                SpecialHolHours = x.Sum(x => x.SpecialHolHours),
                SpecialHolOTHours = x.Sum(x => x.SpecialHolOTHours),
                SpecialHolNightDiffHours = x.Sum(x => x.SpecialHolNightDiffHours),
                SpecialHolNightDiffOTHours = x.Sum(x => x.SpecialHolNightDiffOTHours),

                RestLegalDayHours = x.Sum(xx => xx.RestLegalDayHours),
                RestLegalDayNDHours = x.Sum(xx => xx.RestLegalDayNDHours),
                RestLegalDayNDOTHours = x.Sum(xx => xx.RestLegalDayNDOTHours),
                RestLegalDayOTHours = x.Sum(xx => xx.RestLegalDayOTHours),

                RestSpecialDayHours = x.Sum(xx => xx.RestSpecialDayHours),
                RestSpecialDayNDHours = x.Sum(xx => xx.RestSpecialDayNDHours),
                RestSpecialDayNDOTHours = x.Sum(xx => xx.RestSpecialDayNDOTHours),
                RestSpecialDayOTHours = x.Sum(xx => xx.RestSpecialDayOTHours),
                ShiftWorkingHour = x.Sum(xx => xx.ShiftWorkingHour),

                BranchId = x.First().BranchId,
                DepartmentId = x.First().DepartmentId,
                AreaId = x.First().AreaId,
                PayrollGroupId = x.First().PayrollGroupId,
                ClientId = x.First().ClientId,
                HolCount = x.Sum(xx => xx.HolCount),
                SPCount = x.Sum(xx => xx.SPCount),
                LeaveHours = x.Sum(xx => xx.LeaveHours),
                OB = x.Sum(x => x.OB),
                Absent = x.Sum(x => x.Absent),
            })
            .OrderBy(x => x.FullName)
            .ToListAsync(token);
    }
}
