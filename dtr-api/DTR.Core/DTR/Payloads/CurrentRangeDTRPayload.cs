
using Hrms.Domain.Entities;

namespace DTR.Core;

public class CurrentRangeDTRPayloadService
{
    private readonly WorkScheduleResolver _workScheduleResolver;
    private readonly RestDayResolver _restDayResolver;
    private readonly EmployeeService _employeeService;
    private readonly AttendanceService _attendanceService;
    private readonly LeaveApplicationService _leaveApplicationService;
    private readonly OvertimeApplicationService _otService;
    private readonly UnderTimeApplicationService _utService;
    private readonly GeneralSettingService _generalSettingService;
    private readonly HolidayResolver _holidayResolver;
    public CurrentRangeDTRPayloadService(
        WorkScheduleResolver workScheduleResolver,
        RestDayResolver restDayResolver,
        EmployeeService employeeService,
        AttendanceService attendanceService,
        LeaveApplicationService leaveApplicationService,
        OvertimeApplicationService otService,
        UnderTimeApplicationService utService,
        GeneralSettingService generalSettingService,
        HolidayResolver holidayResolver)
    {
        _workScheduleResolver = workScheduleResolver;
        _restDayResolver = restDayResolver;
        _employeeService = employeeService;
        _attendanceService = attendanceService;
        _leaveApplicationService = leaveApplicationService;
        _otService = otService;
        _utService = utService;
        _generalSettingService = generalSettingService;
        _holidayResolver = holidayResolver;
    }

    public async Task<DTRContextModel> SetPayload(bool canprocess,
        DTRRequestPayload payload, bool removeDoublePunch = true,
        CancellationToken token = default)
    {
        var (fromDate, toDate) = GetDateRange(payload);
        var cleanAttendance = await LoadCleanAttendance(canprocess, payload, removeDoublePunch, token);
        var employees = await ExtractEmployees(payload, token);
        var employeeIds = new HashSet<Guid>(employees.Select(e => e.Id));
        var clientIds = ExtractClientIds(employees);
        var shiftsTask = await _workScheduleResolver.Resolve(fromDate, toDate, employees, token);
        var leavesTask = await _leaveApplicationService.FindByDateRangeAsync(fromDate, toDate, employeeIds, token);
        var holidaysTask = await _holidayResolver.ResolveAsync(fromDate, toDate, employees, token);
        var overtimeTask = await _otService.FindByDateRangeAsync(fromDate, toDate, employeeIds, token);
        var undertimeTask = await _utService.FindByDateRangeAsync(fromDate, toDate, employeeIds, token);
        var dayOffsTask = await _restDayResolver.ResolveAsync(fromDate, toDate, employees, token);

        var companyPolicy = await LoadCompanyPolicy();
        var clientPolicy = await LoadClientPolicy(companyPolicy, clientIds);
        var employeePolicy = await LoadEmployeePolicy(employeeIds);

        return new DTRContextBuilder()
            .WithCleanAttendance(cleanAttendance)
            .WithEmployees(employees)
            .WithShifts(shiftsTask)
            .WithLeaves(leavesTask)
            .WithHolidays(holidaysTask)
            .WithDayOffs(dayOffsTask)
            .WithOverTimeApplications(overtimeTask)
            .WithUnderTimeApplications(undertimeTask)
            .WithCompanyPolicy(companyPolicy)
            .WithClientPolicy(clientPolicy)
            .WithEmployeePolicy(employeePolicy)
            .Build();
    }

    private static (DateOnly fromDate, DateOnly toDate) GetDateRange(DTRRequestPayload payload)
    {
        var from = DateOnly.FromDateTime(payload.FromDate.Date.AddDays(TimeAllowance.AttLookbackDays));
        var to = DateOnly.FromDateTime(payload.ToDate.Date.AddDays(TimeAllowance.AttLookforward));
        return (from, to);
    }

    private async Task<Dictionary<AttendanceEmpId, List<Attendance>>> LoadCleanAttendance(
        bool canProcess,
        DTRRequestPayload payload,
        bool removeDoublePunch,
        CancellationToken token = default)
    {

        var (fromDate, toDate) = GetDateRange(payload);
        var rawLogs = await _attendanceService.LoadAttForDTRProcess(payload, canProcess, token);
        var util = new AttendanceUtility(rawLogs);
        var gap = removeDoublePunch ? TimeAllowance.DoublePunchGap : 0;
        return util.RemoveDoublePunch(gap);
    }

    private async Task<List<EmployeeDTRRun>> ExtractEmployees(DTRRequestPayload payload, CancellationToken token)
    {
        var query = _employeeService.GetQueryable(x => x.BioId > 0);

        if (payload.EmployeeId.HasValue)
        {
            query = query.Where(x => x.Id == payload.EmployeeId.Value);
        }
        else
        {
            if (payload.ClientId.HasValue)
                query = query.Where(x => x.ClientId == payload.ClientId);

            if (payload.PayrollGroupId.HasValue)
                query = query.Where(x => x.PayrollGroupId == payload.PayrollGroupId);

            if (payload.DepartmentId.HasValue)
                query = query.Where(x => x.DepartmentId == payload.DepartmentId);
        }

        return await query.Select(x => new EmployeeDTRRun
        {
            Id = x.Id,
            AreaId = x.AreaId,
            ClientId = x.ClientId,
            FirstName = x.FirstName,
            LastName = x.LastName,
            MiddleName = x.MiddleName,
            Suffix = x.Suffix,
            TimeShiftId = x.TimeShiftId,
            BioId = x.BioId!.Value,
            PayrollGroupId = x.PayrollGroupId,
            DepartmentId = x.DepartmentId,
            DepartmentName = x.Department != null ? x.Department.Name : null,
            RestDays = x.RestDays.Select(r => new RestDayModel
            {
                DayName = r.DayName,
                Id = r.Id,
            }).ToList()
        }).ToListAsync(token);
    }

    private static List<Guid?> ExtractClientIds(List<EmployeeDTRRun> employees)
    {
        return employees
            .Where(e => e.ClientId.HasValue && e.ClientId != Guid.Empty)
            .Select(e => e.ClientId)
            .ToList();
    }

    private async Task<CompanyPolicyRule> LoadCompanyPolicy()
    {
        var settings = await _generalSettingService.GetSettingsAsync("Company");
        return new CompanyPolicyService().Transform(settings);
    }

    private async Task<Dictionary<ClientPolicyKey, ClientPolicyRule>> LoadClientPolicy(CompanyPolicyRule companyPolicy, List<Guid?> clientIds)
    {
        var ids = new HashSet<string>(clientIds.Select(id => id!.Value.ToString()));
        var settings = await _generalSettingService.GetSettingsAsync("Client", ids);
        return new ClientPolicyService().Transform(companyPolicy, settings);
    }

    private async Task<Dictionary<EmployeePolicyKey, EmployeePolicyRule>> LoadEmployeePolicy(HashSet<Guid> employeeIds)
    {
        var ids = new HashSet<string>(employeeIds.Select(id => id.ToString()));
        var settings = await _generalSettingService.GetSettingsAsync("Employee", ids);
        return new EmployeePolicyService().Transform(settings);
    }
}

