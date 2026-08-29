
using Hrms.Domain.Entities;

namespace DTR.Core;

public class CurrentRangeDTRPayloadService
{
    private readonly WorkScheduleResolver _workScheduleResolver;
    private readonly RestDayResolver _restDayResolver;
    private readonly EmployeeService _employeeService;
    private readonly AttendanceService _attendanceService;
    private readonly LeaveApplicationService _leaveApplicationService;
    private readonly TravelOrderApplicationService _travelService;
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
        TravelOrderApplicationService travelService,
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
        _travelService = travelService;
        _otService = otService;
        _utService = utService;
        _generalSettingService = generalSettingService;
        _holidayResolver = holidayResolver;
    }

    public async Task<DTRContextModel> SetPayload(bool canprocess, DTRRequestPayload payload, bool removeDoublePunch = true, CancellationToken token = default)
    {
        var (fromDate, toDate) = GetDateRange(payload);
        var companyPolicy = await LoadCompanyPolicy();
        var gap = removeDoublePunch ? companyPolicy.DoublePunchGap : 0;
        var employees = await ExtractEmployees(payload, token);
        var employeeIds = new HashSet<Guid>(employees.Select(e => e.Id));

        var cleanAttendance = await LoadCleanAttendance(canprocess, payload, removeDoublePunch, employeeIds, gap, token);
        var clientIds = ExtractClientIds(employees);
        var shiftsTask = await _workScheduleResolver.Resolve(fromDate, toDate, employees, token);

        var travelsTask = await _travelService.FindByDateRangeAsync(fromDate, toDate, employeeIds, token);
        var leavesTask = await _leaveApplicationService.FindByDateRangeAsync(fromDate, toDate, employeeIds, token);
        var holidaysTask = await _holidayResolver.ResolveAsync(fromDate, toDate, employees, token);
        var overtimeTask = await _otService.FindByDateRangeAsync(fromDate, toDate, employeeIds, token);
        var undertimeTask = await _utService.FindByDateRangeAsync(fromDate, toDate, employeeIds, token);
        var dayOffsTask = await _restDayResolver.ResolveAsync(fromDate, toDate, employees, token);

        var clientPolicy = await LoadClientPolicy(companyPolicy, clientIds);
        var employeePolicy = new Dictionary<EmployeePolicyKey, EmployeePolicyRule>();

        return new DTRContextBuilder()
            .WithCleanAttendance(cleanAttendance)
            .WithEmployees(employees)
            .WithShifts(shiftsTask)
            .WithLeaves(leavesTask)
            .WithTravels(travelsTask)
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
        HashSet<Guid> empIds,
        double DoublePunchGap = 2,
        CancellationToken token = default)
    {
        var (fromDate, toDate) = GetDateRange(payload);
        var rawLogs = await _attendanceService.LoadAttForDTRProcess(payload, empIds, canProcess, token);
        var util = new AttendanceUtility(rawLogs);
        return util.RemoveDoublePunch(DoublePunchGap);
    }

    // Delegates to EmployeeService.GetForDTRRunAsync — the single source of truth for
    // "which employees does this DTR-shaped request cover", shared with the Roster Report
    // so both resolve schedules for the exact same employee set.
    private Task<List<EmployeeDTRRun>> ExtractEmployees(DTRRequestPayload payload, CancellationToken token)
        => _employeeService.GetForDTRRunAsync(payload, token);

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
}

