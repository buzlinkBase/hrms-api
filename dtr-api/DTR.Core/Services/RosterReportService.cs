using DTR.Core.Services;
using Hrms.Core.Services;
using Hrms.Domain;
using Hrms.Domain.ValueObjects;

namespace DTR.Core;

// Resolves the actual duty roster for a date range by composing the same two
// chain-of-responsibility resolvers the DTR pipeline itself uses to determine an
// employee's effective shift and rest-day status — Work Rotation Plan (date override)
// > Fixed Schedule (day-of-week default) > Permanent Shift, and Change Rest Day (override)
// > Rest Day Date > weekly RestDays — and the same employee-extraction query
// (EmployeeService.GetForDTRRunAsync) the live DTR run itself uses, instead of
// re-deriving any of this logic here.
public class RosterReportService
{
    private readonly EmployeeService _employeeService;
    private readonly WorkScheduleResolver _workScheduleResolver;
    private readonly RestDayResolver _restDayResolver;

    public RosterReportService(
        EmployeeService employeeService,
        WorkScheduleResolver workScheduleResolver,
        RestDayResolver restDayResolver)
    {
        _employeeService = employeeService;
        _workScheduleResolver = workScheduleResolver;
        _restDayResolver = restDayResolver;
    }

    public async Task<List<RosterReportModel>> RosterReportQuery(DTRRequestPayload payload, CancellationToken token)
    {
        var fromDate = DateOnly.FromDateTime(payload.FromDate);
        var toDate = DateOnly.FromDateTime(payload.ToDate);

        var employees = await _employeeService.GetForDTRRunAsync(payload, token);
        if (employees.Count == 0) return new List<RosterReportModel>();

        var shifts = await _workScheduleResolver.Resolve(fromDate, toDate, employees, token);
        var restDays = await _restDayResolver.ResolveAsync(fromDate, toDate, employees, token);

        var results = new List<RosterReportModel>();
        for (var date = fromDate; date <= toDate; date = date.AddDays(1))
        {
            foreach (var employee in employees)
            {
                shifts.TryGetValue(new CurrentTimeShiftKey(employee.Id, date), out var shift);
                var isRestDay = restDays.ContainsKey(new ResDaykey(employee.Id, date));

                results.Add(new RosterReportModel
                {
                    WorkDate = date,
                    EmployeeId = employee.Id,
                    EmployeeNo = employee.EmpNo ?? string.Empty,
                    FullName = employee.FullName(),
                    Department = employee.DepartmentName,
                    ShiftId = shift?.Id,
                    ShiftName = shift?.ShiftName ?? "Unassigned",
                    ShiftStart = shift?.StartTime,
                    ShiftEnd = shift?.EndTime,
                    IsRestDay = isRestDay,
                    // Both come straight from the resolver that produced this shift — the
                    // tier that matched (Override/FixedSchedule/Permanent/OpenShift) and,
                    // only for Override, the Work Rotation Plan row backing it.
                    ScheduleSource = shift?.Source ?? ScheduleSource.OpenShift,
                    OverrideId = shift?.OverrideId,
                });
            }
        }

        return results
            .OrderBy(x => x.WorkDate)
            .ThenBy(x => x.FullName)
            .ToList();
    }
}
