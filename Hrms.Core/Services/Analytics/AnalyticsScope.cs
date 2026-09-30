using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Core.Services.Analytics;

/// <summary>
/// The employees an Analytics filter covers, as a query every area builds on. It goes through the
/// normal repository, so it's already this company's and already hides confidential employees
/// from anyone not cleared to see them -- and so do the records filtered by these employee ids.
/// Department/branch/payroll group are each employee's current one (history isn't tracked).
/// </summary>
public static class AnalyticsScope
{
    public static IQueryable<Employee> Employees(IRepository repository, AnalyticsFilter filter) =>
        repository.FindAll<Employee>().AsNoTracking()
            .Where(e =>
                (filter.DepartmentId == null || e.DepartmentId == filter.DepartmentId) &&
                (filter.BranchId == null || e.BranchId == filter.BranchId) &&
                (filter.PayrollGroupId == null || e.PayrollGroupId == filter.PayrollGroupId) &&
                (filter.EmploymentStatus == null || e.EmploymentStatus == filter.EmploymentStatus));

    public static IQueryable<Guid> EmployeeIds(IRepository repository, AnalyticsFilter filter) =>
        Employees(repository, filter).Select(e => e.Id);

    public const string Unassigned = "Unassigned";
}
