using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;

namespace Hrms.Core.Services;

public record PayrollInclusionResult(
    bool IsRestDayPaid,
    bool IsRegularHolidayIncluded,
    bool IsSpecialNonWorkingIncluded,
    bool IsNightDiffIncluded)
{
    public static readonly PayrollInclusionResult AllFalse = new(false, false, false, false);
}

public class EmployeePayrollInclusionResolver
{
    private readonly PayrollInclusionDefaultsService _defaultsService;

    public EmployeePayrollInclusionResolver(PayrollInclusionDefaultsService defaultsService)
    {
        _defaultsService = defaultsService;
    }
    // Mutates each employee's 4 IsXxxIncluded booleans in place to their final resolved
    // value, so every downstream reader (the DTR pay policies) sees the resolved value
    // without ever knowing resolution happened.
    public async Task ApplyAsync(List<EmployeeModelPayrollRun> employees, CancellationToken token)
    {
        if (employees == null || employees.Count == 0) return;
        var tenantDefaults = await _defaultsService.FineOneAsync(token);
        var overrideHandler = new EmployeeOverrideHandler();
        var tenantHandler = new TenantDefaultHandler(tenantDefaults);
        overrideHandler.SetNextHandler(tenantHandler);

        foreach (var employee in employees)
        {
            var result = overrideHandler.Handle(employee);
            employee.IsRestDayPaid = result.IsRestDayPaid;
            employee.IsRegularHolidayIncluded = result.IsRegularHolidayIncluded;
            employee.IsSpecialNonWorkingIncluded = result.IsSpecialNonWorkingIncluded;
            employee.IsNightDiffIncluded = result.IsNightDiffIncluded;
        }
    }
}

public abstract class PayrollInclusionHandler
{
    protected PayrollInclusionHandler NextHandler { get; set; }
    public void SetNextHandler(PayrollInclusionHandler handler)
    {
        NextHandler = handler;
    }
    protected abstract bool IsApplicable(EmployeeModelPayrollRun employee);
    public PayrollInclusionResult Handle(EmployeeModelPayrollRun employee)
    {
        if (IsApplicable(employee))
        {
            return GetInclusion(employee);
        }
        else if (NextHandler != null)
        {
            return NextHandler.Handle(employee);
        }
        return PayrollInclusionResult.AllFalse;
    }
    protected abstract PayrollInclusionResult GetInclusion(EmployeeModelPayrollRun employee);
}

// Highest priority: the employee's own toggles, only when they've explicitly opted
// out of the tenant-wide defaults.
public class EmployeeOverrideHandler : PayrollInclusionHandler
{
    protected override bool IsApplicable(EmployeeModelPayrollRun employee) => employee.UseEmployeeOverride;
    protected override PayrollInclusionResult GetInclusion(EmployeeModelPayrollRun employee) =>
        new(
            employee.IsRestDayPaid,
            employee.IsRegularHolidayIncluded,
            employee.IsSpecialNonWorkingIncluded,
            employee.IsNightDiffIncluded);
}
// Terminal fallback: tenant-wide defaults, used when the employee has no override
// (or no tenant defaults have been configured yet, in which case everything is false).
public class TenantDefaultHandler : PayrollInclusionHandler
{
    private readonly PayrollInclusionDefaults? _tenantDefaults;

    public TenantDefaultHandler(PayrollInclusionDefaults? tenantDefaults)
    {
        _tenantDefaults = tenantDefaults;
    }
    protected override bool IsApplicable(EmployeeModelPayrollRun employee) => true;
    protected override PayrollInclusionResult GetInclusion(EmployeeModelPayrollRun employee) =>
        new(
            _tenantDefaults?.DefaultRestDayPaid ?? false,
            _tenantDefaults?.DefaultRegularHolidayIncluded ?? false,
            _tenantDefaults?.DefaultSpecialNonWorkingIncluded ?? false,
            _tenantDefaults?.DefaultNightDiffIncluded ?? false);
}
