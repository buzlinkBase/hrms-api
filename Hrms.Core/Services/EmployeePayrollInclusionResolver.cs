namespace Hrms.Core.Services;
public record PayrollInclusionResult(
    bool IsRestDayPaid,
    bool IsRegularHolidayIncluded,
    bool IsSpecialNonWorkingIncluded)
{
    public static readonly PayrollInclusionResult AllFalse = new(false, false, false);
}

public class EmployeePayrollInclusionResolver
{

    public EmployeePayrollInclusionResolver(  )
    {
    }
    // Mutates each employee's 4 IsXxxIncluded booleans in place to their final resolved
    // value, so every downstream reader (the DTR pay policies) sees the resolved value
    // without ever knowing resolution happened.
    public async Task ApplyAsync(List<EmployeeModelPayrollRun> employees, CancellationToken token)
    {
        if (employees == null || employees.Count == 0) return;
        var overrideHandler = new EmployeeOverrideHandler();
        foreach (var employee in employees)
        {
            var result = overrideHandler.Handle(employee);
            employee.IsRestDayPaid = result.IsRestDayPaid;
            employee.IsRegularHolidayIncluded = result.IsRegularHolidayIncluded;
            employee.IsSpecialNonWorkingIncluded = result.IsSpecialNonWorkingIncluded;
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
    protected override bool IsApplicable(EmployeeModelPayrollRun employee) => true;
    protected override PayrollInclusionResult GetInclusion(EmployeeModelPayrollRun employee) =>
        new(
            employee.IsRestDayPaid,
            employee.IsRegularHolidayIncluded,
            employee.IsSpecialNonWorkingIncluded);
} 