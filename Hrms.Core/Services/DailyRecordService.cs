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
    private readonly LeaveDtrReconciliationService _reconciliation;

    public DailyRecordService(IUnitOfWorkService uow,
        TypeAdapterConfig config,
        IMapper mapper,
        ILogger<DailyRecordService> logger,
        LeaveDtrReconciliationService reconciliation) : base(uow)
    {
        _config         = config;
        _mapper         = mapper;
        _logger         = logger;
        _reconciliation = reconciliation;
    }
    public async Task AddRangeAsync(List<DailyRecord> records, CancellationToken token)
    {
        await Uow.Repository.AddRangeAsync(records, token);
        await Uow.SaveChangesAsync(token);
        await CommitChangesAsync(token);
    }
    public async Task<string> BuildBatchCodeAsync(DateOnly rangeFrom, DateOnly rangeTo, Guid? payrollGroupId, CancellationToken token)
    {
        TimeZoneInfo manilaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
        DateTime localTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, manilaTimeZone);
        var ts = localTime.ToString("MM.dd.yyyy.HH:mm");

        var payrollGroupCode = payrollGroupId.HasValue
            ? await Context.PayrollGroups
                .Where(x => x.Id == payrollGroupId.Value)
                .Select(x => x.Code)
                .FirstOrDefaultAsync(token)
            : null;

        var pgSegment = string.IsNullOrWhiteSpace(payrollGroupCode) ? "" : $" PG:{payrollGroupCode}";
        return $"DTR{rangeFrom.ToString("MMMddyyyy")}-{rangeTo.ToString("MMMddyyyy")}{pgSegment} TS:{ts}";
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

    public async Task<(Dictionary<EmployeeKey, List<DailyRecordRunModel>> Records, DateOnly FromDate, DateOnly ToDate)>
    LoadForPayrollRunAsync(List<string> batchCodes, CancellationToken token)
    {
        // Guard 1: Argument validation
        ArgumentNullException.ThrowIfNull(batchCodes);

        // Guard 2: Short-circuit empty inputs to avoid unnecessary DB calls
        if (batchCodes.Count == 0)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            return ([], today, today);
        }

        var recordsList = await GetQueryable(x => batchCodes.Contains(x.BatchCode!))
             .AsNoTracking()
             .ProjectToType<DailyRecordRunModel>(_config)
             .ToListAsync(token);

        // Guard 3: Return early if database returns no matching records
        if (recordsList.Count == 0)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            return ([], today, today);
        }

        var records = recordsList
            .GroupBy(x => new EmployeeKey(x.EmployeeId!))
            .ToDictionary(g => g.Key, g => g.ToList());

        // Single-pass optimization for date range calculation
        var fromDate = recordsList.Min(x => x.WorkDate);
        var toDate = recordsList.Max(x => x.WorkDate);

        return (records, fromDate, toDate);
    }

    public async Task<Dictionary<EmployeeKey, List<DailyRecordRunModel>>>
        LoadForPayrollAsync(PayrollCalcPayload payload,
        CancellationToken token)
    {
        IQueryable<DailyRecord> query;

        if (payload.BatchCodes != null && payload.BatchCodes.Count > 0)
        {
            query = GetQueryable(x => x.Posted && payload.BatchCodes.Contains(x.BatchCode!))
                .AsNoTracking()
                .Include(x => x.Employee);
        }
        else
        {
            query = GetQueryable(GetQueryExpression(payload))
                .AsNoTracking()
                .Include(x => x.Employee);
        }

        return await query
            .ProjectToType<DailyRecordRunModel>(_config)
            .Where(x => x.EmployeeId != null)
            .GroupBy(x => new EmployeeKey(x.EmployeeId))
            .ToDictionaryAsync(x => x.Key, x => x.ToList(), token);
    }

    private Expression<Func<DailyRecord, bool>> GetQueryExpression(DTRQueryPayload payload)
    {
        Expression<Func<DailyRecord, bool>> expression = x =>
            x.Posted &&
            x.WorkDate >= payload.FromDate && x.WorkDate <= payload.ToDate &&
            (payload.EmployeeId == null || x.EmployeeId == payload.EmployeeId) &&
            (payload.DepartmentId == null || x.DepartmentId == payload.DepartmentId) &&
            (payload.PayrollGroupId == null || x.PayrollGroupId == payload.PayrollGroupId) &&
            (payload.ClientId == null || x.ClientId == payload.ClientId);
        return expression;
    }

    public async Task PostAsync(string batchCode, CancellationToken token)
    {
        var records = await _uow.Repository
            .Find<DailyRecord>(x => x.BatchCode == batchCode && !x.Posted)
            .Include(x => x.LeavesInfo)
            .ToListAsync(token);

        foreach (var r in records)
            r.Posted = true;

        // Phase 2: convert approval-time reservations into authoritative deductions
        // using PaidLeaveHours/LeavesInfo already set by the DTR computation engine.
        await _reconciliation.ConsumeReservationsAsync(batchCode, records, token);

        await CommitChangesAsync(token);
        _logger.LogInformation("DTR posted: batch {BatchCode} ({Count} records)", batchCode, records.Count);
    }

    public async Task UnpostAsync(string batchCode, CancellationToken token)
    {
        var records = await _uow.Repository
            .Find<DailyRecord>(x => x.BatchCode == batchCode && x.Posted)
            .ToListAsync(token);

        foreach (var r in records)
        {
            r.Posted         = false;
            r.PaidLeaveHours = 0;
        }

        // Reverse Phase 2 credit deductions; restores reservations for still-approved leaves
        await _reconciliation.ReverseConsumptionAsync(batchCode, token);

        await CommitChangesAsync(token);
        _logger.LogInformation("DTR unposted: batch {BatchCode} ({Count} records)", batchCode, records.Count);
    }

    // Unpost individual records by employee + date range (used when a leave status changes
    // after the DTR for that period was already posted).
    public async Task<int> UnpostByDateRangeAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken token)
    {
        var records = await _uow.Repository
            .Find<DailyRecord>(x =>
                x.EmployeeId == employeeId &&
                x.WorkDate   >= from        &&
                x.WorkDate   <= to          &&
                x.Posted)
            .ToListAsync(token);

        if (records.Count == 0) return 0;

        foreach (var r in records)
        {
            r.Posted         = false;
            r.PaidLeaveHours = 0;
        }

        // Caller is responsible for CommitChangesAsync so this can be batched
        // with other changes in the same unit of work.
        return records.Count;
    }

    public async Task<List<BatchesModel>> GetBatches(DateOnly fromDate, DateOnly toDate, CancellationToken token)
    {
        return await Context.DailyTimeRecords
            .Where(x => x.WorkDate >= fromDate && x.WorkDate <= toDate && x.BatchCode != null)
            .GroupBy(x => x.BatchCode)
            .Select(g => new BatchesModel
            {
                Code = g.Key,
                FromDate = g.Min(x => x.WorkDate),
                ToDate = g.Max(x => x.WorkDate),
                EmployeeCount = g.Select(x => x.EmployeeId).Distinct().Count(),
                IsPosted = g.All(x => x.Posted)
            })
            .OrderByDescending(x => x.FromDate)
            .ToListAsync(token);
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
                LeaveHours = x.Sum(x => x.PaidLeaveHours),
                UnpaidLeaveHours = x.Sum(x => x.UnpaidLeaveHours),
            })
            .OrderBy(x => x.FullName)
            .ToListAsync(token);
    }
    public async Task<List<TardinessReportModel>> TardinessReportQuery(DTRRequestPayload payload, CancellationToken token)
    {
        var fromDate = DateOnly.FromDateTime(payload.FromDate);
        var toDate = DateOnly.FromDateTime(payload.ToDate);

        var query =
            from record in _uow.Repository.FindAll<DailyRecord>().AsNoTracking()
            where record.WorkDate >= fromDate && record.WorkDate <= toDate
                && (record.LateMinutes > 0 || record.UTMinutes > 0)
                && (payload.DepartmentId == null || record.DepartmentId == payload.DepartmentId.Value)
                && (payload.BranchId == null || record.BranchId == payload.BranchId.Value)
                && (payload.OperationAreaId == null || record.AreaId == payload.OperationAreaId.Value)
                && (payload.ClientId == null || record.ClientId == payload.ClientId.Value)
                && (payload.EmployeeId == null || record.EmployeeId == payload.EmployeeId.Value)
            join shift in Context.TimeShifts.AsNoTracking()
                on record.ShiftId equals shift.Id into shiftJoin
            from shift in shiftJoin.DefaultIfEmpty()
            select new TardinessReportModel
            {
                WorkDate = record.WorkDate,
                EmployeeNo = record.Employee != null ? record.Employee.EmployeeNo : string.Empty,
                FullName = record.FullName,
                Department = record.Employee != null && record.Employee.Department != null
                    ? record.Employee.Department.Name
                    : null,
                ScheduledIn = record.ShiftStartTime,
                ActualIn = record.StartTime,
                GracePeriodMinutes = shift != null ? shift.GracePeriodMinutes : 0,
                TardinessMinutes = record.LateMinutes + record.UTMinutes,
                DeductibleMinutes = record.LateMinutes > (shift != null ? shift.GracePeriodMinutes : 0)
                    ? record.LateMinutes + record.UTMinutes
                    : 0
            };

        return await query
            .OrderBy(x => x.WorkDate)
            .ThenBy(x => x.FullName)
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
                 OBHours = x.OBHours,
                 PaidLeaveHours = x.PaidLeaveHours,
                 UnpaidLeaveHours = x.UnpaidLeaveHours,
                 LeavesInfo = x.LeavesInfo == null ? null : x.LeavesInfo.Select(li => new LeaveMetaDataModel
                 {
                     LeaveId = li.LeaveId,
                     Name = li.Name,
                     Hours = li.Hours,
                     StartDateTime = li.StartDateTime,
                     EndDateTime = li.EndDateTime,
                     PayType = li.PayType,
                 }).ToList(),
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
