using System.Linq.Expressions;
using DocumentFormat.OpenXml.Office2010.ExcelAc;
using Hrms.Domain.Entities;
using Mapster;
using Microsoft.Extensions.Logging;

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
    public async Task DeleteAsync(string batchCode, CancellationToken token)
    {
        await ExecuteDeleteAsync(x =>
        x.BatchCode == batchCode, token);
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

    public async Task<List<BatchesModel>> GetBatches(DateOnly fromDate, DateOnly toDate, CancellationToken token)
    {
        return await Context.DailyTimeRecords
            .Where(x => x.WorkDate >= fromDate && x.WorkDate <= toDate && x.BatchCode != null)
            .Select(x => new BatchesModel
            {
                Code = x.BatchCode
            })
            .Distinct()
            .ToListAsync(token)
            ;
    }

    public async Task<List<DTRSummaryModel>> DTRSummaryQuery(string BatchCode, CancellationToken token)
    {

        var dtr = _uow.Repository
            .FindAll<DailyRecord>()
            .AsNoTracking()
            .Where(x => x.BatchCode == BatchCode)
             ;

        return await dtr
            //.Where(x => x.Posted)
            .GroupBy(x => new { x.EmployeeId, x.BatchCode })
            .Select(x => new DTRSummaryModel()
            {
                FullName = x.First().FullName,
                EmployeeId = x.Key.EmployeeId,
                BatchCode = x.Key.BatchCode,
                UTHours = x.Sum(x => x.UTMinutes / 60),
                OverHours = x.Sum(x => x.OverMinutes / 60),
                LateHours = x.Sum(x => x.LateForOTMinutes / 60),

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
                AbsentCount = x.Sum(x => x.AbsentCount),
            })
            .OrderBy(x => x.FullName)
            .ToListAsync(token);
    }
    public async Task<List<DTRDetailModel>> DTRDetailQuery(string batchCode, CancellationToken token)
    {
        return await _uow.Repository
             .FindAll<DailyRecord>()
             .AsNoTracking()
             .Where(x => x.BatchCode == batchCode)
             .Select(x => new DTRDetailModel
             {
                 Id = x.Id,
                 ShiftWorkingHour = x.ShiftWorkingHour,
                 HolCount = x.HolCount,
                 SPCount = x.SPCount,
                 FullName = x.FullName,
                 EmployeeId = x.EmployeeId,
                 WorkDate = x.WorkDate,
                 ShiftName = x.ShiftName,
                 ShiftStartTime = x.ShiftStartTime,
                 ShiftEndTime = x.ShiftEndTime,
                 StartTime = x.StartTime,
                 EndTime = x.EndTime,
                 WorkType = x.WorkType,
                 WorkTypeEnum = x.WorkTypeEnum,
                 LateMinutes = x.LateMinutes,
                 UTMinutes = x.UTMinutes,
                 OverMinutes = x.OverMinutes,
                 LateForOTMinutes = 0,
                 OBHours = 0,
                 LeaveHours = 0,
                 AbsentCount = x.AbsentCount,

                 RegularNetHours = x.RegularNetHours,
                 RegularOTHours = x.RegularOTHours,
                 RegularNDHours = x.RegularNDHours,
                 RegularNDOTHours = x.RegularNDOTHours,

                 RestDayHours = x.RestDayHours,
                 RestDayOTHours = x.RestDayOTHours,
                 RestDayNDHours = x.RestDayNDHours,
                 RestDayNDOTHours = x.RestDayNDOTHours,

                 LegalHolHours = x.LegalHolHours,
                 LegalHolOTHours = x.LegalHolOTHours,
                 LegalHolNightDiffHours = x.LegalHolNightDiffHours,
                 LegalHolNightDiffOTHours = x.LegalHolNightDiffOTHours,

                 SpecialHolHours = x.SpecialHolHours,
                 SpecialHolOTHours = x.SpecialHolOTHours,
                 SpecialHolNightDiffHours = x.SpecialHolNightDiffHours,
                 SpecialHolNightDiffOTHours = x.SpecialHolNightDiffOTHours,

                 RestLegalDayHours = x.RestLegalDayHours,
                 RestLegalDayOTHours = x.RestLegalDayOTHours,
                 RestLegalDayNDHours = x.RestLegalDayNDHours,
                 RestLegalDayNDOTHours = x.RestLegalDayNDOTHours,

                 RestSpecialDayHours = x.RestSpecialDayHours,
                 RestSpecialDayOTHours = x.RestSpecialDayOTHours,
                 RestSpecialDayNDHours = x.RestSpecialDayNDHours,
                 RestSpecialDayNDOTHours = x.RestSpecialDayNDOTHours,
             })
             .OrderBy(x => x.FullName)
             .ThenBy(x => x.WorkDate)
             .ToListAsync(token);
    }

}
