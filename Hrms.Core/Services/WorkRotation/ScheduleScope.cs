using Hrms.Domain.ValueObjects;

namespace Hrms.Core.Services.WorkRotation;

/// <summary>
/// One way a supervisor's reach over schedules can come about. ScheduleScopeService unions every
/// registered rule, so a new kind of reach is a new rule, not another branch in the controller.
/// </summary>
public interface IScheduleScopeRule
{
    Task<List<EmployeeFilterResponseModel>> EmployeesAsync(Guid supervisorEmployeeId, CancellationToken token);
}

// The employees who report to the supervisor (Employee.ManagerId).
public class DirectReportScopeRule : IScheduleScopeRule
{
    private readonly EmployeeService _employees;

    public DirectReportScopeRule(EmployeeService employees)
    {
        _employees = employees;
    }

    public Task<List<EmployeeFilterResponseModel>> EmployeesAsync(Guid supervisorEmployeeId, CancellationToken token)
    {
        return _employees.Filter(new EmployeeFilter { ManagerId = supervisorEmployeeId }, token);
    }
}

// The employees assigned to a project site the supervisor was granted.
public class GrantedSiteScopeRule : IScheduleScopeRule
{
    private readonly EmployeeService _employees;
    private readonly SupervisorSiteGrantService _grants;

    public GrantedSiteScopeRule(EmployeeService employees, SupervisorSiteGrantService grants)
    {
        _employees = employees;
        _grants = grants;
    }

    public async Task<List<EmployeeFilterResponseModel>> EmployeesAsync(Guid supervisorEmployeeId, CancellationToken token)
    {
        var result = new List<EmployeeFilterResponseModel>();
        foreach (var areaId in await _grants.GetAreaIdsAsync(supervisorEmployeeId, token))
        {
            result.AddRange(await _employees.Filter(new EmployeeFilter { OperationAreaId = areaId }, token));
        }
        return result;
    }
}

/// <param name="Employees">Everyone the supervisor may schedule.</param>
/// <param name="AllowedAreaIds">The project sites they may send people to. Empty means no site
/// restriction (a supervisor with no grants keeps the direct-reports-only behavior).</param>
public record ScheduleScope(List<EmployeeFilterResponseModel> Employees, HashSet<Guid> AllowedAreaIds)
{
    public bool CanSchedule(IEnumerable<Guid> employeeIds)
    {
        var allowed = Employees.Select(e => e.Id).ToHashSet();
        return employeeIds.All(allowed.Contains);
    }

    public bool CanUseAreas(IEnumerable<Guid> areaIds) =>
        AllowedAreaIds.Count == 0 || areaIds.All(AllowedAreaIds.Contains);
}

public class ScheduleScopeService
{
    private readonly IEnumerable<IScheduleScopeRule> _rules;
    private readonly SupervisorSiteGrantService _grants;

    public ScheduleScopeService(IEnumerable<IScheduleScopeRule> rules, SupervisorSiteGrantService grants)
    {
        _rules = rules;
        _grants = grants;
    }

    public async Task<ScheduleScope> ResolveAsync(Guid supervisorEmployeeId, CancellationToken token)
    {
        var employees = new Dictionary<Guid, EmployeeFilterResponseModel>();
        foreach (var rule in _rules)
        {
            foreach (var employee in await rule.EmployeesAsync(supervisorEmployeeId, token))
            {
                employees.TryAdd(employee.Id, employee);
            }
        }

        var areaIds = await _grants.GetAreaIdsAsync(supervisorEmployeeId, token);
        return new ScheduleScope(employees.Values.ToList(), areaIds.ToHashSet());
    }
}
