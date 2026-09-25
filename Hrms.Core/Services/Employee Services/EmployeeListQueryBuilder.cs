using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Core.Services;

/// <summary>
/// Translates an <see cref="EmployeeListQuery"/> into EF-translatable Where/OrderBy clauses for
/// the Setup → Employee table. Pure IQueryable in, IQueryable out -- no DbContext or I/O -- so
/// EmployeeService.SearchAsync only has to count, page and project the result.
/// </summary>
public static class EmployeeListQueryBuilder
{
    private delegate IOrderedQueryable<Employee> SortStrategy(IQueryable<Employee> query, bool descending);

    // Whitelisted sort keys (the frontend table's column keys) -- an unknown key falls back to
    // the default name order below instead of ever reaching the query as a raw string.
    private static readonly Dictionary<string, SortStrategy> SortStrategies =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["employeeNo"] = (q, d) => By(q, x => x.EmployeeNo, d),
            ["bioId"] = (q, d) => By(q, x => x.BioId, d),
            ["fullName"] = (q, d) => d
                ? q.OrderByDescending(x => x.LastName).ThenByDescending(x => x.FirstName)
                : q.OrderBy(x => x.LastName).ThenBy(x => x.FirstName),
            ["positionName"] = (q, d) => By(q, x => x.Position == null ? null : x.Position.Name, d),
            ["departmentName"] = (q, d) => By(q, x => x.Department == null ? null : x.Department.Name, d),
            ["clientName"] = (q, d) => By(q, x => x.Client == null ? null : x.Client.Name, d),
            ["areaName"] = (q, d) => By(q, x => x.Area == null ? null : x.Area.Name, d),
            ["branchName"] = (q, d) => By(q, x => x.Branch == null ? null : x.Branch.Name, d),
            ["payrollGroupName"] = (q, d) => By(q, x => x.PayrollGroup == null ? null : x.PayrollGroup.Name, d),
            ["jobLevel"] = (q, d) => By(q, x => x.JobLevel, d),
            ["timeShiftName"] = (q, d) => By(q, x => x.TimeShift == null ? null : x.TimeShift.ShiftName, d),
            ["salaryType"] = (q, d) => By(q, x => x.SalaryType, d),
            ["hireDate"] = (q, d) => By(q, x => x.HireDate, d),
            ["employmentStatus"] = (q, d) => By(q, x => x.EmploymentStatus, d),
            ["status"] = (q, d) => By(q, x => x.Status, d),
            ["gender"] = (q, d) => By(q, x => x.Gender, d),
            ["email"] = (q, d) => By(q, x => x.Email, d),
            ["contact"] = (q, d) => By(q, x => x.Contact, d),
            ["sssNo"] = (q, d) => By(q, x => x.SSSNo, d),
            ["phicNo"] = (q, d) => By(q, x => x.PHICNo, d),
            ["hdmfNo"] = (q, d) => By(q, x => x.HDMFNo, d),
            ["tin"] = (q, d) => By(q, x => x.TIN, d),
        };

    private static IOrderedQueryable<Employee> By<TKey>(
        IQueryable<Employee> query, System.Linq.Expressions.Expression<Func<Employee, TKey>> key, bool descending) =>
        descending ? query.OrderByDescending(key) : query.OrderBy(key);

    public static IQueryable<Employee> ApplyFilters(this IQueryable<Employee> query, EmployeeListQuery q)
    {
        if (!string.IsNullOrWhiteSpace(q.Keyword))
        {
            var k = q.Keyword.Trim();
            query = query.Where(x =>
                x.FirstName.Contains(k) ||
                x.LastName.Contains(k) ||
                x.MiddleName.Contains(k) ||
                x.Suffix.Contains(k) ||
                x.EmployeeNo.Contains(k) ||
                (x.Email != null && x.Email.Contains(k)) ||
                (x.BioId.HasValue && x.BioId.Value.ToString().Contains(k)) ||
                x.SSSNo.Contains(k) ||
                x.PHICNo.Contains(k) ||
                x.HDMFNo.Contains(k) ||
                x.TIN.Contains(k) ||
                (x.Client != null && x.Client.Name.Contains(k)) ||
                (x.PayrollGroup != null && x.PayrollGroup.Name.Contains(k)) ||
                (x.Department != null && x.Department.Name.Contains(k)) ||
                (x.Area != null && x.Area.Name.Contains(k)) ||
                (x.Branch != null && x.Branch.Name.Contains(k)) ||
                (x.Position != null && x.Position.Name.Contains(k)) ||
                (x.TimeShift != null && x.TimeShift.ShiftName.Contains(k)));
        }

        if (HasAny(q.ClientIds)) query = query.Where(x => x.ClientId.HasValue && q.ClientIds!.Contains(x.ClientId.Value));
        if (HasAny(q.PayrollGroupIds)) query = query.Where(x => q.PayrollGroupIds!.Contains(x.PayrollGroupId));
        if (HasAny(q.DepartmentIds)) query = query.Where(x => x.DepartmentId.HasValue && q.DepartmentIds!.Contains(x.DepartmentId.Value));
        if (HasAny(q.AreaIds)) query = query.Where(x => x.AreaId.HasValue && q.AreaIds!.Contains(x.AreaId.Value));
        if (HasAny(q.BranchIds)) query = query.Where(x => x.BranchId.HasValue && q.BranchIds!.Contains(x.BranchId.Value));
        if (HasAny(q.PositionIds)) query = query.Where(x => x.PositionId.HasValue && q.PositionIds!.Contains(x.PositionId.Value));
        if (HasAny(q.TimeShiftIds)) query = query.Where(x => x.TimeShiftId.HasValue && q.TimeShiftIds!.Contains(x.TimeShiftId.Value));

        if (HasAny(q.JobLevels)) query = query.Where(x => q.JobLevels!.Contains(x.JobLevel));
        if (HasAny(q.SalaryTypes)) query = query.Where(x => q.SalaryTypes!.Contains(x.SalaryType));
        if (HasAny(q.EmploymentStatuses)) query = query.Where(x => q.EmploymentStatuses!.Contains(x.EmploymentStatus));
        if (HasAny(q.Statuses)) query = query.Where(x => q.Statuses!.Contains(x.Status));
        if (HasAny(q.Genders)) query = query.Where(x => q.Genders!.Contains(x.Gender));

        if (Text(q.EmployeeNo) is { } employeeNo) query = query.Where(x => x.EmployeeNo.Contains(employeeNo));
        if (Text(q.BioId) is { } bioId) query = query.Where(x => x.BioId.HasValue && x.BioId.Value.ToString().Contains(bioId));
        if (Text(q.FullName) is { } name)
            query = query.Where(x => x.FirstName.Contains(name) || x.LastName.Contains(name) || x.MiddleName.Contains(name));
        if (Text(q.Email) is { } email) query = query.Where(x => x.Email != null && x.Email.Contains(email));
        if (Text(q.Contact) is { } contact) query = query.Where(x => x.Contact.Contains(contact));
        if (Text(q.SSSNo) is { } sss) query = query.Where(x => x.SSSNo.Contains(sss));
        if (Text(q.PHICNo) is { } phic) query = query.Where(x => x.PHICNo.Contains(phic));
        if (Text(q.HDMFNo) is { } hdmf) query = query.Where(x => x.HDMFNo.Contains(hdmf));
        if (Text(q.TIN) is { } tin) query = query.Where(x => x.TIN.Contains(tin));

        if (q.HireDateFrom is { } from) query = query.Where(x => x.HireDate >= from);
        if (q.HireDateTo is { } to) query = query.Where(x => x.HireDate <= to);

        return query;
    }

    /// <summary>Unknown/missing SortField falls back to last name, first name. Id is always the
    /// final tiebreaker so rows with equal sort values keep a stable order across pages.</summary>
    public static IQueryable<Employee> ApplySort(this IQueryable<Employee> query, string? sortField, string? sortOrder)
    {
        var descending = string.Equals(sortOrder, "descend", StringComparison.OrdinalIgnoreCase);
        var ordered = sortField != null && SortStrategies.TryGetValue(sortField, out var strategy)
            ? strategy(query, descending)
            : SortStrategies["fullName"](query, descending: false);
        return ordered.ThenBy(x => x.Id);
    }

    private static bool HasAny<T>(List<T>? values) => values is { Count: > 0 };

    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
