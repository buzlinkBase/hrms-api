namespace Hrms.Domain.ValueObjects;

/// <summary>
/// Server-side paging, sorting and column filtering for the Setup → Employee table
/// (GET employees/list -- see EmployeeService.SearchAsync). Deliberately separate from
/// PaginationPayload / GET employees, which ~20 other screens use to load the full employee
/// list for their dropdowns and must keep behaving exactly as before.
///
/// Every filter is optional; the ones that are set combine with AND. SortField uses the
/// table's own column keys (e.g. "clientName") and SortOrder uses antd's vocabulary
/// ("ascend"/"descend") so the UI can pass its sorter state straight through.
/// </summary>
public class EmployeeListQuery
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 100;

    public int Page { get; set; } = 1;
    public int Limit { get; set; } = DefaultLimit;

    public string? SortField { get; set; }
    public string? SortOrder { get; set; }

    /// <summary>Global search: names, employee no., email, Bio ID, statutory numbers, and the
    /// names of the employee's client, payroll group, department, project site, branch,
    /// position and shift.</summary>
    public string? Keyword { get; set; }

    // Multi-select lookups (match any of the given ids).
    public List<Guid>? ClientIds { get; set; }
    public List<Guid>? PayrollGroupIds { get; set; }
    public List<Guid>? DepartmentIds { get; set; }
    /// <summary>Project Site (the Area / CostCenters entity).</summary>
    public List<Guid>? AreaIds { get; set; }
    public List<Guid>? BranchIds { get; set; }
    public List<Guid>? PositionIds { get; set; }
    public List<Guid>? TimeShiftIds { get; set; }

    // Multi-select enums / fixed values.
    public List<JobLevelOption>? JobLevels { get; set; }
    public List<SalaryType>? SalaryTypes { get; set; }
    public List<EmploymentStatus>? EmploymentStatuses { get; set; }
    public List<string>? Statuses { get; set; }
    public List<string>? Genders { get; set; }

    // Free-text "contains" filters.
    public string? EmployeeNo { get; set; }
    public string? BioId { get; set; }
    /// <summary>Matches first, last or middle name.</summary>
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Contact { get; set; }
    public string? SSSNo { get; set; }
    public string? PHICNo { get; set; }
    public string? HDMFNo { get; set; }
    public string? TIN { get; set; }

    public DateOnly? HireDateFrom { get; set; }
    public DateOnly? HireDateTo { get; set; }

    public int NormalizedPage => Page < 1 ? 1 : Page;
    public int NormalizedLimit => Limit < 1 ? DefaultLimit : Math.Min(Limit, MaxLimit);
}
